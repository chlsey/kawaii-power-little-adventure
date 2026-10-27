using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

// TCP host for qa_framework's UnityQAEnvironment. Listens on a background
// thread; all game-state mutation (QaPlayerController) happens on the main
// thread via Update(), since Unity's scene/physics API is not thread-safe.
//
// This host never generates or regenerates a level. It only ever hands off
// control of whatever player PcgDungeonManager already created locally (R
// key / Start) - a connecting QA client takes over an existing player, it
// does not request or cause a new one. See docs/integration-readiness.md in
// rein-powered-QA-framework for the wire contract this implements.
public sealed class QaTcpHost : MonoBehaviour
{
    private const int RayCount = 16;
    private const int DefaultMaxSteps = 1000;

    [SerializeField] private PcgDungeonManager dungeonManager;
    [SerializeField] private int port = 8800;

    private TcpListener listener;
    private Thread acceptThread;
    private TcpClient client;
    private NetworkStream stream;
    private readonly ConcurrentQueue<string> incomingRequests = new ConcurrentQueue<string>();
    private readonly object writeLock = new object();

    private QaPlayerController remotePlayer;
    private int episodeCounter;
    private int stepCountThisEpisode;
    private EpisodeLimits currentLimits;

    private struct EpisodeLimits
    {
        public int MaxSteps;
        public bool MaxDurationProvided;
        public float MaxDurationSeconds;
    }

    private void Start()
    {
        listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();
        acceptThread = new Thread(AcceptLoop) { IsBackground = true };
        acceptThread.Start();
        Debug.Log($"QaTcpHost listening on 127.0.0.1:{port}");
    }

    private void OnDestroy()
    {
        try { listener?.Stop(); } catch (Exception) { /* already stopped */ }
        try { client?.Close(); } catch (Exception) { /* already closed */ }
    }

    private void AcceptLoop()
    {
        try
        {
            while (true)
            {
                TcpClient incoming = listener.AcceptTcpClient();
                AttachClient(incoming);
            }
        }
        catch (SocketException)
        {
            // Listener was stopped (e.g. OnDestroy); exit quietly.
        }
        catch (ObjectDisposedException)
        {
            // Listener was stopped; exit quietly.
        }
    }

    private void AttachClient(TcpClient incoming)
    {
        // Only one QA bot session at a time; a new connection replaces any previous one.
        client?.Close();
        client = incoming;
        stream = client.GetStream();

        if (dungeonManager != null)
            dungeonManager.RemoteControlActive = true;

        SendHandshake();

        var readThread = new Thread(() => ReadLoop(stream)) { IsBackground = true };
        readThread.Start();
    }

    private void ReadLoop(NetworkStream owningStream)
    {
        try
        {
            using var reader = new StreamReader(owningStream, Encoding.UTF8);
            string line;
            while ((line = reader.ReadLine()) != null)
                incomingRequests.Enqueue(line);
        }
        catch (Exception)
        {
            // Connection dropped; nothing further to read from this client.
        }
        finally
        {
            if (dungeonManager != null)
                dungeonManager.RemoteControlActive = false;
            remotePlayer = null;
        }
    }

    private void Update()
    {
        while (incomingRequests.TryDequeue(out string line))
            HandleRequestLine(line);
    }

    private void HandleRequestLine(string line)
    {
        QaWireCommandEnvelope envelope;
        try
        {
            envelope = JsonUtility.FromJson<QaWireCommandEnvelope>(line);
        }
        catch (Exception error)
        {
            SendError($"Malformed request: {error.Message}");
            return;
        }

        switch (envelope?.cmd)
        {
            case "reset":
                HandleReset(line);
                break;
            case "step":
                HandleStep(line);
                break;
            case "close":
                remotePlayer = null;
                SendJson(JsonUtility.ToJson(new QaWireAck()));
                break;
            default:
                SendError($"Unknown command: {envelope?.cmd}");
                break;
        }
    }

    // Takes over whatever player already exists in the scene; never generates
    // or regenerates a level. Resetting with nothing yet generated is an error,
    // not a prompt to create one - generate it in Unity first (R key / Start).
    private void HandleReset(string line)
    {
        QaWireResetRequest request = JsonUtility.FromJson<QaWireResetRequest>(line);

        if (dungeonManager == null || dungeonManager.ActivePlayer == null)
        {
            SendError("No level has been generated yet. Generate one in Unity (Start/R key) before connecting.");
            return;
        }

        remotePlayer = dungeonManager.ActivePlayer;
        remotePlayer.BeginTest();

        episodeCounter++;
        stepCountThisEpisode = 0;
        currentLimits = new EpisodeLimits
        {
            MaxSteps = request.max_steps > 0 ? request.max_steps : DefaultMaxSteps,
            MaxDurationProvided = request.max_duration_provided,
            MaxDurationSeconds = request.max_duration_seconds,
        };

        var reply = new QaWireResetReply
        {
            observation = ToWireObservation(remotePlayer.Observe()),
            info = BuildInfo(step: 0, ended: false, reasonString: null, metrics: remotePlayer.Metrics),
        };
        SendJson(JsonUtility.ToJson(reply));
    }

