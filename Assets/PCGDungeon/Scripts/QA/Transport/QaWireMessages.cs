using System;

// JsonUtility-serializable wire DTOs for the QaTcpHost <-> qa_framework
// protocol. Field names intentionally match the Python side's to_dict()/
// from_dict() keys exactly (snake_case, not C# convention) so most payloads
// can be decoded there without any field-renaming translation layer.
//
// JsonUtility cannot deserialize open-ended dictionaries or nullable value
// types, so optional data uses an explicit "_provided"/"_acknowledged" flag
// next to a concrete, always-present field instead of JSON null.

[Serializable]
public class QaWireCommandEnvelope
{
    public string cmd;
}

[Serializable]
public class QaWireGenerationParameters
{
    public int RoomCount;
    public int AreaWidth;
    public int AreaHeight;
    public int MinRoomWidth;
    public int MaxRoomWidth;
    public int MinRoomHeight;
    public int MaxRoomHeight;
    public int Seed;
}

[Serializable]
public class QaWireResetRequest
{
    public string cmd;
    public bool seed_provided;
    public int seed;
    public int max_steps;
    public bool max_duration_provided;
    public float max_duration_seconds;
    public bool level_provided;
    public string level_id;
    public int generation_seed;
    public bool generator_version_provided;
    public string generator_version;
    public QaWireGenerationParameters generation_parameters;
}

[Serializable]
public class QaWireAction
{
    public float move_x;
    public float move_y;
    public bool attack;
    public bool interact;
    public int ability;
}

[Serializable]
public class QaWireStepRequest
{
    public string cmd;
    public QaWireAction action;
}

[Serializable]
public class QaWireRay
{
    public bool hit;
    public float distance_01;
    public string object_type;
}

[Serializable]
public class QaWireObservation
{
    public int schema_version;
    public int ray_count;
    public float health_01;
    public int powerup_count;
    public QaWireAction previous_action;
    public QaWireRay[] rays;
}

[Serializable]
public class QaWireMetrics
{
    public int step_count;
    public float elapsed_seconds;
    public bool completed;
    public bool player_died;
    public int enemies_killed;
    public float damage_taken;
    public int powerups_used;
    public int stuck_events;
}

[Serializable]
public class QaWireEpisodeResult
{
    public bool terminated;
    public bool truncated;
    public string terminal_reason;
    public QaWireMetrics metrics;
}

[Serializable]
public class QaWireInfo
{
    public int schema_version;
    public int step;
    public string episode_id;
    public bool level_acknowledged;
    public string level_id;
    public int generation_seed;
    public bool has_episode_result;
    public QaWireEpisodeResult episode_result;
}

[Serializable]
public class QaWireResetReply
{
    public bool ok = true;
    public QaWireObservation observation;
    public QaWireInfo info;
}

[Serializable]
public class QaWireStepReply
{
    public bool ok = true;
    public QaWireObservation observation;
    public float reward;
    public bool terminated;
    public bool truncated;
    public QaWireInfo info;
}

[Serializable]
public class QaWireErrorReply
{
    public bool ok = false;
    public string error;
}

[Serializable]
public class QaWireAck
{
    public bool ok = true;
}
