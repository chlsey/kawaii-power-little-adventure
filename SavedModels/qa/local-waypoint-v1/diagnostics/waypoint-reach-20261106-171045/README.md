# QA-25: one fixed waypoint in an empty room

**Passed: 1/1 arrival after 0.9 game seconds / 9 movement actions.** Initial distance 4.0 units; final distance 0.40 units, within the 0.5-unit acceptance radius. The attempt had a 60-second / 600-action maximum and stopped on arrival.

Frozen deterministic [QA-24 checkpoint](../../qa-waypoint-20261104-170721-train/README.md), `local-waypoint-v1` (134 inputs, two movement actions). SHA-256 `78548d2ca50e90f61b737e9f74a6bf983c7682ea96c2e431b94cf71810076c36`. No training: zero new updates, all policy tensors, model training-step counter and checkpoint bytes unchanged. Unity exited successfully; project settings restored.

The existing empty 40 x 40 scene supplies the real player, floor and boundary walls. The player starts at X/Z (0, 0); a fixed waypoint is four units northeast at (2.828427, 2.828427). Reset seed 260000 initializes the environment before fixed placement. Exit object and trigger are disabled; the waypoint marker has no collider or sensor label. All exit ray channels were checked as zero. The automatic coordinator is bypassed. The normal observation mapping supplies sensor rays and previous action; its last four features are set to the same presence, normalized X/Z direction and distance/ray-range encoding as `QaWaypointPolicyMapping`. The real controller applies predicted actions at 0.1 game seconds per action, with Unity physics between actions. Arrival means horizontal distance <= 0.5 units; it is not an actual level-exit trigger.

This single successful target does not establish performance across directions/distances, dependence on the waypoint input, recovery behavior, or full-level completion. There is no masked-input control in this test.

- [Setup](setup.json), [result](result.json), [full observations/actions/trajectory](trace.json), [checkpoint and source provenance](run.json).
- [Python runner](../../../../../Training/test_waypoint_reach.py), [Unity session](../../../../../Assets/Editor/QaWaypointReachSession.cs).
- Rerun from the game root with `python Training/test_waypoint_reach.py` (launches headless Unity and writes a new `Logs/qa-waypoint-reach/<UTC timestamp>/` directory).
- Detailed Unity logs and the exact executed source copies are local-only under `Logs/qa-waypoint-reach/20261106-171045/`. Maintained runner/session files gained explanatory flow comments after execution; the recorded hashes refer to the executed versions.
