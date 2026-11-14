# QA-56 another 3,000 mixed-room exit episodes

Status: complete. Exactly 3,000 additional training episodes saved as QA-56, bringing this branch to **7,000 mixed-task episodes**. Frozen mixed exits regress **121 -> 101/150**; original two-room exits change **50 -> 56/100**, an uncertain gain. QA-55 remains the stronger mixed-room checkpoint; QA-56 is retained separately. No additional learning or deployment.

Continue full QA-55 actor/critic/optimizer from SHA-256 `dc478e543bb4574692f2fe022663bd3382bcbee963f33699b199424aa81d9116`, 1,380,479 steps / 53,860 optimizer epochs / 8,700 lineage episodes. Same LR 0.000002 / gamma .99 / entropy .01 / rollout 256 / batch 64 / CPU and local-waypoint-v1 policy. No source, geometry, reward or navigator changes.

Exactly 3,000 additional episodes authorized, seeds 22800-25799; maximum 1,800,000 actions. Final totals are 7,000 mixed-task / 11,700 actor-lineage episodes. QA-51 is a separate fork excluded from this lineage. Same 1-3 connected regular/winding/middle-wall/sharp-corner/L rooms, one active waypoint <=10 units on assignment, actual final-room exit or sixty-second timeout, no room resets. Reward +50 actual exit / shared +5 positive waypoint cap / -0.001/action.

All fifteen direct controls pass every first-room-type/count combination; idle reaches no exit and earns zero positive shaping. Frozen QA-55 replay exactly reproduces all saved outcomes/action counts: 121/150 mixed and 50/100 original two-room exits. Matched child layouts/seeds and original 100 empty-room waypoint sequences are identical. Evaluation weights/counters stay fixed, with zero learning updates.

## Completed training and checkpoint

Training reaches **2,997/3,000 actual exits**, with three timeouts, in **527,115 actions**. One-room cases: 999/1,000; two-room: 1,000/1,000; three-room: 998/1,000. Every success traverses every generated room and enters the grounded final exit. Timeouts end in starting room, room one and final room once each. Generated rooms total 6,000: regular 1,201 / winding 1,208 / middle walls 1,191 / sharp corners 1,211 / L shape 1,189. Exploratory training success is separate from frozen-policy evaluation.

Final **1,907,594 steps / 74,450 optimizer epochs / 11,700 actor-lineage episodes**, adding 527,115 actions and 20,590 epochs. Exact stopping discards eleven partial-rollout transitions. All **106 archives** (105 periodic snapshots plus final) pass SHA-256 and ZIP CRC checks. Reloaded counters, lineage and LR/schedule/groups 0.000002 verified.

Final SHA-256: `c389391ac7b22484d93d779fb9aa8a86fadf7b8f911f5b9666162fae3f29a796`. [Final model](final_model.zip), [manifest](checkpoints.json), [training result](training-result.json), [completion](completion.json), [verification and paired analysis](verification-result.json).

## Matched frozen results

| Benchmark | QA-50 reference | QA-55 parent | QA-56 |
| --- | ---: | ---: | ---: |
| Mixed one-room exits | 47/50 | 48/50 | 47/50 |
| Mixed two-room exits | 41/50 | 39/50 | 32/50 |
| Mixed three-room exits | 29/50 | 34/50 | 22/50 |
| Mixed total | 117/150 | 121/150 | **101/150** |
| Original two-room exits | 57/100 | 50/100 | **56/100** |
| Empty-room waypoint arrivals/min | 45.41 | 45.54 | **45.49** |

Mixed completion falls 80.67% -> 67.33% (-13.33 percentage points), sixteen gained / 36 lost / 98 tied cases. Exploratory paired-bootstrap 95% change **-22.67..-4 points**, supporting regression on these reused cases. Original two-room completion changes 50% -> 56% (+6 points), nineteen gained / thirteen lost / 68 tied; interval **-5..+17 points**, so a reliable gain is not established (20,000 paired draws, seed 56). QA-55 remains the stronger mixed-room reference. QA-56 is close to QA-50's original two-room count (56 versus 57) but substantially below its mixed count (101 versus 117); newest does not imply best across tasks.

By first-room type (thirty mixed cases each), QA-55 -> QA-56 exits: regular 26 -> 22, winding 21 -> 21, middle walls 25 -> 21, sharp corners 24 -> 22, L shape 25 -> 15. Later rooms can contain other types; these descriptive groups do not isolate a shape-specific cause.

Mixed failures increase 29 -> 49: starting room 25 / corridor fourteen / room one three / final room seven. Capped mean attempt time worsens 23.929 -> 29.414 seconds. Missing-waypoint actions rise 11,317 -> 18,184, low-motion actions 2,922 -> 9,925, replans 966 -> 3,436, across 35,893 -> 44,121 total actions. Original two-room capped mean improves 35.864 -> 33.180 seconds; remaining failures end in starting room twenty / corridor 24, with missing-waypoint actions 12,217 -> 6,343 and low-motion actions 9,428 -> 3,534. Task-dependent changes do not isolate a causal mechanism. Empty-room mean changes only 45.54 -> 45.49; all 100 cases reach ten, minimum 38 / maximum 54.

## Sampling and verification

This run contains 2,742 distinct physical floorplans across 3,000 training draws. Cumulative QA-52/53/54/55/56 training contains 5,973 distinct floorplans across 7,000 draws. Evaluation has 150 unique floorplans; 21 overlap this run's training geometry and forty overlap cumulative training. Separate seeds do not make the reused evaluation geometry-disjoint. Source snapshots match QA-55 exactly; only continuation checkpoint, episode budget and fresh training seeds changed.

[Config](training-config.yaml), [run metadata](run.json), [source snapshots](source/), [parent mixed evaluation](parent-evaluation-result.json), [child mixed evaluation](child-evaluation-result.json). Each phase retains episodes, layouts and compressed action traces. Runtime logs are local-only under `Logs/qa-mixed-exit/20261111-080204-train/`.

Verified exact episode/seed budget, successful traversal/actual grounded exit, room/type coverage, identical before/after layouts and waypoint sequences, frozen weights/counters, all archive SHA/CRC, final reload, unchanged QA-50/55 and source hashes, restored settings, Python syntax and local documentation links. Unity stopped. Training history and checkpoint indexes reconciled. No additional training, commit, push or deployment.
