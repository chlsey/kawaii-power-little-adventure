# Dungeon QA recording

QA recording is opt-in. Add **QA Framework > Dungeon QA Adapter** to the scene's `PcgDungeonManager` (or drag the adapter script onto it). Unity adds the required `DungeonStateSource` with the adapter. The manager uses the attached adapter to call `StartUnit("dungeon-<seed>")` after player initialization; the adapter attaches `QaPlayerStateSource` to that player when binding its evidence.

Without an adapter, dungeon play and RL training run without QA state-source components, report delivery, or a Python QA recorder. The saved QA test scenes include the adapter explicitly; `QaPlayerTraining` does not.

```text
+------------------------------------------------------------------+
| SCENE STARTUP                                                    |
| Press Play -> QaRecorderLauncher starts Python.                  |
| Unity connects to port 8800 -> validates TCP readiness.          |
+------------------------------------------------------------------+
             |
             v
+------------------------------------------------------------------+
| GAME: dungeon attempt                                            |
| Enable manager -> accept dungeon -> initialize player.           |
| StartUnit binds state sources.                                   |
| Random or local ONNX policy controls the player.                 |
| Exit / reset / death / timeout -> EndUnit.                       |
+------------------------------------------------------------------+
             |
             v
+------------------------------------------------------------------+
| QA DELIVERY                                                      |
| Aggregate final snapshots -> shared TCP sender on port 8800.     |
+------------------------------------------------------------------+
             |
             v
+------------------------------------------------------------------+
| PYTHON: game-owned recorder                                      |
| Receive -> evaluate reached_exit -> append results.              |
| Log successes / total runs and mean successful exit time.        |
+------------------------------------------------------------------+
```

## Start recording

Open `Assets/Scenes/InitialGenerationScene.unity` for the random baseline, then press Play. The trained scene, `QaPlayerModelTest.unity`, first needs a replacement 37,771-input policy: its retained million-step model expects the earlier 119-input layout and is rejected by the runner. Both scenes have the shared `QaRecorderLauncher` configured to start the game-owned Python recorder, complete its TCP readiness handshake through `DungeonQaAdapter`, and enable `PcgDungeonManager`. The same connection carries the reports. They write fresh timestamped files under `Logs/qa-results/` (`qa-player-model-*.jsonl` or `random-player-*.jsonl`). The exact path and results appear in the Unity Console. Stopping Play closes the connection and stops the owned Python process tree; an unexpected recorder exit pauses the manager.

Python must be on PATH, or set **Python Executable** to your interpreter. The saved scenes use `../rein-powered-QA-framework/src` as **Python Path**, so the sibling checkout provides the framework without installing it. Port 8800 must be free; startup failures leave gameplay paused. The Unity manifest references the sibling framework's `Unity` package.

To add this to another scene, add **QA Framework > QA Recorder Launcher**, configure its arguments and Python path, and assign its unit adapter (or keep both components on the same GameObject). Disable the gameplay controller in the saved scene, then wire **On Ready** to enable it and **On Stopped** to disable it. Run the ordinary Python receive loop; `QAReportReceiver` sends readiness automatically. The launcher checks the report schema and echoed launch session before starting gameplay. Relative paths start at the project root (beside the executable in a desktop build); builds need Python and the scripts/package at the configured paths. Stdout contains logs only.

For optional recording alongside external RL training, first attach `DungeonQaAdapter` to that scene's manager, then start the recorder manually from the game repository. Scenes without a launcher use the same manual command:

```powershell
python -m pip install -e ../rein-powered-QA-framework
python QA/record_dungeon_runs.py
```

Start this manual receiver before the training session. Do not also run it beside a scene's enabled launcher: only one receiver can listen on port 8800. To use an existing manual recorder with either local test scene, disable the launcher and enable the manager before Play.

For a trained-player batch, follow the [model-test scene guide](../Assets/Scenes/QaPlayerModelTest.README.md) to assign a compatible model and read the 100-attempt totals. Player training uses the separate RL bridge on port 7780; recording does not supply training observations, actions, or rewards.

`InitialGenerationScene` automatically generates the next dungeon every 120 seconds of Unity gameplay time. The timer starts over on regeneration and continues after a clear, death, or step limit, so testing proceeds without pressing R. An outstanding attempt is reported before the reset; an already-ended attempt is not counted again. The manager's `Auto Reset Interval Seconds` Inspector field controls this interval; zero disables it. External RL sessions keep trainer-controlled resets. R remains available for an earlier manual reset.

The manual command defaults to `Logs/qa-results/dungeon-runs.jsonl` in the game directory. Use `--output path/to/results.jsonl` for another results file, or `--port` to match the adapter's configured port. Reopening the same file resumes its totals; the launcher supplies a new timestamped filename for each Play session.

## Attempt lifecycle and evidence

- A run starts only after generation succeeds and the player has been spawned/reused and initialized. Generation-only PCG operations and failed generations do not count as player runs.
- Reaching the exit or dying ends the unit through a player event. Local step limits and the RL environment's time limit also end it.
- Regeneration ends an outstanding attempt with reason `reset` before its player/components are destroyed. Resetting after an already-ended attempt produces no second report.
- Each attempt has a fresh framework `attempt_id`, including retries with the same seed. The recorder ignores repeated attempt IDs.
- Disabling/re-enabling the shared adapter does not create unit events. Keep it enabled during delivery; stopping Play is not a dungeon reset and may discard pending sends.

The concrete `DungeonQaReport` supplies this evidence inside the shared schema-v2 envelope:

```json
{
  "seed": 12,
  "elapsed_seconds": 4.5,
  "reached_exit": true,
  "end_reason": "completed"
}
```

- `elapsed_seconds` is the controller's simulated action time: `StepCount * 0.1`. It excludes wall-clock training/network waits and is not a stopwatch measurement of time spent between actions.
- `reached_exit` is the explicit success predicate. Each received ended attempt enters the total, including resets, deaths, and timeouts.
- The recorder adds `time_to_exit_seconds` for successful attempts and `null` for failed attempts. Failed-attempt duration remains available in the evidence.
- Every stored result includes cumulative `successes`, `failures`, `total_runs`, `success_rate` as a fraction, and `mean_exit_seconds` over successful attempts only.
- Totals describe ended attempts received and stored in that file; active attempts and reports lost while the receiver is unavailable are absent. The shared transport has no persistent queue, report retry, or per-report application acknowledgement.

QA evidence snapshots are read only in the unit-end hook. Player control, model inference, observations, and RL rewards stay in the game's control/training path. `ModelSensorPackage` captures rays/traces and updates its persistent grid at model action boundaries. Training and local inference read the same fixed nearby map window; these are model inputs, not QA evidence or report payloads. Sensor attempts reset with player attempts and freeze the final map before regeneration. The receiver sends readiness once per connection; subsequent report processing sends no gameplay commands or QA results back to Unity.

## Check an integration

- Change player state during an attempt; confirm no report arrives before the end and the final evidence reflects the latest state.
- Clear a level, then reset it; expect one report. Retry the same seed; expect a fresh attempt ID.
- Confirm readiness enables gameplay, startup failures leave it paused, and stopping Play cleans up the recorder.
- Read the last JSONL line for cumulative totals. Controlled lifecycle fixtures verify counting; model performance belongs to the model's evaluation results.

See [the shared integration guide](../../rein-powered-QA-framework/docs/integration-readiness.md) for protocol checks and [the model provenance](../Assets/DemoModels/QAPlayer1M/README.md) for retained trained-player results.
