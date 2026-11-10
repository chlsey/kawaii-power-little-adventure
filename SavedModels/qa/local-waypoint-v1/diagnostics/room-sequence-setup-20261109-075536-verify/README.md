# Continuous room-sequence setup — verified, no training

**The endless-within-budget room sequence is ready and verified. No additional training episodes have run.** The earlier finite eight-room QA-48 experiment had already completed its 300 episodes when the user selected this alternative. The additional budget question remains pending: 300 new sequence episodes, or setup only. Use preserved QA-47 as the training parent; QA-48 regressed.

Each episode starts a physical chain of simple 20 x 20 rooms connected by ordinary generated corridors. Room centres advance 24 units along X; seeded lateral steps of -24/0/+24 add turns. The game generator prepares sixteen rooms ahead inside a 480 x 200 area. This is a finite prefix that cannot be exhausted within the episode: entering the sixteenth room requires at least **351 forward units**, while 600 actions at four units/s allow at most **240 commanded movement units**. No claim of an infinitely allocated scene is made. The clock therefore ends the exercise before the prepared chain can end.

The navigator advances to the next unvisited room in sequence. Reaching a room changes the instruction without resetting the player, previous action, sensors or map; there is no per-room teleport or dungeon regeneration. Waypoints remain visible, capsule-clear and at most ten units away when assigned. The starting room is excluded. A new room counts once after the actor moves at least one unit inside its footprint and into its vertical volume. Revisits earn nothing; the exit is disabled. Full 60-second episodes reward +10/new room, shared +5 positive waypoint shaping at most, and -0.001/action.

The same local-waypoint-v1 134 -> 64 -> 64 -> 2 policy and PPO settings are retained. The new `TrainingRoomSequence` parameter is opt-in and defaults false for normal PCG and existing exit tasks.

## Verification only

| Case | New rooms beyond spawn | Actions | Return |
| --- | --- | --- | --- |
| Direct steering, seed 456000 | 6 | 600 | 64.4 |
| Direct steering, seed 456001 | 5 | 600 | 54.4 |
| Direct steering, seed 456002 | 6 | 600 | 64.4 |
| Idle, seed 456003 | 0 | 600 | -0.6 |

All episodes last sixty seconds, no prefix is exhausted, room IDs are unique and spawn is excluded. Direct controls receive zero missing-waypoint actions; idle receives no room/progress reward. All model weights and counters stay fixed at QA-47: **416,793 steps / 16,250 updates / 4,300 lineage episodes**. This checks the environment, not trained room-sequence performance.

The legacy default two-room generation path was also replayed on seeds 451000-451002 using QA-47: all three outcomes and step counts exactly match the archived baseline. Settings were restored and Unity stopped. [Parent checkpoint](../../qa-two-room-20261109-070143-train/final_model.zip) SHA-256 remains `2eafa17b18b59271d93ac2d0a4a3f95240788296f5c8132d9f20b33740c7477d`.

## Reviewable proposal

The [300-episode config](training-config.yaml) proposes fresh training seeds 16600-16899 and matched parent/child sequence evaluation seeds 455000-455099. It starts from QA-47's full actor/critic/optimizer and would produce a separate child with 4,600 actor-lineage episodes, excluding QA-48's failed branch. **That learning run is not started or authorized by this verification record.**

Verification command from the game directory: `python -u -B Training/train_room_throughput.py --config Training/qa-player-room-sequence-300.yaml --mode verify`. The `train` mode would start the proposed additional 300 episodes only after the pending budget choice.

- [Run metadata: verified, actual training episodes zero](run.json), [preflight result](verification/result.json), [episode records](verification/episodes.jsonl), [room visits](verification/room-visits.jsonl), [full action traces](verification/actions.jsonl.gz).
- [Legacy replay](legacy-exit-smoke/episodes.jsonl), [legacy traces](legacy-exit-smoke/actions.jsonl.gz).
- [Runner snapshot](source/Training/train_room_throughput.py), [adapter snapshot](source/Assets/Editor/QaTwoRoomNavigationSession.cs), [generator snapshot](source/Assets/PCGDungeon/Scripts/Generation/Generator.cs).

Local logs/original settings: `Logs/qa-room-sequence/20261109-075536-verify/`.
