using System;
using Kcccr.QaFramework;
using UnityEngine;

[Serializable]
public struct DungeonState
{
    public int seed;
    public string end_reason;
}

/// <summary>Exposes the dungeon identity and game-owned ending reason for the current player attempt.</summary>
[DisallowMultipleComponent]
public sealed class DungeonStateSource : MonoBehaviour, IQaStateSource<DungeonState>
{
    private PcgDungeonManager dungeonManager;

    private void Awake()
    {
        dungeonManager = GetComponent<PcgDungeonManager>();
    }

    public DungeonState GetState()
    {
        return new DungeonState
        {
            seed = dungeonManager.ActiveSeed,
            end_reason = dungeonManager.PlayerEpisodeEndReason
        };
    }
}
