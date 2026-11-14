# QA-53 another 500 mixed-room exit episodes

Status: complete. Exactly 500 additional episodes saved as QA-53; 1,000 total mixed-task episodes in this branch. Frozen changes versus QA-52 are small and uncertain; QA-50 remains stronger on the tested navigation benchmarks. No further training or deployment.

Continue the latest saved QA-52, SHA-256 `85acc6ac355b86a3916dd85f60b12139bd4408e78bdfd99e12ea03a0698aaac0`, at 751,864 steps / 29,320 optimizer epochs / 5,200 actor-lineage episodes. Retain full actor, critic and optimizer at constant LR 0.000002, gamma .99, entropy .01, rollout 256 / batch 64. QA-50 and QA-52 remain unchanged.

User authorized exactly 500 additional episodes, fresh seeds 19300-19799. This brings the QA-50 -> QA-52 -> QA-53 mixed-task branch to 1,000 mixed training episodes and 5,700 actor-lineage episodes if complete. QA-51 is excluded. Room generation, navigation, perception/actions, rewards and stopping rules are unchanged from QA-52: 1-3 connected rooms drawn across regular, winding, middle walls, sharp corners and L shapes; one active waypoint <=10 units on assignment; real final-room exit or sixty-second timeout; no per-room reset. Reward +50 actual exit, shared +5 waypoint progress/arrival cap, -0.001/action.

Repeat fifteen direct controls plus idle. Frozen parent replay must exactly reproduce QA-52 on the 150 mixed cases 481000-481149 and 100 legacy two-room cases 451000-451099. Evaluate the child on those same cases plus original empty-room waypoint sequences 288000-288099. Evaluation makes zero learning updates. Preserve periodic snapshots every 5,000 actions and a final model after exactly 500 episodes; no automatic further run or deployment.

Parent replay exactly reproduces QA-52's 111/150 mixed exits and 30/100 legacy exits, including every case's outcome and step count. Direct controls pass all fifteen room-type/count combinations; idle reaches no exit. These checks make zero learning updates.

## Completed budget and checkpoint

All **500/500 training episodes reach the actual final-room exit**, after traversing every generated room, in 91,850 actions. Room counts: 166 one-room, 167 two-room and 167 three-room episodes. Generated rooms total 1,001: regular 209 / winding 184 / middle walls 207 / sharp corners 197 / L shape 204. These exploratory training outcomes use different seeds and action selection from deterministic evaluation; they do not demonstrate a frozen-policy gain.

Final counters: **843,714 model steps / 32,900 optimizer epochs / 5,700 actor-lineage episodes**, adding 91,850 actions and 3,580 epochs to QA-52. The exact 500-episode stop discards 202 partial-rollout transitions. Full actor/critic/optimizer continuation and LR/schedule/groups 0.000002 are verified in the reloaded final model. Eighteen periodic snapshots plus final model are retained; all nineteen archives pass SHA-256 and ZIP CRC checks.

Final SHA-256: `9f9a2d0754ad86e271187fb74373aabf641ee67334f2d115ec8d6473717fec22`. [Final model](final_model.zip), [manifest](checkpoints.json), [training result](training-result.json), [completion](completion.json), [verification and paired analysis](verification-result.json).

## Matched frozen results

Every parent/child evaluation uses identical physical layouts, goals and seeds, with zero learning updates.

| Benchmark | QA-50 reference | QA-52 parent | QA-53 |
| --- | ---: | ---: | ---: |
| Mixed one-room exits | 47/50 | 46/50 | 46/50 |
| Mixed two-room exits | 41/50 | 36/50 | 35/50 |
| Mixed three-room exits | 29/50 | 29/50 | 28/50 |
| Mixed total | 117/150 | 111/150 | **109/150** |
| Original two-room exits | 57/100 | 30/100 | **35/100** |
| Empty-room waypoint arrivals/min | 45.41 | 45.36 | **45.38** |

Mixed completion changes 74% -> 72.67%, seventeen gains / nineteen losses / 114 ties; exploratory paired-bootstrap 95% change **-9.33..+6.67 percentage points** (20,000 draws, seed 53). Original two-room completion changes 30% -> 35%, twelve gains / seven losses / 81 ties; paired-bootstrap 95% **-4..+13 points**. Neither difference establishes a clear improvement. QA-50 remains stronger on both benchmarks.

By first-room type (30 mixed cases each), QA-52 -> QA-53 exits: regular 19 -> 22, winding 21 -> 17, middle walls 23 -> 20, sharp corners 22 -> 28, L shape 26 -> 22. Later rooms may contain other types; these descriptive groups do not isolate a shape-specific training effect.

Mixed final failures total 41: room zero fifteen / room one three / corridor nine / final room fourteen. The original two-room failures end in starting room 23 / corridor 42. Original mean attempt time including timeouts improves 45.502 -> 43.244 seconds, but missing-waypoint actions remain 16,647/43,244 and replans rise 6,266 -> 8,627. Empty-room ten-target completion stays 100/100, minimum 38. Stable waypoint drills and high exploratory training success do not establish reliable integrated navigation.

## Sampling and limits

Fresh training seeds contain 492 distinct physical floorplans in 500 draws. The cumulative QA-52 + QA-53 training set has 968 distinct floorplans across 1,000 draws. The 150 evaluation layouts are unique; six overlap this run's training geometry and seventeen overlap the cumulative training geometry. Seeds are separate, but evaluation is not completely geometry-disjoint. These are reused matched benchmarks, not a new unseen-dungeon validation.

This campaign changes only checkpoint lineage and fresh training seeds. It does not test a new reward, navigator, policy architecture or learning rate. The parent replay reproduces every retained QA-52 mixed and original two-room outcome/step count. Preserve QA-50/52 and the new QA-53 evidence without automatic replacement or extra training.

## Evidence and verification

[Config](training-config.yaml), [run metadata](run.json), [source snapshots](source/), [parent mixed evaluation](parent-evaluation-result.json), [child mixed evaluation](child-evaluation-result.json). Every phase retains episodes, layout records and compressed action traces. Runtime logs are local-only under `Logs/qa-mixed-exit/20261110-165359-train/`.

Verified exact 500-seed/episode budget, actual final-room exit and full-room traversal for every training success, room/type coverage, identical before/after layouts, frozen evaluation weights/counters, final reload/LR, all archive SHA/CRC checks, unchanged QA-50/52/source checksums, restored project settings, Python syntax and local documentation links. Unity stopped. No commit, push, deployment or additional training beyond these 500 episodes was performed.
