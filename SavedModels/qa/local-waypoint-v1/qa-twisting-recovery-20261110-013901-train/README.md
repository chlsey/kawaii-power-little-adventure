# QA-51 wall/corner continuation and seeded twisting rooms

Status: complete; performance regressed. All 200 wall/corner and 200 winding-room episodes are saved. Retain QA-51 as a failed alternative; QA-50 is unchanged and preferred between this pair. No further training or deployment.

Continue preserved QA-50, SHA-256 `acffd0594bb6a03a157e2853688dfc4d7647606d052cb241fe620d9f8eb4db64`, 656,793 steps / 25,610 optimizer epochs / 4,700 lineage episodes. Retain the full actor, critic and optimizer at constant learning rate 0.00002; gamma .99, entropy .01, rollout 256 and batch 64 remain unchanged.

Authorized stages: 200 wall/corner episodes (seeds 18400-18599), then 200 seeded twisting-room episodes (18600-18799). Each runs sixty seconds / 600 actions, total 240,000 new actions. Save both stage checkpoints and the final model. No additional stage or deployment is authorized.

The first stage repeats QA-50's physical-wall/inside-corner drill, 100 of each type across four orientations. The second uses training-only, enclosed three-unit-wide winding rooms inside the existing 40x40 floor. Each seed generates a self-avoiding 6-10 segment route with 5-7-unit straight sections and ninety-degree bends. Physical colliders enclose the carved walkable cells; the underlying floor remains beneath and outside those walls. This is an irregular training-room generator, separate from ordinary game PCG and from the untrained endless-room setup.

One active target is placed at the next bend/end, held until arrival. On reaching the far end, the route reverses; movement history and sensor state persist. All actual waypoint assignments remain within 1-10 units. The existing timed shaping stays 0.015 per arrival + 0.0015 per new-closest unit - 0.001 per action, with no new penalties or exit rewards.

Preflight verifies eight direct controls plus idle for each drill; winding controls must traverse both endpoints without low-motion actions. Frozen checks before learning and after each 200-episode stage use 100 cases each: original empty room (288000-288099), wall/corner recovery (466000-466099), new winding rooms (467000-467099), original generated two-room exits (451000-451099). Historical QA-50 retention must reproduce exactly. Record winding far-end/return arrivals, throughput and low-motion actions separately. Evaluations have zero optimizer updates.

Parent baselines exactly reproduce QA-50: empty-room mean 45.41, recovery mean 52.29 and two-room exits 57/100, with matching historical case counts/outcomes/steps. In the new winding task, QA-50 already reaches the far end and returns in 100/100 layouts, averaging 39.26 waypoint arrivals/minute and 4.60 endpoint arrivals/minute with zero low-motion or away/perpendicular actions. Completion is already at ceiling in this drill; speed and transfer must be measured separately. All eight direct winding controls also traverse both endpoints without low-motion actions, and idle reaches none.

After the 200 wall/corner episodes: 120,000 new actions, 9,547 exploratory training arrivals, 776,793 model steps / 30,290 optimizer epochs / 4,900 lineage episodes. Frozen empty/recovery/winding means are 45.65 / 51.67 / 38.58; winding far-end and return completion stays 100/100 with zero low-motion actions. Two-room exits regress to 24/100, with 25,320 missing-waypoint actions across 48,698 actions and 15,165 low-motion actions. This intermediate checkpoint does not improve integrated navigation. The requested winding stage continued from this saved intermediate.

## Completed budget and saved model

All 400 requested seeds complete exactly 600 actions / sixty seconds, with no extra auto-reset episode: 200 wall/corner episodes (100 wall / 100 corner) and 200 winding episodes. Total 240,000 training actions and 11,451 exploratory arrivals. The winding stage records 1,904 arrivals (mean 9.52), 78/200 reaching the far end and 36/200 returning to start. These exploratory training cases and actions are separate from frozen results.

Final model: **896,793 steps / 34,970 optimizer epochs / 5,100 lineage episodes**, adding 240,000 actions and 9,360 epochs to QA-50. Exact stage stops discard 192 partial-rollout transitions each. Actor, critic and optimizer continue throughout; LR/schedule/optimizer groups remain 0.00002. Both stage checkpoints, twenty-four periodic snapshots and the final model are retained; all twenty-seven archives pass SHA-256 and ZIP CRC verification.

