# QA-50 lower-rate waypoint refresh and wall/corner recovery

Status: complete. Exactly 400 authorized training episodes, followed by frozen evaluations; no further learning or deployment.

Continue preserved QA-47 (`../qa-two-room-20261109-070143-train/final_model.zip`), SHA-256 `2eafa17b18b59271d93ac2d0a4a3f95240788296f5c8132d9f20b33740c7477d`, 416,793 steps / 16,250 optimizer updates / 4,300 lineage episodes. Preserve actor, critic and optimizer moments, reduce LR and its schedule 0.0002 -> 0.00002. Other PPO settings stay unchanged.

Authorized budget: 100 full-minute random waypoint episodes (seeds 18000-18099), then 300 full-minute wall/corner recovery episodes (18100-18399). Exactly 240,000 actions if complete. One target remains active until arrival; movement/history persists across targets. Actual handoff distances 1-10 units, nominal precomputed legs 1.5-9.5. Existing timed reward remains 0.015 per arrival plus 0.0015 per new-closest unit minus 0.001 per action. No new wall penalty.

The second stage uses the same physical 40x40 room: half the resets start 0.4-0.7 units from a wall surface, half near an inside corner, in four orientations. Targets stay in reachable convex free-space strips/corner patches and never require crossing a wall. This isolates near-wall local control without navigator replanning or competing waypoints; it does not train blocked-target route finding. No teleport occurs within an episode.

Before learning: eight direct controls and one idle episode per drill, then 100 frozen cases each for original empty-room targets, recovery and two-room exit navigation. Repeat the three frozen checks after each 100 training episodes. Original empty/two-room benchmarks are reused; recovery seeds 466000-466099 are new evaluation cases. Evaluations use fixed weights and deterministic commands. Save each 100-episode block separately; no automatic deployment or further stage.

Frozen parent baselines: empty-room 44.85 arrivals/minute (100/100 reach ten), recovery 47.78/minute (99/100 reach ten; 3,154 low-motion actions / 60,000), two-room 54/100 exits. Both historical benchmark case counts/outcomes/steps reproduce exactly. Eight direct controls per drill reach at least ten targets without low-motion actions; idle controls reach zero and earn zero positive shaping. All verification and baseline weights/counters remain fixed.

## Training and checkpoint lineage

All 400 seeds completed exactly 600 actions / sixty seconds: 100 refresh episodes and 300 recovery episodes (150 wall / 150 corner). No extra episode starts after each block. Training totals 17,373 arrivals, of which 13,198 occur in the recovery stage; these use exploratory actions and are separate from frozen results. Recovery block means are 39.18 / 44.65 / 48.15 arrivals per minute on different training seeds.

Final counters: **656,793 steps / 25,610 optimizer epochs / 4,700 lineage episodes**; adds exactly 240,000 actions and 9,360 optimizer epochs to QA-47. Each 100-episode stop discards 96 incomplete-rollout transitions (384 total) at the exact authorized boundary. Actor, critic and optimizer moments continue from QA-47; learning rate and its schedule are verified at 0.00002 in the reloaded final model. All four block checkpoints and twenty-four periodic snapshots are retained, alongside the final model. All twenty-nine archives pass SHA-256 and ZIP CRC verification.

Final SHA-256: `acffd0594bb6a03a157e2853688dfc4d7647606d052cb241fe620d9f8eb4db64`. [Final model](final_model.zip), [checkpoint manifest](checkpoints.json), [completion](completion.json), [verification and paired analysis](verification-result.json).

## Matched frozen results

Each row uses the same 100 cases per task. Evaluations make zero learning updates. Empty/recovery values are arrivals per sixty seconds; low motion is displacement <0.02 units per action across 60,000 recovery actions.

| Checkpoint | Training completed | Empty arrivals/min | Recovery arrivals/min | Recovery low-motion actions | Two-room exits |
| --- | --- | ---: | ---: | ---: | ---: |
| QA-47 parent | None in this run | 44.85 | 47.78 | 3,154 | 54/100 |
| After 100 | 100 refresh | 45.10 | 36.25 | 14,499 | 34/100 |
| After 200 | Refresh + 100 recovery | 45.59 | 49.33 | 2,002 | 48/100 |
| After 300 | Refresh + 200 recovery | 45.58 | 49.86 | 989 | 55/100 |
| After 400 | Refresh + 300 recovery | **45.41** | **52.29** | **927** | **57/100** |

Empty-room ten-target completion remains 100/100; minimum improves 37 -> 38. Recovery ten-target completion remains 99/100, with the same zero-arrival case 466071 (597 low-motion actions in the final evaluation). Recovery mean rises **9.44%** and low-motion actions fall **70.61%**. Guided away/perpendicular actions in recovery fall 3,379 -> 995. The curriculum improves local wall/corner movement but does not eliminate every stuck state.

Two-room completion rises three points, with eleven gained / eight lost cases; exploratory paired case-bootstrap 95% change **-5..+12 percentage points**, so this is not a clear exit-reliability gain. Mean attempt duration including timeouts improves 34.884 -> 33.469 seconds. Forty-three failures remain: thirteen in the starting room, thirty in corridors. Replans rise 3,735 -> 6,205 and missing-waypoint actions 6,439 -> 9,895 across 34,884 -> 33,469 actions; better local recovery has not established a solution to integrated navigation/recovery loops.

Exploratory paired bootstrap (20,000 draws, seed 50): empty-room gain +0.56 arrivals/min, 95% +0.41..+0.72; recovery gain +4.51, 95% +2.74..+6.54. Empty case changes: 49 improve / four worsen / 47 tie; recovery: 62 improve / seventeen worsen / 21 tie. These are matched-case estimates on reused benchmarks, not broad dungeon generalization. Learning rate and task curriculum both changed; this run does not isolate their individual effects.

The refresh-only checkpoint regressed on recovery and dungeon traversal despite maintaining empty-room performance; the recovery stage restored and improved the local skill. Preserve QA-47 as the established reference and QA-50 as a recovery-improved candidate; no automatic model promotion, ONNX export or deployment. The separately proposed QA-49 endless task did not train during this run.

## Evidence

[Config](training-config.yaml), [run metadata](run.json), [source snapshots](source/), [empty baseline](parent-empty-result.json), [recovery baseline](parent-recovery-result.json), [two-room baseline](parent-two-room-result.json), [final empty test](after-400-empty-result.json), [final recovery test](after-400-recovery-result.json), [final two-room test](after-400-two-room-result.json). Every phase retains episode records, target sequences and compressed action traces. Runtime logs are local-only under `Logs/qa-recovery/20261109-224625-train/`.

Parent checksum and source snapshots unchanged; saved project settings restored; diagnostic/training Unity processes stopped. Runner Python syntax and affected-file whitespace/link checks passed.
