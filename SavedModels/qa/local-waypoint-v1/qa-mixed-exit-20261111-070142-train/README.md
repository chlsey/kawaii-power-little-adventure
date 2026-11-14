# QA-55 another 2,000 mixed-room exit episodes

Status: complete. Exactly 2,000 additional training episodes saved as QA-55, bringing this branch to **4,000 mixed-task episodes**. Frozen mixed exits improve **107 -> 121/150**, original two-room exits **32 -> 50/100**. The paired results support improvement over QA-54 on these reused benchmarks. QA-50 remains a useful alternative: 117/150 mixed and 57/100 original exits. No additional training or deployment.

Continue full QA-54 actor/critic/optimizer from SHA-256 `4a5ba3ed4aca2c5763bd6145ea6ef2652629dfea8ba5125e2fbc5ee78c40336f`, 1,027,926 steps / 40,090 optimizer epochs / 6,700 actor-lineage episodes. Constant LR 0.000002, gamma .99, entropy .01, rollout 256 / batch 64, CPU. No source, environment, reward, navigation or policy changes.

User authorized exactly 2,000 additional training episodes, seeds 20800-22799, maximum 1,200,000 actions. Final branch totals are 4,000 mixed-task / 8,700 actor-lineage episodes; QA-51 is a separate fork excluded from this lineage. Same 1-3 connected rooms across regular, winding, middle walls, sharp corners and L shapes, actual final-room exit, one active waypoint <=10 units on assignment, sixty-second timeout and no room resets. Reward +50 actual exit, shared +5 positive waypoint cap, -0.001/action.

All fifteen direct controls pass every first-room-type/count combination; idle reaches no exit and earns zero positive shaping. Frozen parent replay exactly reproduces every QA-54 outcome/action count: 107/150 mixed and 32/100 original two-room exits. Child checks use identical layouts/seeds plus the original 100 empty-room waypoint sequences. All evaluation weights and counters stay fixed.

## Completed training and checkpoint

Training reaches **1,998/2,000 actual exits**, with two timeouts, in **352,553 actions**. All one-room cases (666/666), all two-room cases (667/667) and 665/667 three-room cases succeed. Every successful episode traverses every generated room and enters the actual grounded final exit. The two timeouts end in the exit room and a corridor. Generated rooms total 4,001: regular 790 / winding 809 / middle walls 789 / sharp corners 815 / L shape 798. Exploratory training success is separate from frozen-policy evaluation.

Final **1,380,479 model steps / 53,860 optimizer epochs / 8,700 actor-lineage episodes**, adding 352,553 actions and 13,770 epochs. Exact stopping discards 41 partial-rollout transitions. All **71 archives** (seventy periodic snapshots plus final) pass SHA-256 and ZIP CRC verification; model reload confirms counters, lineage and LR/schedule/groups 0.000002.

Final SHA-256: `dc478e543bb4574692f2fe022663bd3382bcbee963f33699b199424aa81d9116`. [Final model](final_model.zip), [manifest](checkpoints.json), [training result](training-result.json), [completion](completion.json), [verification and paired analysis](verification-result.json).

## Matched frozen results

| Benchmark | QA-50 reference | QA-54 parent | QA-55 |
| --- | ---: | ---: | ---: |
| Mixed one-room exits | 47/50 | 47/50 | 48/50 |
| Mixed two-room exits | 41/50 | 34/50 | 39/50 |
| Mixed three-room exits | 29/50 | 26/50 | 34/50 |
| Mixed total | 117/150 | 107/150 | **121/150** |
| Original two-room exits | 57/100 | 32/100 | **50/100** |
| Empty-room waypoint arrivals/min | 45.41 | 45.54 | **45.54** |

Mixed completion improves 71.33% -> 80.67% (+9.33 percentage points), twenty-five gained / eleven lost / 114 tied cases. Exploratory paired-bootstrap 95% change **+2..+17.33 points**. Original two-room completion improves 32% -> 50% (+18 points), twenty-four gained / six lost / seventy tied; interval **+8..+28 points** (20,000 paired draws, seed 55). Both comparisons support a gain over QA-54 on the reused cases; neither establishes generalization to unseen geometry or a specific causal mechanism. QA-55 exceeds QA-50's mixed count by four cases but remains seven below its original two-room count; it does not dominate QA-50 across tasks.

By first-room type (thirty mixed cases each), QA-54 -> QA-55 exits: regular 19 -> 26, winding 19 -> 21, middle walls 23 -> 25, sharp corners 23 -> 24, L shape 23 -> 25. Later rooms may have other shapes; these descriptive groups do not isolate shape-specific effects.

Mixed failures decrease 43 -> 29: eight end in starting room, nine in corridor, four in room one, eight in final room. Capped mean attempt time improves 27.397 -> 23.929 seconds; missing-waypoint actions fall 19,822 -> 11,317 and low-motion actions 12,728 -> 2,922, across 41,095 -> 35,893 total actions. Original two-room failures decrease 68 -> 50 (starting room seventeen / corridor 33); capped mean time improves 44.523 -> 35.864 seconds. Guidance gaps and movement failures remain. Empty-room mean stays 45.54; all 100 cases reach ten, minimum 38 / maximum 53.

## Sampling and verification

This run contains 1,879 distinct physical floorplans across 2,000 training draws. Cumulative QA-52/53/54/55 contains 3,570 distinct floorplans across 4,000 draws. Evaluation has 150 unique floorplans; 35 overlap this run's training geometry and 39 overlap cumulative training. Separate seeds do not make the reused evaluation geometry-disjoint. This campaign changes only checkpoint lineage and fresh training seeds; source snapshots match QA-54 exactly.

[Config](training-config.yaml), [run metadata](run.json), [source snapshots](source/), [parent mixed evaluation](parent-evaluation-result.json), [child mixed evaluation](child-evaluation-result.json). Each phase retains episodes, layouts and compressed action traces. Runtime logs are local-only under `Logs/qa-mixed-exit/20261111-070142-train/`.

Verified exact episode/seed budget, successful traversal/real grounded exit, room/type coverage, identical before/after layouts and original waypoint sequences, frozen weights/counters, model reload, all archive SHA/CRC, unchanged QA-50/54 and source hashes, restored settings, Python syntax and local documentation links. Unity stopped. Training history and checkpoint indexes reconciled. No additional training, commit, push or deployment.
