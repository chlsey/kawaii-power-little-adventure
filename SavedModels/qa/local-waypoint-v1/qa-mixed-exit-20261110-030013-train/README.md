# QA-52 mixed 1-3-room exit curriculum

Status: complete; exactly 500 authorized training episodes saved. No clear gain on the new mixed task and a substantial loss on the original two-room benchmark. Retain QA-52 as an alternative; QA-50 is unchanged and preferred. No further training or deployment.

Continue preserved QA-50 (SHA-256 `acffd0594bb6a03a157e2853688dfc4d7647606d052cb241fe620d9f8eb4db64`), 656,793 steps / 25,610 optimizer epochs / 4,700 lineage episodes. Retain actor, critic and optimizer moments; reduce LR/schedule/groups tenfold, 0.00002 -> 0.000002. Other PPO settings unchanged: gamma .99, entropy .01, rollout 256, batch 64.

Exactly 500 episodes are authorized, seeds 18800-19299. Each has 1-3 connected rooms, an actual game exit in its final room, no per-room resets, and a sixty-second timeout. Room-count and first-type combinations are stratified by seed; remaining types, geometry, offsets and mirroring are seeded random, with four global orientations. Types are regular open rooms, winding rooms, rooms with central wall obstacles, sharp-corner rooms and L-shaped rooms. Corridors are three units wide; geometry uses physical floors/walls and ordinary player/sensor/exit components under the dungeon manager lifecycle. The editor-only layout builder replaces the training geometry; ordinary game PCG is unchanged.

Known-floor A* supplies at most one clear waypoint within ten units. Mixed mode retains an arrived waypoint when the next route segment is not yet clear, allowing the actor to approach the bend further; it does not create competing targets. Legacy navigation leaves this option off. Rewards follow the established dungeon task: +50 for actual exit entry, shared +5 cap on waypoint progress/arrival credit, -0.001/action. There is no room-entry reward or automatic completion merely for reaching a waypoint.

Preflight direct controls pass all fifteen first-room-type by room-count combinations; idle times out with zero positive shaping. Two earlier setup attempts made zero optimizer updates: [initial single-room settings failure](../qa-mixed-exit-20261110-025411-train/README.md) and [handoff failure](../qa-mixed-exit-20261110-025642-train/README.md). The ordered-sequence validation now permits the one-room/no-adjacent-room case; mixed-mode handoff retention changes those same direct controls from six to fifteen successful exits. These are setup corrections, not additional training episodes or evidence of policy learning.

Frozen evaluation before/after learning uses the same 150 cases, seeds 481000-481149, with room-count and first-type breakdowns. Parent baseline completes 117/150 exits (78%): one room 47/50, two rooms 41/50, three rooms 29/50. The original two-room cases 451000-451099 reproduce all QA-50 outcomes/step counts exactly, 57/100 exits. Repeat them afterward; check original empty-room waypoint sequences 288000-288099 after training for retention. Preserve QA-50, periodic checkpoints every 5,000 actions and a final checkpoint after exactly 500 episodes. No automatic deployment or further curriculum.

## Completed training and checkpoint

All 500 fresh seeds complete: **497 actual exits / three timeouts**, 95,071 actions, successful exits verified inside the final room at ground height after visiting every room. Room counts are 167 one-room episodes (167 exits), 166 two-room episodes (164 exits), 167 three-room episodes (166 exits). Across 1,000 generated rooms, types are regular 195 / winding 212 / middle walls 195 / sharp corners 202 / L shape 196. The 500 training draws contain 498 distinct physical floorplans.

These are exploratory training results on training seeds, not frozen evaluation success. Final counters: **751,864 model steps / 29,320 optimizer epochs / 5,200 lineage episodes**, adding 95,071 actions and 3,710 epochs to QA-50. The exact episode stop discards 95 incomplete-rollout transitions. QA-51 is excluded from this lineage. LR, schedule and optimizer groups reload at 0.000002. All nineteen periodic snapshots and the final archive pass SHA-256 and ZIP CRC checks.

Final SHA-256: `85acc6ac355b86a3916dd85f60b12139bd4408e78bdfd99e12ea03a0698aaac0`. [Final model](final_model.zip), [checkpoint manifest](checkpoints.json), [training summary](training-result.json), [completion](completion.json), [verification and paired analysis](verification-result.json).

## Matched frozen mixed-room exits

Same 150 layouts, target rules and seeds, with zero learning updates. No geometry or navigator changes between the parent and child evaluations.

| Room count | QA-50 | QA-52 |
| --- | ---: | ---: |
| One | 47/50 | 46/50 |
| Two | 41/50 | 36/50 |
| Three | 29/50 | 29/50 |
| Total | **117/150 (78%)** | **111/150 (74%)** |

Paired outcomes: 25 gains / 31 losses / 94 ties. Mean change -4 percentage points; exploratory paired-bootstrap 95% **-13.33..+6 points** (20,000 draws, seed 52). This does not show an improvement; the mixed-task decline itself is uncertain at this sample size.

| First room type | QA-50 | QA-52 |
| --- | ---: | ---: |
| Regular | 22/30 | 19/30 |
| Winding | 23/30 | 21/30 |
| Middle walls | 28/30 | 23/30 |
| Sharp corners | 19/30 | 22/30 |
| L shape | 25/30 | 26/30 |

These groups describe the first room; later rooms can contain other types. They are not isolated causal comparisons of each shape. Evaluation seeds are separate, but thirteen physical floorplans occur in both the 498 unique training layouts and the 150 unique evaluation layouts. This is not a completely geometry-disjoint test; spawn/exit variation can further distinguish episodes sharing a floorplan.

## Retention and interpretation

The original two-room benchmark regresses **57 -> 30/100 exits**: one gain / 28 losses / 71 ties, paired-bootstrap 95% **-36..-18 percentage points**. Mean attempt duration including timeouts rises 33.469 -> 45.502 seconds. Seventy final timeouts end in starting room 17 / corridor 53. Missing-waypoint actions rise 9,895/33,469 -> 21,251/45,502; low-motion actions 5,324 -> 9,897. Guided steering and navigator handoff/recovery interact; a specific optimizer, reward or input-history cause is not isolated by this experiment.

Original empty-room waypoint performance is largely retained: **45.41 -> 45.36 arrivals/minute**, 100/100 still reach ten, minimum 38 in both. Thus passing the simpler waypoint drill does not establish dungeon-navigation retention.

The sampled training policy reaches many exits, but its deterministic frozen counterpart does not show a gain on the new mixed test and loses substantial prior navigation skill. Training and evaluation differ in seeds and action selection; do not attribute the gap to exploration alone without a matched action-mode comparison. Retain the requested final model as evidence; keep QA-50 preferred rather than automatically replacing it.

## Evidence and checks

[Config](training-config.yaml), [run metadata](run.json), [source snapshots](source/), [parent mixed evaluation](parent-evaluation-result.json), [child mixed evaluation](child-evaluation-result.json). All phase layouts, episodes and compressed actions are retained; retention results are also in [completion](completion.json). Local-only runtime logs: `Logs/qa-mixed-exit/20261110-030013-train/`.

Checked exact 500-episode budget/seeds, room/type coverage, connected route bounds, every successful episode's room traversal and actual final-room exit, identical before/after layout records, fixed evaluation weights/counters, reloaded final model/counters/LR, all twenty archive hashes/CRC, unchanged QA-50/source checksums, restored project settings, Python syntax and local documentation links. Unity stopped. No additional training, deployment, commit or push was performed.
