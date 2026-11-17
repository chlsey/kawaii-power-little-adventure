# QA-58: another 1,000 mixed-room episodes

Status: **complete**, reconciled 2026-11-14 18:25 UTC. Training and all frozen evaluations finished. The additional episodes did not improve aggregate mixed-room completion; original two-room completion is lower. Empty-room movement is retained. No deployment or further learning.

## Training and checkpoint lineage

User-authorized full actor/critic/optimizer continuation from [QA-57](../qa-mixed-exit-20261111-084731-train/README.md), SHA-256 `5ed80b880d2cff680c2a4e104d309c08bfdd5c7178a4b37921f69dad9f2ffb80`, starting at 2,433,864 steps / 94,990 optimizer epochs / 14,700 completed lineage episodes. Unchanged local-waypoint-v1, LR 0.000002, gamma .99, entropy .01, rollout 256, batch 64, CPU. One to three connected rooms (regular, winding, middle walls, sharp corners, L shape), ten-unit waypoints, sixty-second deadline, actual final-room exit and no room resets. Reward +50 exit / shared +5 positive waypoint cap / -0.001 per action.

Exactly **1,000 unique completed seeds 28800-29799**, **997 exits / three timeouts (99.7%)**, **176,301 actions**; all three failures end in the exit room. Training one-/two-/three-room exits: 333/334, 331/333, 333/333. This exploratory training result is separate from deterministic frozen evaluation.

Final **2,610,165 model steps / 101,870 optimizer epochs / 15,700 completed lineage episodes**, including **11,000 mixed-task episodes**. Added 6,880 optimizer epochs. Exact stopping discards 173 pending rollout transitions. Final SHA-256 `ed6fddf1b411dacb345691a6faf95e976e46964ac3ed0e7d3996fae13b7b5763`. [Final model](final_model.zip), [manifest](checkpoints.json), [run config](training-config.yaml), [maintained config](../../../../Training/qa-player-mixed-exit-qa58-1000.yaml), [completion](completion.json).

## Matched frozen results

Weights remain fixed during evaluation. Parent replay reproduces QA-57. Mixed seeds 481000-481149 have identical parent/child layouts; original two-room seeds 451000-451099 and empty waypoint sequences 288000-288099 match their parent benchmarks.

| Benchmark | QA-57 parent | QA-58 |
| --- | ---: | ---: |
| Mixed one-room exits | 49/50 | 46/50 |
| Mixed two-room exits | 40/50 | 40/50 |
| Mixed three-room exits | 31/50 | 34/50 |
| Mixed total | 120/150 (80%) | 120/150 (80%) |
| Original two-room exits | 45/100 | 38/100 |
| Empty waypoint arrivals/min | 45.58 | 45.59 |
| Empty cases reaching ten | 100/100 | 100/100 |

Mixed outcomes change on 38 cases: **19 gained / 19 lost**. Exploratory paired-bootstrap 95% completion change **-8..+8 percentage points**, 20,000 draws, seed 58. Capped mean attempt time changes 23.995 -> 23.883 seconds; no demonstrated completion gain. Starting-room/corridor/intermediate-room/final-room failures change 10/8/5/7 -> 10/8/2/10. Missing-waypoint actions increase 9,556 -> 11,903, while replans decrease 2,482 -> 1,562; neither by itself establishes a causal mechanism.

Original two-room outcomes: **four gained / eleven lost**, seven percentage points lower. Exploratory paired-bootstrap interval **-15..0 points**; the observed decline does not establish a clear improvement or strong regression claim. Capped attempt time worsens **39.046 -> 41.820 seconds**. Of 62 failures, **50 end in corridors**, eleven in the starting room and one in the exit room. Low-motion actions increase 2,005 -> 7,683; missing-waypoint actions 11,878 -> 15,954. Successful-only mean is lower (13.436 -> 12.158 seconds), but fewer successes make that an insufficient efficiency comparison.

Empty-room waypoint retention is essentially unchanged: 45.58 -> 45.59 mean arrivals/minute; final range 38-53, all 100 cases reach ten.

## Interpretation and verification

QA-55 remains slightly ahead on recorded mixed completion (121/150) and original two-room completion (50/100). QA-50 retains the higher original two-room count (57/100), with 117/150 mixed. These aggregate counts do not establish paired superiority against QA-58. Preserve the alternatives; the latest checkpoint is not automatically best.

High exploratory training completion has not translated into improved frozen navigation. These are reused benchmarks; different seeds do not guarantee disjoint geometry. This is an oracle-guided local-navigation exercise, not evidence of unseen-exit discovery, combat, puzzle completion or full-game QA readiness. Investigate corridor/no-waypoint failures and evaluate fresh geometry before deciding on another training continuation. No new training is authorized by this report.

All 36 checkpoint hashes and ZIP CRC pass; exact training seed budget and actions verified, parent/source hashes unchanged, mixed layouts identical, settings restored and processes stopped. [Final verification and paired intervals](final-verification.json), [training result](training-result.json), [parent mixed](parent-evaluation-result.json), [child mixed](child-evaluation-result.json), [original two-room records](child-two-room/episodes.jsonl), [empty retention](empty-room-retention/result.json). Full metrics live in [completion](completion.json). Raw runtime logs remain local under `Logs/qa-mixed-exit/20261114-072247-train/`.
