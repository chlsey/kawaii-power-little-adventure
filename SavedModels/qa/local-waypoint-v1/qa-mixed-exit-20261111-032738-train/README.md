# QA-54 another 1,000 mixed-room exit episodes

Status: complete. Exactly 1,000 additional episodes saved as QA-54, bringing this branch to 2,000 mixed-task episodes. Frozen mixed exits change **109 -> 107/150** and original two-room exits **35 -> 32/100**: small, uncertain declines and no demonstrated improvement. QA-50 remains stronger on both exit benchmarks. No further training or deployment.

Continue saved QA-53, SHA-256 `9f9a2d0754ad86e271187fb74373aabf641ee67334f2d115ec8d6473717fec22`, 843,714 steps / 32,900 optimizer epochs / 5,700 actor-lineage episodes. Full actor/critic/optimizer continuation at unchanged LR 0.000002, gamma .99, entropy .01, rollout 256 and batch 64. Earlier checkpoints remain preserved.

User authorized exactly 1,000 additional episodes, fresh seeds 19800-20799. Room generation, policy contract, navigation, rewards and stopping rules remain unchanged: regular/winding/middle-wall/sharp-corner/L shapes, 1-3 connected rooms, one active waypoint <=10 units on assignment, actual exit in the final room, sixty-second timeout, no per-room reset. Rewards are +50 actual exit, shared +5 positive waypoint cap, -0.001/action. The configured 600,000 actions are a maximum; actual actions are measured because exit completion ends episodes early. The runner now reads the explicit episode budget instead of hardcoding 500. QA-51 is a separate fork excluded from this lineage.

All fifteen direct controls pass the five first-room types across 1-3 rooms; idle reaches no exit and earns no positive waypoint reward. Frozen parent replay exactly reproduces every retained QA-53 mixed and original two-room outcome and step count. Parent/child evaluation uses identical layouts and seeds with zero learning updates.

## Completed budget and checkpoint

Training reaches **997/1,000 actual exits**, using **184,212 actions**. One-room cases: 334/334; two-room: 332/333; three-room: 331/333. Every success traverses every generated room and enters the real final exit. Three timeouts end in a corridor once and the exit room twice. Generated rooms total 1,999: regular 392 / winding 419 / middle walls 393 / sharp corners 386 / L shape 409. Exploratory training outcomes use different action selection and seeds from deterministic evaluation and do not establish a frozen-policy gain.

Final counters: **1,027,926 steps / 40,090 optimizer epochs / 6,700 actor-lineage episodes**, adding 184,212 actions and 7,190 epochs. The exact stop discards 148 partial-rollout transitions. Thirty-six periodic snapshots plus final model are retained; all **37 archives** pass SHA-256 and ZIP CRC checks. Reloaded counters, lineage and LR/schedule/groups are verified.

Final SHA-256: `4a5ba3ed4aca2c5763bd6145ea6ef2652629dfea8ba5125e2fbc5ee78c40336f`. [Final model](final_model.zip), [manifest](checkpoints.json), [training result](training-result.json), [completion](completion.json), [verification and paired analysis](verification-result.json).

## Matched frozen results

| Benchmark | QA-50 reference | QA-53 parent | QA-54 |
| --- | ---: | ---: | ---: |
| Mixed one-room exits | 47/50 | 46/50 | 47/50 |
| Mixed two-room exits | 41/50 | 35/50 | 34/50 |
| Mixed three-room exits | 29/50 | 28/50 | 26/50 |
| Mixed total | 117/150 | 109/150 | **107/150** |
| Original two-room exits | 57/100 | 35/100 | **32/100** |
| Empty-room waypoint arrivals/min | 45.41 | 45.38 | **45.54** |

Mixed completion changes 72.67% -> 71.33%, seventeen gained / nineteen lost / 114 tied cases. Exploratory paired-bootstrap 95% change is **-9.33..+6.67 percentage points**. Original two-room completion changes 35% -> 32%, eight gained / eleven lost / 81 tied; interval **-11..+6 points** (20,000 paired draws, seed 54). Neither difference establishes an improvement or a clear decline. QA-50 remains stronger at 117/150 mixed and 57/100 original exits.

By first-room type (thirty mixed cases each), QA-53 -> QA-54 exits: regular 22 -> 19, winding 17 -> 19, middle walls 20 -> 23, sharp corners 28 -> 23, L shape 22 -> 23. Later rooms may contain other shapes; these groups do not isolate a shape-specific effect.

Mixed failures end in starting room fourteen / corridor sixteen / room one four / final room nine. Missing-waypoint actions rise 15,022 -> 19,822 and low-motion actions 5,533 -> 12,728, despite similar total actions (41,223 -> 41,095). Original two-room failures end in starting room 23 / corridor 45, and capped mean attempt time changes 43.244 -> 44.523 seconds. These aggregates show continuing guidance and movement failures without isolating their cause. Empty-room waypoint sequences match the parent: all 100 reach ten targets, minimum 38 / maximum 53.

## Sampling and verification

Training contains 966 distinct physical floorplans in 1,000 draws. Cumulative QA-52/53/54 training contains 1,874 distinct floorplans across 2,000 draws. All 150 evaluation floorplans are unique; twelve overlap this run's training geometry and 22 overlap cumulative training geometry. Separate seeds do not make this evaluation completely geometry-disjoint. The matched benchmark is reused, not a new unseen-dungeon validation.

[Config](training-config.yaml), [run metadata](run.json), [source snapshots](source/), [parent mixed evaluation](parent-evaluation-result.json), [child mixed evaluation](child-evaluation-result.json). Each phase retains episodes, layouts and compressed action traces. Runtime logs are local-only under `Logs/qa-mixed-exit/20261111-032738-train/`.

Verified exact episode/seed budget, full-room traversal and grounded actual exit for every success, room/type coverage, identical evaluation layouts/waypoint sequences, frozen weights/counters, all archive hashes/CRC, final model reload, unchanged QA-50/53 and source hashes, restored project settings, Python syntax and local documentation links. Unity stopped. Training history and checkpoint indexes updated. No additional training, commit, push or deployment.
