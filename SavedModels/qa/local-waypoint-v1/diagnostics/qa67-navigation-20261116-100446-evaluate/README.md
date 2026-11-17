# QA-67: observed navigation with shared capsule checks

Implemented and evaluated with frozen QA-50. **49/100 exits and 51/100 exit discoveries**, compared with 0/100 and 5/100 on the identical QA-66 layouts. Missing guidance changes from 82.08% to 44.73% of actions. Completion improves, while normalized recovery churn and observed evaluation throughput regress. These are reused diagnostic layouts, not independent generic-level validation.

## Implemented changes

1. Shared upright world-space controller capsule from active bounds, with a 0.05-world-unit normal safety margin. Planning, waypoint sweeps/overlaps and recovery use the same shape; check the actor position as well as its cell centre.
2. Live blocked-state and terminal snapshots: actual observed cells, nonmutating planner classifications, body bounds/endpoints, goal and excluded frontier points. No generator destination enters navigation.
3. Physically refine only inflated observed non-wall cells and their edges. Keep ordinary recorded wall cells and unknown space blocked. Recovery may leave the clear portion of its initial observed wall-hit voxel only after a full physical capsule sweep; other wall-hit voxels remain blocked. Defer an early intermediate turn handoff until a safe continuation exists or distance is at most 0.05.
4. Cache failed goal selection until pose, planner/map/body, exit knowledge, exclusions or eligibility changes. Ignore world-bound numerical jitter below 0.0001 units when invalidating shape geometry.
5. Direct Unity scenarios and the full matched frozen-policy campaign, with reference steering on failed cases. No learning, curriculum advancement, promotion or deployment.

## Matched 100-case results

Same seeds 530000-530099, two generated 20 x 20 rooms and actual corridors, 25 cases per quarter-turn, twelve-unit sensing, ten-unit waypoint cap, sixty-second/600-action deadline and no enemies/puzzles/loot. All generated floor/spawn/exit fingerprints match QA-66.

| Measurement | QA-66 baseline | QA-67 |
| --- | --- | --- |
| Exit completions | 0/100 | 49/100 |
| Exit discoveries | 5/100 | 51/100 |
| Actions without waypoint | 82.08% | 44.73% |
| Actions | 60000 | 40819 |
| Assignments | 1628 | 5601 |
| Assignments per 100 guided actions | 15.14 | 24.83 |
| Interrupted instructions | 418 | 2784 |
| Exact two-action cycles | 0 | 0 |
| Broader recovery return states | 60 | 3462 |
| Cases with stuck windows | 99 | 47 |
| Stuck windows | 1400 | 504 |
| Low-displacement actions (<0.02) | 32593 | 12165 |
| Whole action phase: actions per second | 24.48 | 7.77 |
| Whole action phase: seconds | 2451.3 | 5254.4 |

| Rotation (degrees) | Cases | Exits |
| --- | --- | --- |
| 0 | 25 | 17 |
| 90 | 25 | 8 |
| 180 | 25 | 15 |
| 270 | 25 | 9 |

Orientation counts are descriptive for this reused benchmark. They do not isolate the cause of directional differences.

There are 2761 coordinator arrival events. Median instruction lifetime is 0.200 simulated seconds. Assignment counts must be read alongside guided actions and completion; more available guidance can increase the raw count. Broader returning states and stuck windows are diagnostic counts, not distinct failure incidents.

Top instruction endings: {"escape_arrived_free": 1794, "start_cell_blocked": 1644, "waypoint_arrived": 967, "goal_cell_invalid": 454, "oscillation_detected": 242, "waypoint_segment_invalid": 213, "escape_invalid": 152, "episode_ended": 56}. All assignment versions are accounted for; see the full assignment audit.

## Controls and planning cost

Reference steering clears **42/51 policy failures**; 9 also fail under reference steering. Controls use the same observed-map navigator and matching geometry. A reference success establishes traversability by this stack with reference steering, but does not isolate movement weights from the different perception/recovery trajectory. All nine shared reference failures end with `no_reachable_goal` at a clear actor position. Offline terminal snapshots find zero eligible frontiers in the planner-free reachable cells; ignoring clearance leaves 12-34 eligible observed non-wall frontier cells. Physical edge sweeps are not reconstructed, so this does not establish that a safe route exists. See [blocked-map audit](reference-blocked-map-audit.json).

The four preflight controls are also retained. QA-66 baseline controls stopped at 77/100 and must remain labelled partial.

The whole action phase includes generation, physics, bridge transport, inference and recording. Timing comes from ready/episode file timestamps in separate Editor runs; it does not isolate a cause for the throughput regression. See [phase timing](action-phase-timing.json).

The coordinator records 416835.3 milliseconds total, averaging **10.212 ms per action**, including navigation-event callbacks. This is an average, not tail-latency validation or a whole-game performance guarantee. It skips 3974 unchanged failed searches and performs 679525 cell refinement checks; 92307 refined-cell results are cumulative across snapshots, not unique world cells. Whole campaign wall time, including Unity startups and controls, is 6465.4 seconds.

## Verification and limits

Five synthetic direct Unity scenarios pass **57 assertions**: scaled body dimensions, clear/blocked passages, an observed L-turn, safe recovery from a coarse initial wall voxel, and cache invalidation while upright rotation preserves cached failure. In-memory weights/counters, checkpoint file and all source hashes are unchanged throughout the final campaign. Original project settings are restored. QA-50 stays selected, with no training authorized.

Multiple navigator changes were tested together. The benchmark was inspected and reused during diagnosis; these results do not measure independent effects or prove generic-level reliability. Capsule construction assumes this game's upright controller. Refinement retains the 0.25-unit lattice; a passage with no usable cell-centre steering point can still be missed. This is a remaining representation limit, not a proven diagnosis of every shared failure. The prior QA-65 empty-room result belongs to its earlier source and is not a fresh regression result for this revision.

[Result](result.json), [campaign/source hashes](campaign.json), [navigation audit](navigation-audit.json), [assignment endings](navigation-assignment-audit.json), [exact cycles](two-action-loops.json), [direct checks](direct-verification/verification.json), [policy records](evaluation/episodes.jsonl), [baseline](../qa66-observed-dungeons-20261116-070841/README.md).

[Final integrity checks](integrity-checks.json).
