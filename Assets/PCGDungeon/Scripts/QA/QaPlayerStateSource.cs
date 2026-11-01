using System;
using Kcccr.QaFramework;
using UnityEngine;

[Serializable]
public struct QaPlayerState
{
    public float elapsed_seconds;
    public bool reached_exit;
}

/// <summary>Exposes the player's current exit outcome and simulated action time to unit-end aggregation.</summary>
[DisallowMultipleComponent]
public sealed class QaPlayerStateSource : MonoBehaviour, IQaStateSource<QaPlayerState>
{
    private QaPlayerController player;

    private void Awake()
    {
        player = GetComponent<QaPlayerController>();
    }

    public QaPlayerState GetState()
    {
        QaEpisodeMetrics metrics = player.Metrics;
        return new QaPlayerState
        {
            elapsed_seconds = metrics.ElapsedSeconds,
            reached_exit = metrics.Completed
        };
    }
}
