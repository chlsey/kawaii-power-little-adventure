# Local waypoint navigation (`local-waypoint-v1`)

This is the current QA workflow. A deterministic, game-owned orchestrator owns observed-map memory, goals, routes and recovery; one learned local actor receives current rays, previous movement and one assigned waypoint. Normal navigation assignments are visible and capped at ten units by default; historical batch recipes retain six unless configured otherwise. Direct skill drills can assign longer targets. Policy architecture: **134 -> 64 -> 64 -> 2** (world X/Z movement). See the [system contract](../../../Training/PCG-system-design.md#overall-navigation-and-action-design).

## Current checkpoint and branch

**QA-32 is the preserved 8-12-unit skill baseline**, at 63,246 branch training steps and 7,000 total actor-lineage episodes: **492/500 training arrivals and 99/100 frozen arrivals at 8-12 units**, successful frozen mean 2.99 s. The successful branch is QA-27 (3-6) -> QA-30 (3-7) -> QA-31 (6-10) -> QA-32 (8-12). QA-28/29's separate 13-18-unit branch remains preserved and is excluded from these counters. This does not promote QA-32 across every historical task.

The [QA-33 navigator-enabled test](diagnostics/distant-exit-20261107-220640-evaluate/README.md) and [QA-34 navigator-disabled control](diagnostics/no-navigator-20261107-230050-evaluate/README.md) use these same frozen weights on 500 matched actual-exit cases: **364/500 versus 236/500**. Mean episode duration including 60-second timeouts is **31.09 versus 37.52 s**. On the 189 cases both cleared, disabled was faster: **11.75 versus 19.52 s**. This compares guidance removal from one waypoint-trained policy; no independently trained navigator-free policy was evaluated. Those comparisons performed no training and did not advance the curriculum automatically; later manually selected training runs are recorded separately below.

## QA-38 maximum waypoint distance

[Frozen QA-32 with a 10-unit maximum](diagnostics/max10-20261108-143217-evaluate/README.md) cleared 75/100 versus 74/100 under the historical six-unit cap on matched cases. Mean capped attempt time increased 29.97 -> 32.75 seconds; no demonstrated completion improvement. 64.3% of observed assignments were 8-10 units, with shorter points still permitted. This is navigation configuration evaluation, not new policy training. Default coordinator cap is 10; legacy runner recipes retain six unless `max_waypoint_distance` is set.

## Completed QA-36 exit-navigation continuation

[QA-36 completed 500 unique training attempts and 500 frozen benchmark cases after recovery](qa-waypoint-20261108-051102-train/README.md). Training cleared 403/500; frozen evaluation cleared **332/500 (66.4%) versus QA-32's 364/500 (72.8%)**, a loss of 6.4 points. Child retained without promotion; QA-32 remains preferred on this benchmark. The report preserves both crashes, rollback accounting, runtime comparer workaround, exact ten-case overlap verification and the failed new-slice reference check. All sessions are stopped.

## QA-39 standalone 1-10-unit continuation

[QA-39](qa-waypoint-reach-20261108-145257-train/README.md) is the standalone skill parent of QA-42, resumed directly from original QA-32 for 500 additional episodes: **500/500 training arrivals, 100/100 frozen arrivals**. On matched 1-10-unit evaluation cases, parent also cleared 100/100; mean arrival time improved **1.829 -> 1.594 seconds (12.8%)**. Final 72,054 steps / 7,500 lineage episodes. The standalone experiment did not exercise navigation or moving handoffs. A separate integrated evaluation is recorded below; QA-32 remains the reference for the historical matched navigation benchmark. Original and backup preserved; no automatic promotion.

## QA-40 longer-goal navigation

[Frozen QA-39 navigation](diagnostics/qa39-navigation-30to40-20261108-175349-evaluate/README.md) reached **67/100 initially unseen goals 30-40 units away**, with 33 timeouts: 30 before discovery and three after. The separate 50 x 50 empty room uses a ten-unit local waypoint cap and a 60-second deadline. Traces recorded 505 arrival-driven handoffs; 55/67 successes had at least two. Weights stayed fixed. Different model, geometry and cases prevent a matched comparison with QA-38's 75/100; generated-dungeon traversal remains untested.

## QA-41 predefined waypoint chains

[Frozen QA-39 chain test](diagnostics/waypoint-chain-20261108-182109/README.md) completed **33/100 ten-target chains**, despite **100/100 first-target arrivals**. Reference controls completed the same 100/100 chains. Targets were 1-10 units away on assignment, with no exploration or navigator and no reset between handoffs. Failed legs had mean 52.42 seconds on a fixed target; previous-action causality is not isolated. Weights unchanged.

## QA-42 random chain curriculum

[QA-42](qa-waypoint-chain-20261108-183246-train/README.md) continues full QA-39 through 100 two-target, 100 three-target, 100 four-target and 200 random 1-10-target episodes. Training completes 371/500 chains. Matched frozen ten-target completion improves **33 -> 41/100 (+8 points)**, with ten gained and two lost cases; common-success mean time increases 15.44 -> 20.92 seconds. Final **174,332 model steps / 8,000 lineage episodes**. All 500 training seeds are separate from evaluation, but the benchmark was already diagnosed. Parent preserved; actual navigator/exit performance and deployment remain untested.

## QA-43 another 500 chain episodes

[QA-43](qa-waypoint-chain-20261108-194516-train/README.md) continues QA-42 with the same four blocks and fresh seeds 11500-11999: 408/500 training chains complete. Frozen ten-target completion is **42/100 versus 41/100**, four cases gained and three lost; no clear completion improvement. Mean duration on the 38 common successes falls **21.59 -> 17.08 seconds**. Final **260,803 model steps / 8,500 lineage episodes**. Parent preserved, no deployment or actual navigator/exit evaluation.

## QA-44 pre-waypoint restart and full-minute arrivals

[QA-44](qa-waypoint-throughput-20261108-233001-train/README.md) restarts from the QA-17 actor via zero-step QA-18, excluding later waypoint/navigation learning; [QA-43 is fully backed up](qa43-backup-20261108-231031/BACKUP.md). Four authorized 500-episode stages: single targets 3-6, 6-8 and 8-10 (each 500/500 training, 100/100 frozen), then full-60-second continuous targets. On matched timed cases, mean arrivals **10.93 (QA-43) -> 38.98 (new parent before timed training) -> 44.43 (final)**; ten-target threshold **42 -> 95 -> 100/100**, final range 37-53. The timed stage adds +5.45 arrivals/minute, primarily improving the low tail; most overall gain precedes it. No causal input ablation. Final **328,433 branch steps / 4,000 actor-lineage episodes**. Empty-room local control only; no actual navigator/exit deployment.

## Historical exit-task baselines

[QA-19](qa-waypoint-20261103-163554-train/README.md) remains the 20 x 20 baseline with 100/100 frozen exit clears. QA-21 through QA-24 cover the separate 40 x 40, 30-second exit task; they do not establish consistent improvement from further training or larger exit rewards. Their checkpoints and full comparisons remain linked in the inventory and [training history](../../../Training/changelog/qa-player.md).

## Checkpoint inventory

| Checkpoint | Model steps in this workflow | Role |
| --- | --- | --- |
| [QA-44 restarted timed policy](qa-waypoint-throughput-20261108-233001-train/README.md) | 328,433, new QA-17 actor branch via QA-18 | Four 500-episode stages; final 44.43 targets/minute, 100/100 reach ten versus QA-43 10.93 / 42/100. Strong before timed stage too (38.98 / 95/100); no causal or deployment claim. |
| [QA-43 continued chain skill](qa-waypoint-chain-20261108-194516-train/README.md) | 260,803, full QA-42 continuation | Another 500 chain episodes; frozen ten-target completion 42/100 versus 41/100, common successes faster; no clear reliability gain. |
| [QA-42 chain continuation](qa-waypoint-chain-20261108-183246-train/README.md) | 174,332, full QA-39 continuation | 500 requested chain episodes, 371 training completions; frozen ten-target chains 41/100 versus parent 33/100, slower common successes; no exit/navigation evaluation. |
| [QA-39 1-10-unit child](qa-waypoint-reach-20261108-145257-train/README.md) | 72,054, direct QA-32 branch | 500/500 training and 100/100 frozen arrivals; matched mean arrival 1.829 -> 1.594 s; [Separate integrated evaluation](diagnostics/qa39-navigation-30to40-20261108-175349-evaluate/README.md): 67/100 goals at 30-40 units in a 50 x 50 empty room. |
| [QA-32 8-12-unit continuation](qa-waypoint-reach-20261107-214655-train/README.md) | 63,246 in the successful skill branch | Range raised to 8-12 units: 492/500 training and 99/100 frozen arrivals, unchanged rewards. |
| [QA-31 medium-range continuation](qa-waypoint-reach-20261107-185943-train/README.md) | 40,668 in the successful skill branch | Full distance band raised to 6-10 units: 500/500 training and 100/100 frozen arrivals, unchanged rewards. |
| [QA-30 gradual nearby fork](qa-waypoint-reach-20261107-185241-train/README.md) | 26,582 in a fork from QA-27 | Returned to the nearby parent, extended 3-6 -> 3-7 units: 500/500 training and 100/100 frozen arrivals, unchanged rewards. |
| [QA-29 further 500 episodes](qa-waypoint-reach-20261107-053815-train/README.md) | 397,548 in the dedicated skill branch | 221/500 training arrivals; matched frozen completion regressed 51/100 -> 33/100. Preserve QA-28; child retained without promotion. |
| [QA-28 farther-waypoint continuation](qa-waypoint-reach-20261107-051415-train/README.md) | 196,848 in the dedicated skill branch | 234/500 training arrivals and 51/100 frozen arrivals at 13-18 units; resumes QA-27's full model/optimizer. Retained separately without promotion. |
| [QA-27 nearby-waypoint skill](qa-waypoint-reach-20261107-034515-train/README.md) | 13,718 in a separate actor-transfer branch | 500/500 nearby training arrivals and 100/100 frozen arrivals; no exit, fresh critic/optimizer, combined waypoint budget +5 below local-objective floor +50. Parent of both later skill branches. |
| [QA-18 transfer start](qa-waypoint-transfer-start/final_model.zip) | 0 | Actor/head/distribution from QA-17; fresh critic and optimizer. [Provenance](qa-waypoint-transfer-start/transfer.json). |
| [QA-19 snapshot at 10k](qa-waypoint-20261103-163554-train/checkpoints/rl_model_10000_steps.zip) | 10,000 | Retained intermediate training state; not independently frozen-evaluated. |
| [QA-19 snapshot at 20k](qa-waypoint-20261103-163554-train/checkpoints/rl_model_20000_steps.zip) | 20,000 | Retained intermediate training state; not independently frozen-evaluated. |
| [QA-19 snapshot at 30k](qa-waypoint-20261103-163554-train/checkpoints/rl_model_30000_steps.zip) | 30,000 | Retained intermediate training state; not independently frozen-evaluated. |
| [QA-19 final](qa-waypoint-20261103-163554-train/final_model.zip) | 36,286 | Completed 500-episode 20 x 20 run and 100 frozen evaluations. |
| [QA-21 final](qa-waypoint-20261103-172142-train/final_model.zip) | 160,362 | Completed 500 additional 40 x 40 episodes and 100 frozen evaluations; waypoint dependence remains unestablished. |
| [QA-22 final](qa-waypoint-20261104-011557-train/final_model.zip) | 271,368 | Completed 500 further 40 x 40 episodes and 100 enabled / 100 masked frozen evaluations; waypoint reliance remains unresolved. |
| [QA-23 final](qa-waypoint-20261104-084138-train/final_model.zip) | 496,365 | Completed 1,000 more 40 x 40 episodes and 100 enabled / 100 masked frozen evaluations; enabled completion regressed. |
| [QA-24 final](qa-waypoint-20261104-170721-train/final_model.zip) | 613,364 | Completed 500 more episodes with exit reward +200 and 100 enabled / 100 masked frozen evaluations; no clear completion improvement. |
| [QA-21 snapshots](qa-waypoint-20261103-172142-train/checkpoints/) | 46,286-156,286 | All twelve periodic checkpoints; [file hashes](qa-waypoint-20261103-172142-train/checkpoints.json). |

[QA-19 file hashes](qa-waypoint-20261103-163554-train/checkpoints.json), [transfer file hash](qa-waypoint-transfer-start/checkpoints.json), [training records](qa-waypoint-20261103-163554-train/training-episodes.jsonl), [evaluation records](qa-waypoint-20261103-163554-train/evaluation-episodes.jsonl).

The actor parent remains in its original architecture: [QA-17 local vision](../local-vision-v1/qa-local-movement-20261103-020805/README.md). Its 2,000 episodes / 198,495 steps remain separate from the 500 waypoint-training episodes / 36,286 steps. Do not copy it into the waypoint family or relabel its policy semantics.

The [training guide](../../../Training/README.md#qa-player-training) maps each runner/config to its task. The [8-12 skill config](../../../Training/qa-player-waypoint-reach-8to12.yaml) reproduces the QA-31 -> QA-32 continuation; the [distant-exit config](../../../Training/qa-player-waypoint-distant-exit-test.yaml) evaluates QA-32 without training. The historical [20 x 20 config](../../../Training/qa-player-waypoint-20x20.yaml) still names the zero-step QA-18 transfer to reproduce QA-19. Templates keep explicit parents and never silently follow the latest timestamp.

## Historical larger-room exit stage

[QA-20 larger-room validation](qa-waypoint-40x40-validation-20261103/README.md) records the reference and frozen policy comparisons. The [40 x 40 continuation config](../../../Training/qa-player-waypoint-40x40.yaml) starts from QA-19's full checkpoint/optimizer, retaining its 36,286 model steps and 2,500 prior lineage episodes. Exits are 30-34 units away, vision remains 12 units, and waypoints remain within six units. The [scene guide](../../../Assets/Scenes/QaSingleRoomTraining.README.md#larger-room-waypoint-continuation) separates reference verification, frozen waypoint-dependence comparisons and explicit training. Future trained runs stay in this architecture folder; the preserved QA-19 parent is not overwritten.

Completed larger-room run: [QA-21 checkpoints and provenance](qa-waypoint-20261103-172142-train/README.md), followed by QA-22/23/24 above. Periodic filenames use cumulative model steps from QA-19's 36,286; `rl_model_46286_steps.zip` is 10,000 new steps in this stage. These exit runs used a 30-second limit; QA-33/34's 60-second frozen integration tests are a separate benchmark. All recorded sessions are stopped.
