# QA-39 navigation to a goal 30-40 units away

`QaWaypoint50Training.unity` is an empty-room navigation evaluation scene derived from the 40 x 40 waypoint scene. It has a 50 x 50 floor and four enclosing walls, with no internal barriers or enemies. The extra space allows spawn and goal to remain 1.5 units inside the floor while supporting 40-unit separation in any direction; usable width is 47 units. The original 40 x 40 scene is unchanged.

The [frozen QA-39 recipe](../../Training/qa-player-waypoint-qa39-navigation-30to40.yaml) selects 100 seeds 287000-287099, initial spawn-to-exit distance 30-40 units, 12-unit vision, a 10-unit maximum local waypoint assignment and a 60-second / 600-action deadline. The exit starts unseen. Sensor memory, frontier selection, observed-space route planning, visibility/clearance checks, assignment handoffs and recovery are enabled. The distant goal is not fed directly to the local policy: it receives the normal assigned local instruction. Shorter local points remain valid for turns and final approach.

The exit trigger is this benchmark's completion marker. A local waypoint arrival alone never marks game completion. This is empty-room integrated navigation, not generated-dungeon, obstacle or combat validation. QA-39 is the model trained on standalone 1-10-unit targets; these evaluations perform no learning or automatic promotion.

From the game directory, with other Unity sessions closed:

```powershell
python -B Training/run_local_movement.py --config Training/qa-player-waypoint-qa39-navigation-30to40.yaml --mode verify
python -B Training/run_local_movement.py --config Training/qa-player-waypoint-qa39-navigation-30to40.yaml --mode evaluate --episodes 100
```

Verification checks placements, room size, assigned observation bounds, map/clearance/recovery/reward fixtures, reference compass clears and full reference navigation. Reference navigation uses the actual configured episode deadline. Reference controls are excluded from the 100 frozen policy cases.

The recipe pins exact QA-39 weights and hash; it does not automatically select a newer checkpoint. [Skill-model provenance](../../SavedModels/qa/local-waypoint-v1/qa-waypoint-reach-20261108-145257-train/README.md), [training/evaluation history](../../Training/changelog/qa-player.md).

[Recorded frozen result](../../SavedModels/qa/local-waypoint-v1/diagnostics/qa39-navigation-30to40-20261108-175349-evaluate/README.md): 67/100 goals reached, 33 timeouts (30 before discovery, three after), with weights unchanged.
