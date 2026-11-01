using System;
using Kcccr.QaFramework;
using UnityEngine;

[Serializable]
public struct DungeonQaEvidence
{
    public int seed;
    public float elapsed_seconds;
    public bool reached_exit;
    public string end_reason;
}

[Serializable]
public sealed class DungeonQaReport : QaUnitReport
{
    public DungeonQaEvidence evidence;
}

/// <summary>Aggregates final dungeon/player snapshots and inherits the shared unit-report transport.</summary>
[AddComponentMenu("QA Framework/Dungeon QA Adapter")]
[RequireComponent(typeof(DungeonStateSource))]
[DisallowMultipleComponent]
public sealed class DungeonQaAdapter : QaUnitAdapter
{
    private IQaStateSource<DungeonState> dungeonSource;
    private IQaStateSource<QaPlayerState> playerSource;

    protected override void OnUnitStart()
    {
        dungeonSource = GetComponent<DungeonStateSource>();
        QaPlayerController player = GetComponent<PcgDungeonManager>().ActivePlayer;
        if (player.GetComponent<QaPlayerStateSource>() == null)
            player.gameObject.AddComponent<QaPlayerStateSource>();
        playerSource = player.GetComponent<QaPlayerStateSource>();
    }

    protected override QaUnitReport OnUnitEnd()
    {
        DungeonState dungeon = dungeonSource.GetState();
        QaPlayerState player = playerSource.GetState();
        return new DungeonQaReport
        {
            evidence = new DungeonQaEvidence
            {
                seed = dungeon.seed,
                elapsed_seconds = player.elapsed_seconds,
                reached_exit = player.reached_exit,
                end_reason = dungeon.end_reason
            }
        };
    }
}
