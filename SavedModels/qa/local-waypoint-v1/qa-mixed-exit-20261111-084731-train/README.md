# QA-57 another 3,000 mixed-room exit episodes

Status: complete after checkpoint recovery. Exactly **3,000 completed episodes**, **2,993 exits / seven timeouts**. Mixed navigation recovers versus QA-56, while original two-room navigation regresses. QA-55 remains slightly ahead on both benchmark counts; retain all alternatives. No additional learning or deployment.

Continue full QA-56 actor/critic/optimizer from SHA-256 `c389391ac7b22484d93d779fb9aa8a86fadf7b8f911f5b9666162fae3f29a796`, 1,907,594 steps / 74,450 optimizer epochs / 11,700 completed lineage episodes. Same LR/schedule/groups 0.000002, gamma .99, entropy .01, rollout 256 / batch 64 / CPU and local-waypoint-v1 policy. Fresh seeds 25800-28799, exactly 3,000 completed episodes / maximum 1,800,000 actions. Same 1-3 connected regular/winding/middle-wall/sharp-corner/L rooms; one active waypoint <=10 units on assignment; real final-room exit or sixty-second timeout, no room reset; +50 exit / shared +5 positive waypoint cap / -0.001/action. Original environment and runner sources match QA-56; recovery orchestration was added separately.

## Completed budget and recovery

Training room counts: 999/1,000 one-room, 997/1,000 two-room and 997/1,000 three-room exits. Every success traverses all generated rooms and enters the actual grounded final exit. Six thousand generated rooms: regular 1,201 / winding 1,205 / middle walls 1,188 / sharp corners 1,214 / L shape 1,192. Failures end in starting room once / corridor once / room one once / final room four times. Exploratory training outcomes are separate from frozen evaluation.

Unity disconnected after 1,651 completed episodes; cause remains unknown. Preserve the interrupted records and resume exactly 1,349 remaining episodes from the latest full actor/critic/optimizer checkpoint, `checkpoints/rl_model_2197594_steps.zip`, SHA-256 `1d58c5f4d4b76b0c4d57ad8844678a518a3c6944580ce097c8d0a275ee2e8fcd`. It has 2,197,594 steps / 85,770 optimizer epochs / 13,325 stored lineage episodes. Resume seed 27451, the first unfinished episode. Twenty-six completed episodes after the snapshot remain in the original budget and are not repeated, although their latest learning progress was lost. Thirty recorded actions from the interrupted partial episode are additional to the completed-episode totals.

Completed episodes use **530,521 actions**; including the partial attempt there are **530,551 recorded actions**. Final model counters retain **526,270 actions** beyond QA-56: 290,000 before checkpoint recovery plus 236,270 afterward. Thus 4,281 recorded post-checkpoint actions are not retained, including 4,251 counted completed-episode actions. The checkpoint's pending 208 rollout transitions are discarded on resume; the exact final stop discards 238 more. Final completed-budget lineage is 14,700, including the 26 completed episodes whose latest learning progress was lost. This brings the branch to **10,000 mixed-task episodes**; QA-51 remains an excluded alternate fork.

Final **2,433,864 model steps / 94,990 optimizer epochs**, adding 20,540 retained epochs. All **106 archives** (58 initial periodic snapshots, 47 recovery snapshots and final) pass SHA-256 and ZIP CRC; reloaded counters, lineage and LR verified. Final SHA-256 `5ed80b880d2cff680c2a4e104d309c08bfdd5c7178a4b37921f69dad9f2ffb80`. [Final model](final_model.zip), [manifest](checkpoints.json), [completion](completion.json), [recovery accounting](recovery.json), [verification and paired analysis](verification-result.json).

Trace retention initially failed because the recovery segment parent directory was missing. Creating that directory and a finalize-only path resumed retention/evaluation from the saved final model with zero further learning. Preserve the [training recovery source](source/Training/recover_mixed_room_exit_qa57.training.py) and [corrected finalization source](source/Training/recover_mixed_room_exit_qa57.py); original failure records remain historical evidence. Both failures are reconciled into this one campaign.

## Matched frozen results

All fifteen direct controls pass; idle has no exit or positive shaping. Parent replay exactly reproduces every QA-56 mixed and original two-room outcome/action count. Matched child layouts/seeds and original empty-room waypoint sequences are identical. Evaluation weights/counters remain fixed.

| Benchmark | QA-55 reference | QA-56 parent | QA-57 |
| --- | ---: | ---: | ---: |
| Mixed one-room exits | 48/50 | 47/50 | 49/50 |
| Mixed two-room exits | 39/50 | 32/50 | 40/50 |
| Mixed three-room exits | 34/50 | 22/50 | 31/50 |
| Mixed total | 121/150 | 101/150 | **120/150** |
| Original two-room exits | 50/100 | 56/100 | **45/100** |
| Empty waypoint arrivals/min | 45.54 | 45.49 | **45.58** |

Mixed completion improves 67.33% -> 80% (+12.67 percentage points), 26 gained / seven lost / 117 tied cases. Exploratory paired-bootstrap 95% change **+5.33..+20 points**. Original two-room falls 56% -> 45% (-11 points), three gained / fourteen lost / 83 tied; interval **-19..-3 points** (20,000 paired draws, seed 57). Both support the task-dependent changes on these reused cases. QA-55 remains slightly ahead on both aggregate counts; no paired superiority claim against QA-55 is made. QA-50 retains 117/150 mixed and 57/100 original exits. The recovered continuation does not establish a new causal mechanism or unseen-dungeon generalization.

Mixed failures decrease 49 -> 30: starting room ten / corridor eight / room one five / final room seven. Capped mean attempt time improves 29.414 -> 23.995 seconds; missing-waypoint actions 18,184 -> 9,556, low-motion actions 9,925 -> 5,063, replans 3,436 -> 2,482. Original two-room failures increase 44 -> 55: starting room twelve / corridor 43. Capped mean worsens 33.180 -> 39.046 seconds and missing-waypoint actions rise 6,343 -> 11,878, despite low-motion actions falling 3,534 -> 2,005. Waypoint mean changes 45.49 -> 45.58; all 100 reach ten targets, minimum 38 / maximum 53.

## Evidence and limits

Training contains 2,725 unique floorplans in 3,000 draws. Cumulative QA-52 through QA-57 contains 8,249 unique floorplans across 10,000 draws. All 150 evaluation floorplans are unique; 32 overlap current training geometry and 45 overlap cumulative training. Separate seeds do not make this reused benchmark geometry-disjoint.

[Config](training-config.yaml), [metadata](run.json), [training aggregate](training/), [interrupted segment](segments/training/), [recovery segment](segments/training-recovery-01/), [training result](training-result.json), [parent mixed evaluation](parent-evaluation-result.json), [child mixed evaluation](child-evaluation-result.json), [source snapshots](source/). Raw segments retain compressed action traces; the aggregate contains each completed seed once and excludes the unfinished attempt. Runtime logs are local-only under `Logs/qa-mixed-exit/20261111-084731-train/`.

Verified exact seed budget and segment aggregation, all successful room traversals/actual exits, frozen comparisons and identical layouts/waypoint sequences, final reload, all archive hashes/CRC, preserved QA-50/56 and original sources, recovery source provenance, restored settings, Python syntax and local links. Unity stopped; history and checkpoint indexes reconciled. No additional training, commit, push or deployment.