    private void HandleStep(string line)
    {
        if (remotePlayer == null || !remotePlayer.EpisodeActive)
        {
            SendError("No active remote episode. Call reset first.");
            return;
        }

        QaWireStepRequest request = JsonUtility.FromJson<QaWireStepRequest>(line);
        QaAction action = new QaAction(
            request.action.move_x, request.action.move_y,
            request.action.attack, request.action.interact, request.action.ability);

        QaObservation observation = remotePlayer.ApplyAction(action);
        stepCountThisEpisode++;
        QaEpisodeMetrics metrics = remotePlayer.Metrics;

        bool terminated = false;
        bool truncated = false;
        string reasonString = null;

        // Completion/death precede imposed limits even if a limit is reached on the same step.
        if (metrics.Completed)
        {
            terminated = true;
            reasonString = "completed";
        }
        else if (metrics.PlayerDied)
        {
            terminated = true;
            reasonString = "player_died";
        }
        else if (stepCountThisEpisode >= currentLimits.MaxSteps)
        {
            truncated = true;
            reasonString = "max_steps";
        }
        else if (currentLimits.MaxDurationProvided && metrics.ElapsedSeconds >= currentLimits.MaxDurationSeconds)
        {
            truncated = true;
            reasonString = "max_duration";
        }

        var reply = new QaWireStepReply
        {
            observation = ToWireObservation(observation),
            reward = metrics.Completed ? 1f : 0f,
            terminated = terminated,
            truncated = truncated,
            info = BuildInfo(stepCountThisEpisode, terminated || truncated, reasonString, metrics),
        };
        SendJson(JsonUtility.ToJson(reply));

        if (terminated || truncated)
            remotePlayer = null;
    }

    private QaWireInfo BuildInfo(int step, bool ended, string reasonString, QaEpisodeMetrics metrics)
    {
        bool hasResult = ended && reasonString != null;
        var info = new QaWireInfo
        {
            schema_version = 1,
            step = step,
            episode_id = $"unity-{episodeCounter}",
            // This host never generates a level, so it has no level identity to acknowledge.
            level_acknowledged = false,
            level_id = "",
            generation_seed = 0,
            has_episode_result = hasResult,
        };

        if (hasResult)
        {
            info.episode_result = new QaWireEpisodeResult
            {
                terminated = reasonString == "completed" || reasonString == "player_died",
                truncated = reasonString == "max_steps" || reasonString == "max_duration",
                terminal_reason = reasonString,
                metrics = ToWireMetrics(metrics),
            };
        }

        return info;
    }

    private static QaWireObservation ToWireObservation(QaObservation observation)
    {
        var rays = new QaWireRay[observation.Rays.Length];
        for (int i = 0; i < rays.Length; i++)
        {
            QaRayObservation ray = observation.Rays[i];
            rays[i] = new QaWireRay
            {
                hit = ray.Hit,
                distance_01 = ray.Distance01,
                object_type = ray.ObjectType.ToString().ToUpperInvariant(),
            };
        }

        return new QaWireObservation
        {
            schema_version = 1,
            ray_count = observation.RayCount,
            health_01 = observation.Health01,
            powerup_count = observation.PowerupCount,
            previous_action = ToWireAction(observation.PreviousAction),
            rays = rays,
        };
    }

    private static QaWireAction ToWireAction(QaAction action)
    {
        return new QaWireAction
        {
            move_x = action.MoveX,
            move_y = action.MoveY,
            attack = action.Attack,
            interact = action.Interact,
            ability = action.Ability,
        };
    }

    private static QaWireMetrics ToWireMetrics(QaEpisodeMetrics metrics)
    {
        return new QaWireMetrics
        {
            step_count = metrics.StepCount,
            elapsed_seconds = metrics.ElapsedSeconds,
            completed = metrics.Completed,
            player_died = metrics.PlayerDied,
            enemies_killed = metrics.EnemiesKilled,
            damage_taken = metrics.DamageTaken,
            powerups_used = metrics.PowerupsUsed,
            stuck_events = metrics.StuckEvents,
        };
    }

    private void SendHandshake()
    {
        int observationSize = 7 + 7 * RayCount;
        string handshake =
            "{\"role\":\"qa_gameplay\"," +
            "\"observation_schema_version\":1," +
            $"\"ray_count\":{RayCount}," +
            "\"action_encoding_version\":1," +
            "\"max_ability_slot\":0," +
            // This host only ever controls a pre-existing player; it never
            // generates or acknowledges a specific level recipe.
            "\"supports_level_recipe\":false," +
            "\"acknowledges_level_identity\":false," +
            "\"reports_terminal_reason\":true," +
            "\"reports_episode_metrics\":true," +
            "\"separates_termination_flags\":true," +
            $"\"observation_size\":{observationSize}," +
            "\"action_size\":5}";
        SendJson(handshake);
    }

    private void SendError(string message)
    {
        SendJson(JsonUtility.ToJson(new QaWireErrorReply { error = message }));
    }

    private void SendJson(string json)
    {
        if (stream == null)
            return;

        byte[] bytes = Encoding.UTF8.GetBytes(json + "\n");
        lock (writeLock)
        {
            try
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush();
            }
            catch (Exception)
            {
                // Client disconnected mid-write; the next accepted connection replaces it.
            }
        }
    }
}
