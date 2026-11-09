# QA-42: random waypoint-chain curriculum from QA-39

**Complete: all 500 requested training episodes, then 100 frozen ten-target chains. The child completes 41/100 versus preserved QA-39's 33/100 on identical cases (+8 percentage points).** Ten parent failures become successes, two parent successes become failures, 31 succeed under both and 57 fail under both. This is a measured improvement on the reused chain benchmark, with 59 child timeouts still preventing a reliability claim. All sessions stopped; no further training or deployment.

## Requested training blocks

| Block | Episodes | Complete chains | Timeouts | Waypoint arrivals | Training actions |
| --- | ---: | ---: | ---: | ---: | ---: |
| 2 waypoints | 100 | 94 | 6 | 194 | 7,000 |
| 3 waypoints | 100 | 87 | 13 | 282 | 12,569 |
| 4 waypoints | 100 | 64 | 36 | 334 | 26,352 |
| Random 1-10 waypoints | 200 | 126 | 74 | 829 | 56,357 |

Exactly **371/500 complete training chains, 129 timeouts, 1,639 arrivals / 2,011 planned targets**. Training is stochastic and the policy changes within each block; these are not frozen success estimates or controlled comparisons between lengths. Every episode receives a fresh seeded random sequence. The final block samples chain length uniformly from 1-10 using a separate seeded RNG:

| Random chain length | Training episodes | Complete chains |
| --- | ---: | ---: |
| 1 | 19 | 19 |
| 2 | 19 | 17 |
| 3 | 21 | 20 |
| 4 | 20 | 16 |
| 5 | 19 | 13 |
| 6 | 21 | 12 |
| 7 | 20 | 10 |
| 8 | 20 | 6 |
| 9 | 20 | 8 |
| 10 | 21 | 5 |

Training seeds are **11000-11099**, **11100-11199**, **11200-11299**, then **11300-11499**. No episode beyond those 500 was started by the training vector environment. The 100 reference controls, 100 parent replays and 100 child evaluations do not increment actor-training lineage.

## Frozen matched comparison

Both models receive the exact precomputed ten-target coordinate sequences on seeds **288000-288099**, no reward or weight updates. Parent replay exactly reproduces QA-41's outcomes, action counts and arrival counts in all 100 cases before learning; geometry and control encoding are preserved.

| Measure | QA-39 parent | QA-42 child |
| --- | ---: | ---: |
| Complete ten-target chains | 33/100 | 41/100 |
| Timeouts | 67 | 59 |
| Total waypoint arrivals | 620 | 681 |
| Observed arrival handoffs | 587 | 640 |
| Mean episode duration, timeouts capped at 60 s | 45.33 s | 44.59 s |
| Successful-chain mean, different success subsets | 15.55 s | 22.41 s |
| Mean duration on the 31 common successes | 15.44 s | 20.92 s |

| Waypoint reached | QA-39 parent | QA-42 child |
| --- | ---: | ---: |
| 1 | 100/100 | 100/100 |
| 2 | 97/100 | 98/100 |
| 3 | 89/100 | 91/100 |
| 4 | 69/100 | 75/100 |
| 5 | 61/100 | 69/100 |
| 6 | 53/100 | 61/100 |
| 7 | 45/100 | 55/100 |
| 8 | 39/100 | 48/100 |
| 9 | 34/100 | 43/100 |
| 10 | 33/100 | 41/100 |

Counts in the last table are cumulative out of all 100 chains; later targets are attempted only after their predecessors. Child recorded 681 arrivals / 740 attempted targets; 260 remaining planned targets were unattempted, not extra failures. Previous-action inputs match the preceding command on every continuing action in both frozen traces, including all handoffs.

Exploratory paired case-bootstrap 95% interval for completion gain: **+2 to +15 percentage points** (20,000 draws, seed 42); exact paired test p=0.03857. This quantifies case sampling on one parent/child pair, not independent-training variability. The benchmark was already diagnosed and informed the curriculum, so this is not a new untouched test set. Multiple aspects of training change together, including sequence exposure and reward normalization; their individual effects are not isolated.

Mean capped duration improves only 0.741 seconds; paired case-bootstrap interval **-1.741 to +3.315 seconds saved**, so a general speed improvement is not established. Child is slower on the common-success subset. Aggregate away/perpendicular command counts decrease from 17,786/45,330 (39.2%) to 15,431/44,589 (34.6%); states/paths differ, so this is descriptive rather than an input-level causal comparison.

## Environment, rewards and optimizer

