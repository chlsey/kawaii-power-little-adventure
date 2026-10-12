using System;

// Mirrors qa_framework's EpisodeMetrics fields, accumulated live by QaPlayerController.
[Serializable]
public class QaEpisodeMetrics
{
    public int StepCount;
    public float ElapsedSeconds;
    public bool Completed;
    public bool PlayerDied;
    public int EnemiesKilled;
    public float DamageTaken;
    public int PowerupsUsed;
    public int StuckEvents;
}
