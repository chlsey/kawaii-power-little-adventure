# QA-45: frozen QA-44 in simple two-room dungeons

**QA-44 reached the actual exit in 48/100 layouts. A direction-following reference using the same waypoint heuristic reached 100/100.** No training, checkpoint modification, model promotion or deployment occurred.

| Controller | Actual exits | Timeouts | Mean duration, all attempts | Mean duration, successful attempts |
| --- | --- | --- | --- | --- |
| Frozen QA-44 | 48/100 | 52 | 38.576 s | 15.367 s over 48 cases |
| Direction-following reference | 100/100 | 0 | 10.145 s | 10.145 s over 100 cases |
| Idle control | 0/1 | 1 | 60 s | None |

On the same 48 cases that QA-44 clears, reference mean duration is 9.750 s. Success times span 6.2-48.2 s for QA-44. Failed cases always use the full 600 actions / 60 simulated seconds. These are simulation times, not wall-clock timings.

## What was tested

- **100 distinct generated layouts**, seeds 450000-450099; each contains exactly two empty 20 x 20 rooms, generated corridors/walls and open doors, inside the generator's 100 x 100 placement area. No enemies, bosses, loot, puzzles or decorative obstacles. Normal game settings are cloned at runtime and remain unchanged.
- The real `PcgDungeonManager.StartPlayerEpisode(parameters)` calls the normal generator/validator and initializes the player/sensors. Explicit parameters bypass the PCG model's minimum room count. The manager retains actual exit callbacks and attempt lifecycle; external RL-bridge stepping supplies the frozen Python policy, while the manager's local random/manual loop is disabled.
- Same game player prefab, base speed four units/s, 0.1-second actions. Spawn feet are lifted 0.11 units above floor origin to match the generated floor top. Completion requires the actual `QaExitZone` trigger; waypoint arrival does not complete a dungeon. Interaction is disabled as an exit requirement.
- Initial straight-line exit distance: mean 33.332 units, range 21.000-54.672. Generator footprints and all initial routes exactly match the reference in every case.
- QA-44 remains `local-waypoint-v1`, 134 inputs / two world-axis movement outputs. Inputs contain previous movement, current 64 wall/exit rays and the assigned waypoint. All 38,576 learned-policy actions and observations are retained.

## Waypoint heuristic

This is a **test-only navigator with full generated-layout and exit knowledge**, not the ordinary observed-map frontier/exploration navigator. Four-neighbour A* uses integer floor-tile centres and Manhattan distance to the known exit. It chooses the furthest usable point along that route within ten units, checks the swept world-scaled player capsule against walls and verifies floor coverage along the segment. Arrival tolerance is 0.5 units; twenty actions without 0.05 units of new closest approach trigger replanning. Shorter waypoints are allowed at corners and on final approach; recorded assignment distances span 0.556-10.000 units, none exceed ten.

The geometry/exit map only controls waypoint selection. It is not inserted into the model's observations. The reference receives the same local waypoint and moves directly along its normalized direction. All 100 reference cases complete, with zero replans, zero actions lacking a waypoint and zero low-motion actions. Initial floor lists, routes, spawn and exit positions match the learned-policy layouts exactly. Reference success establishes that these layouts can be traversed using this heuristic; it does not prove recovery from every off-route position the learned actor reaches.

## Where QA-44 failed

| Final location among 52 failures | Cases |
| --- | --- |
| Starting room | 32 |
| Corridor | 19 |
| Exit room | 1 |

- **23/52 failures reach no waypoint at all.** Seed 450000 moves 211.95 units in the starting room, never approaches its current waypoint closer than about six units, and times out. Several failures show circling while still moving; others become pinned near walls.
- **16/52 failures spend at least half of their final 100 actions moving less than 0.02 units per action.** This measures near-immobility, not the controller's windowed stuck-event counter.
- **12/52 failures have some actions without a usable waypoint; nine spend at least half of the final 100 actions without one.** Drift into wall-adjacent/off-route positions can defeat this test heuristic's subsequent route/clearance checks. Missing guidance and learned steering therefore interact during recovery.
- Six failures issue away/perpendicular commands for at least half their guided actions in the final 100 actions, with at least 50 guided actions available. These categories overlap; they are descriptive trace summaries, not mutually exclusive proven causes.
- Only one failure enters the exit room: seed 450076 ends 12.14 units from the exit, after 499 no-waypoint actions and 488 near-immobile actions. It does not reach the final exit approach and then decline to leave.
- Across all learned episodes: 7,726/38,576 low-motion actions, 4,831 without a waypoint, 8,817 away/perpendicular guided commands and 4,761 replans. Repeated waypoint arrivals/assignments during oscillation are not additional exit successes or unique progress; do not treat the 3,321 arrival events as a completion score.

**Interpretation:** strong continuous target following in a spacious empty room did not transfer reliably to this smaller-room/corridor distribution. The known route works under direct steering, while learned movement and recovery from its drift remain weak. This experiment does not isolate wall inputs, movement history, room size, corridor width or waypoint replacement as the single cause. A useful next training stage would target doorway approaches, corridor turns and recovery using these real sensors/colliders, followed by a separate frozen held-out evaluation; no such training was started.

## Integrity and evidence

QA-44 [checkpoint](../../qa-waypoint-throughput-20261108-233001-train/final_model.zip) SHA-256 remains `1094cd0cea9a3fe2bc183b5bd3a7c1dc2ea6d22f4d5d5939629d7ed7ccafde8e`. Policy tensors match their pre-run copies exactly; model counter remains 328,433 and optimizer updates 12,810. Evaluation reward is zero. All Unity sessions exited successfully after the corrected setup, and project settings were restored. This is evaluation QA-45, not a new QA-45 policy checkpoint.

- [Run settings and source hashes](run.json), [combined results](result.json), [failure summaries](failure-analysis.json).
- [Learned episodes](evaluation/episodes.jsonl), [layouts and initial routes](evaluation/layouts.jsonl), [compressed full action traces](evaluation/actions.jsonl.gz).
- [Matched reference episodes](matched-reference/episodes.jsonl), [reference action traces](matched-reference/actions.jsonl.gz), [idle control](idle/episodes.jsonl).
- [Exact runner snapshot](source/Training/evaluate_two_room_navigation.py), [Unity environment/heuristic snapshot](source/Assets/Editor/QaTwoRoomNavigationSession.cs), [dungeon-manager snapshot](source/Assets/PCGDungeon/Scripts/Generation/PcgDungeonManager.cs).

Run from the game directory: `python -u -B Training/evaluate_two_room_navigation.py --mode evaluate`. It runs the three-case reference preflight, idle control and exactly 100 frozen model episodes. The additional full matched reference used the same runner's `phase(runtime, 'matched-reference', range(450000, 450100))` function with no model argument; its default controller follows waypoint direction. No setup changes occurred between learned and matched-reference phases.

Local runtime/logs: `Logs/qa-two-room-navigation/20261109-044803-evaluate/`. An earlier setup preflight at `20261109-044537-evaluate` was rejected before any model episodes: the quarter-unit grid/unscaled clearance provided no corridor route, and the generator could not fit seed 450001 in its restricted 80-unit placement area. The corrected test uses integer tile centres, actual scaled capsule clearance and a 100-unit area; no generator/actor mechanics were changed to obtain the recorded 48/100 result.