Final SHA-256: `95c4cffad40ab07463fe442033db7a147dddbddd0344fac207ebb4290a165fe4`. [Final model](final_model.zip), [stage-one model](after-200/final_model.zip), [stage-two model](after-400/final_model.zip), [manifest](checkpoints.json), [completion](completion.json), [verification and paired results](verification-result.json).

## Frozen comparison

Same 100 seeds, target sequences and physical layouts per task; all weights/counters fixed during evaluation. Values are waypoint arrivals per sixty seconds, except actual two-room exits.

| Checkpoint | Empty room | Wall/corner recovery | Winding room | Winding far end / return | Two-room exits |
| --- | ---: | ---: | ---: | --- | ---: |
| QA-50 parent | 45.41 | 52.29 | 39.26 | 100/100 / 100/100 | 57/100 |
| After 200 wall/corner | 45.65 | 51.67 | 38.58 | 100/100 / 100/100 | 24/100 |
| After all 400 | 44.86 | 44.52 | 5.33 | 26/100 / 0/100 | 29/100 |

Final winding low-motion actions rise 0 -> 45,764/60,000 (76.27%); away/perpendicular commands 0 -> 42,679/60,000. The waypoint remains single and fixed until arrival, with no stall replanning in this drill. This demonstrates a learned-control regression under the same geometry/target rules, rather than evidence of simultaneous competing waypoints. The experiment does not identify a specific optimizer or reward mechanism as its cause.

Recovery ten-target cases fall 99 -> 95/100, low-motion actions 927 -> 6,029. Empty-room ten-target cases remain 100/100 with zero low-motion actions. Integrated two-room failures total 71: 47 end in the starting room and 24 in corridors. Mean attempt duration including timeouts rises 33.469 -> 46.060 seconds. Low-motion actions rise 5,324/33,469 -> 26,855/46,060; final missing-waypoint actions are 5,995/46,060 and away/perpendicular actions 23,321/46,060. Final steering failures occur despite substantially more time with guidance than at the wall-only intermediate.

Paired case analysis (20,000 bootstrap draws, seed 51): two-room outcomes have four gains / 32 losses / 64 ties, mean -28 percentage points, exploratory 95% -38..-18. Empty arrivals change -0.55/min (95% -0.68..-0.42), recovery -7.77 (95% -10.78..-5.14), winding -33.93 (95% -35.22..-32.64), with all 100 winding cases worse. These measured regressions support keeping QA-50. They do not justify automatically continuing learning or deploying the final model.

## Layout sampling and limits

Layouts are sampled with replacement by seed: the 200 training episodes contain **112 distinct physical winding floorplans**, the 100 evaluation cases contain **77**, and **67 floorplans occur in both sets**. Evaluation seeds are separate, but these are not geometry-disjoint generalization tests. This overlap is disclosed rather than treating different seeds as proof of unseen geometry. QA-50 already solved every evaluation route before additional learning; QA-51 fails even with this substantial overlap.

The generator produces orthogonal right-angle bends, not acute-angle walls, and uses known centerline routing without discovery, enemies or exit rewards. The floor extends under and outside the enclosing physical walls. Control and baseline tests establish traversability of the recorded task; ordinary game PCG generation was not modified by this curriculum.

## Evidence and checks

[Config](training-config.yaml), [metadata](run.json), [source snapshots](source/), [winding baseline](parent-twisting-result.json), [final winding test](after-400-twisting-result.json), [final two-room test](after-400-two-room-result.json). Each phase retains target/layout records, episode results and compressed action traces. Local-only runtime logs: `Logs/qa-twisting/20261110-013901-train/`.

Exact episode budget/seeds, matching layout/target records across all evaluations, geometry bounds/turns, checkpoint reload/counters/LR, all archive hashes/CRC, Python syntax and documentation links checked. Parent/source hashes unchanged; original project settings restored; Unity stopped. No commit, push, deployment or training beyond these 400 episodes was performed in this task.
