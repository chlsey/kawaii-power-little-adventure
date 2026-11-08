# Local waypoint navigation (`local-waypoint-v1`)

This is the current QA workflow. A deterministic, game-owned orchestrator owns observed-map memory, goals, routes and recovery; one learned local actor receives current rays, previous movement and one assigned waypoint. Normal navigation assignments are visible and within six units; direct skill drills can assign longer targets. Policy architecture: **134 -> 64 -> 64 -> 2** (world X/Z movement). See the [system contract](../../../Training/PCG-system-design.md#overall-navigation-and-action-design).

## Current checkpoint and branch

**QA-32 is the latest trained skill checkpoint**, at 63,246 branch training steps and 7,000 total actor-lineage episodes: **492/500 training arrivals and 99/100 frozen arrivals at 8-12 units**, successful frozen mean 2.99 s. The successful branch is QA-27 (3-6) -> QA-30 (3-7) -> QA-31 (6-10) -> QA-32 (8-12). QA-28/29's separate 13-18-unit branch remains preserved and is excluded from these counters. This does not promote QA-32 across every historical task.

The [QA-33 navigator-enabled test](diagnostics/distant-exit-20261107-220640-evaluate/README.md) and [QA-34 navigator-disabled control](diagnostics/no-navigator-20261107-230050-evaluate/README.md) use these same frozen weights on 500 matched actual-exit cases: **364/500 versus 236/500**. Mean episode duration including 60-second timeouts is **31.09 versus 37.52 s**. On the 189 cases both cleared, disabled was faster: **11.75 versus 19.52 s**. This compares guidance removal from one waypoint-trained policy; no independently trained navigator-free policy was evaluated. No training or automatic curriculum advancement followed.

## Historical exit-task baselines

**QA-24 completed:** [500 episodes from QA-23 with exit reward +200](qa-waypoint-20261104-170721-train/README.md), **232/500 training clears**, then **54/100 enabled versus 50/100 masked** frozen clears. Enabled gain over QA-23 is +6 points (-4 to +16); latest input effect +4 (-9 to +16). No clear completion improvement or positive latest waypoint benefit is established. The relative gap improved mainly because masked completion declined 62 -> 50. Parent preserved; all sessions stopped. This reward-change continuation has no parallel unchanged-reward training arm.

**QA-23 completed:** [1,000 further 40 x 40 episodes from QA-22](qa-waypoint-20261104-084138-train/README.md), **559/1,000 training clears**, then **48/100 enabled versus 62/100 masked** frozen clears. Enabled completion regressed **64 -> 48** (paired 95% interval -30 to -2 points), although rewarded waypoint arrivals rose 201 -> 346. Child retained without promotion; parent archive preserved; all sessions stopped.

**QA-22 completed:** [500 further 40 x 40 episodes from QA-21](qa-waypoint-20261104-011557-train/README.md), **324/500 training clears**, then **64/100 enabled versus 55/100 masked** frozen clears. The enabled gain over QA-21 is +3 points (-10 to +16); the waypoint gap increased by +4 points (-15 to +23). Neither establishes clear improvement or stronger waypoint reliance. QA-22 sessions are stopped; both checkpoints remain preserved.

[QA-19: final checkpoint and results](qa-waypoint-20261103-163554-train/README.md): **468/500 training clears (93.6%)**, then **100/100 frozen evaluation clears**, 4.323 seconds per exit on average, zero evaluation updates. This covers the open 20 x 20 room; advancement remains manual.

QA-19 SHA-256: `67c50b7f30f38eb7194979f4e63ec836c4ee1e45fecc7f2a89961f84631a03cd`.

[QA-21: larger 40 x 40 run](qa-waypoint-20261103-172142-train/README.md) completed **248/500 training clears**, then **61/100 frozen evaluation clears**. The [100-case matched comparison](qa-waypoint-20261103-172142-train/comparison-100/README.md) cleared **61/100 with waypoints versus 56/100 masked**; the paired difference interval spans -8 to +18 points. Before-training enabled score was **63/100**. Neither a training improvement nor clear waypoint benefit is established; waypoint dependence remains unresolved. This is a separate task from QA-19's 20 x 20 baseline; keep both checkpoints. QA-21 SHA-256: `de32a90630b506d97610a918cadea89039518720d0ae2e96c1af58a231e1fe6b`.

## Checkpoint inventory

| Checkpoint | Model steps in this workflow | Role |
| --- | --- | --- |
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
