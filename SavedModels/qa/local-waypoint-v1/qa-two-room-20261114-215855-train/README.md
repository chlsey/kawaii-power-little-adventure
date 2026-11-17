# QA-59: 1,000 corridor-focused two-room episodes

Status: **complete**, reconciled 2026-11-15 01:22 UTC. All 1,000 training episodes reached the exit; frozen two-room completion improved from 38/100 to 46/100, with uncertainty remaining. This uses the existing original two-room navigation task, starting in the first room and traversing generated corridors to the second-room exit. It does not spawn exclusively inside corridors. No further learning or deployment.

Full QA-58 actor/critic/optimizer continuation, parent SHA-256 `ed6fddf1b411dacb345691a6faf95e976e46964ac3ed0e7d3996fae13b7b5763`, starting at 2,610,165 model steps / 101,870 optimizer epochs / 15,700 completed lineage episodes. [Parent report](../qa-mixed-exit-20261114-072247-train/README.md).

Exactly 1,000 additional training episodes, fresh seeds 29800-30799, maximum 600,000 actions. Unchanged local-waypoint-v1, LR 0.000002, gamma .99, entropy .01, rollout 256 / batch 64 / CPU. Two 20 x 20 rooms in a 100 x 100 area; oracle-guided waypoints up to ten units, sixty-second deadline. Reward +50 actual exit / shared +5 waypoint progress and arrivals / -0.001 per action. The task changes from mixed-room geometry to the existing original two-room corridor traversal; environment and runner sources are unchanged. [Run config](training-config.yaml), [maintained config](../../../../Training/qa-player-corridor-qa59-1000.yaml), [run metadata](run.json).

Runner checks three steering controls and one idle control, replays frozen parent on seeds 451000-451099 (38/100 exits), then trains exactly the authorized budget and saves periodic checkpoints every 5,000 actions plus the final model. Frozen child uses the same 100 layouts. Empty waypoint retention uses the original 100 sequences, seeds 288000-288099 (parent 45.59 arrivals/min). These reused benchmarks do not establish unseen-geometry generalization. Retain mixed-room QA-58 results as historical context; this runner does not measure mixed-room retention.

Earlier checkpoints remain preserved. The completed budget brings the actor lineage to 16,700 completed episodes, with the previous 11,000 mixed-task episodes unchanged. No automatic follow-on training, deployment, commit or push. Existing direct-file instrumentation is retained; no QA transport changes.

Local live evidence: `Logs/qa-corridor-training/20261114-215855-train/`, with launcher output in `Logs/qa59-launch.stdout.log` and `Logs/qa59-launch.stderr.log`. Final results are reconciled here and in the [QA history](../../../../Training/changelog/qa-player.md).

## Completed results

| Metric | QA-58 parent | QA-59 |
| --- | ---: | ---: |
| Frozen two-room exits | 38/100 | 46/100 |
| Mean attempt time, including timeouts | 41.820 s | 37.869 s |
| Corridor failures | 50 | 46 |
| Starting-room failures | 11 | 8 |
| Exit-room failures | 1 | 0 |
| Empty waypoint arrivals/min | 45.59 | 45.66 |
| Empty cases reaching ten | 100/100 | 100/100 |

Training: exactly 1,000 exits / zero timeouts in 123,853 actions. All seeds 29800-30799 completed once. Final 2,734,018 model steps / 106,700 optimizer epochs / 16,700 completed lineage episodes, adding 4,830 optimizer epochs. Exact stopping discards 205 pending rollout transitions. Final SHA-256 `d039b40d8d87fc142314e47daa76aa97f145aabad29f0f955c97fa6a24ffa6d4`.

Frozen evaluation uses identical layouts/seeds 451000-451099 with zero learning: 30 both clear, sixteen gained, eight lost, 46 both fail. Exploratory paired-bootstrap 95% completion change -1..+18 percentage points, 20,000 draws, seed 59. The observed eight-point gain remains uncertain; these reused benchmarks do not establish unseen-geometry generalization. QA-50 retains 57/100 on the original two-room task, and mixed-room retention was not evaluated after this run.

Corridor reliability remains weak: 46 of 54 failures end there. Low-motion actions rise 7,683 -> 9,363 and missing-waypoint actions 15,954 -> 16,089. Completion and average capped time improved, but this does not identify a causal repair mechanism. Empty-room movement remains retained, range 38-53 arrivals.

Verified exact episode budget/actions, parent/source hashes unchanged, identical evaluation layouts, all 25 checkpoint hashes and ZIP CRC, and restored project settings. Python and Unity stopped. [Training result](training-result.json), [paired comparison](exit-comparison.json), [empty retention](empty-room-comparison.json), [completion](completion.json), [final verification](final-verification.json), [final model](final_model.zip).