- Existing empty 40 x 40 scene, centre reset at chain start, no exit, no navigator/exploration/replans. No obstacles, enemies or generated dungeon.
- Precompute a bounded random walk per episode: uniform nominal angle and radius 1.5-9.5 units, reject points outside +/-15 X/Z. Arrival tolerance 0.5 ensures each next target is 1-10 units from the actual handoff position; all recorded assignments across phases span **1.05108-9.90670 units**. Target coordinates are independent of policy actions, although actual handoff positions depend on movement.
- Arrival immediately changes the instruction without resetting player position, orientation, previous action, sensors or episode metrics. No target replacement or abandonment. A whole chain has one 60-second / 600-action budget; actions last 0.1 game seconds, vision is 12 units, playback 100.
- Training uses existing per-leg +0.1/unit new closest approach and +1 arrival, **divided by planned chain length**, plus -0.001/action. Each leg gets a fresh tracker and scaled credit; total positive reward remains <=5 and below the +50 local-objective floor. No actual-exit payout exists in this skill exercise. Frozen diagnostics preserve zero reward. No history masking or input-contract change.
- Full QA-39 actor, critic and optimizer continuation; same local-waypoint-v1 / 134 -> 64 -> 64 -> 2. PPO LR 0.0002, gamma 0.99, entropy 0.01, rollout 256, batch 64, ten optimization epochs, CPU. Each block stops at its exact episode budget and saves full state. Unfinished rollouts at those stops discard **390 collected transitions** rather than start extra episodes; 102,278 actions were collected and 101,888 entered optimized rollouts. Existing timeout/termination handling remains unchanged.

Reference preflight cleared 100/100 random-length chains / 548 targets, covering every length 1-10. Reward accounting and assignment limits passed; maximum observed positive reference return component was 1.8400. Reference controls verify feasibility, not learned success.

## Checkpoint and integrity

Parent: [QA-39](../qa-waypoint-reach-20261108-145257-train/README.md), SHA-256 `a90f3a238b4d6e6b236db357467edafd97b89c5b2dc57bccba50263c72102d2f`, 72,054 steps / 2,800 updates / 7,500 completed lineage episodes. Parent archives unchanged.

Child: **174,332 model steps / 6,780 optimizer updates / 8,000 completed lineage episodes**, adding 102,278 steps and 3,980 updates in this campaign. Final SHA-256 **`0bd60198d0904f0099c1e19691a1dc553c220b512542576d1c56eff0b2b0025d`**. All **24** saved zip archives passed manifest/hash/CRC checks, including four block-end models and periodic snapshots. Final frozen evaluation added zero steps/updates; 45 sampled saved-model predictions match evaluation traces. Runtime source hashes match retained snapshots, all seven Unity sessions finished normally, settings restored and processes stopped.

This validates only the fixed-sequence empty-room task. Actual navigator/exit performance, 8-12-unit retention, obstacle/combat handling, dungeon integration and deployment of this child remain untested. No model promotion or further stage follows automatically.

## Artifacts and reproduction

[Final model](final_model.zip), [four block and periodic checkpoint identities](checkpoints.json), [training summary](training-result.json), [500 training records](training-episodes.json), [matched comparison](comparison.json), [analysis script](analyze.py), [completion verification](completion-verification.json), [run-owned config](training-config.yaml), [lineage/source hashes](run.json).

[Two-target block](training-1-result.json), [three-target block](training-2-result.json), [four-target block](training-3-result.json), [mixed-length block](training-4-result.json), [parent frozen result](parent-evaluation-result.json), [child frozen result](evaluation-result.json), [reference verification](verification/result.json).

[Parent full actions](parent-evaluation/actions.jsonl.gz), [child full actions](evaluation/actions.jsonl.gz), [parent coordinates](parent-evaluation/chains.jsonl), [child coordinates](evaluation/chains.jsonl), [child per-leg outcomes](evaluation/legs.jsonl), [child per-chain outcomes](evaluation/episodes.jsonl). Each training phase folder also retains its exact coordinates, leg records, outcomes and session settings; training action traces are disabled.

From the game directory with other Unity sessions closed:

```powershell
python -B Training/run_waypoint_chain.py --config Training/qa-player-waypoint-chain-curriculum.yaml --mode verify
python -B Training/run_waypoint_chain.py --config Training/qa-player-waypoint-chain-curriculum.yaml --mode train
```

These commands verify or reproduce the explicitly selected recipe from QA-39; they do not resume the child or select the newest timestamp. To analyze saved data only:

```powershell
python -B SavedModels/qa/local-waypoint-v1/qa-waypoint-chain-20261108-183246-train/analyze.py
```

[Maintained recipe](../../../../Training/qa-player-waypoint-chain-curriculum.yaml), [runner](../../../../Training/run_waypoint_chain.py), [session guide](../../../../Assets/Scenes/QaSingleRoomTraining.README.md#random-waypoint-chain-curriculum), [QA history](../../../../Training/changelog/qa-player.md).

Local runtime: `Logs/qa-waypoint-chain-curriculum/20261108-183246-train/`. All 500 authorized training episodes and both frozen comparisons are complete.
