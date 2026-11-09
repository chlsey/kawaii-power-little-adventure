# Single-room QA player training

The current waypoint workflow uses the maintained batch runners below with Unity closed. Read the [orchestrator/local-policy design](../../Training/PCG-system-design.md#overall-navigation-and-action-design) for ownership, action order, recovery and local-objective priority. For historical manual workflows, choose a scene, press Play and start the external Python trainer with its matching config.

| Scene | Floor size | Config | TCP port |
| --- | --- | --- | --- |
| [QaWaypointReachTraining](QaWaypointReachTraining.unity) | Open 40 x 40; nearby assigned waypoint 3-6 units away; no exit | [qa-player-waypoint-reach-nearby.yaml](../../Training/qa-player-waypoint-reach-nearby.yaml) | 7783 |
| [QaWaypoint40Training](QaWaypoint40Training.unity) | Open 40 x 40; assigned waypoints, unseen exit 30-34 units away | [qa-player-waypoint-40x40.yaml](../../Training/qa-player-waypoint-40x40.yaml) | 7783 |
| [QaWaypoint20Training](QaWaypoint20Training.unity) | Open 20 x 20; assigned waypoints, unseen exit 14-16 units away | [qa-player-waypoint-20x20.yaml](../../Training/qa-player-waypoint-20x20.yaml) | 7783 |
| [QaLocalMovementTraining](QaLocalMovementTraining.unity) | 10 x 10; current vision only, no map input | [qa-player-local-movement.yaml](../../Training/qa-player-local-movement.yaml) | 7783 |
| [QaLocalMovement20Training](QaLocalMovement20Training.unity) | 20 x 20; visible exit 3-6 units away | [qa-player-local-movement-20x20.yaml](../../Training/qa-player-local-movement-20x20.yaml) | 7783 |
| [QaLocalMovementHidden40Training](QaLocalMovementHidden40Training.unity) | 40 x 40; exit starts outside vision, 14-18 units away | [qa-player-local-movement-hidden-40x40.yaml](../../Training/qa-player-local-movement-hidden-40x40.yaml) | 7783 |
| [QaMovementNoExploration10Training](QaMovementNoExploration10Training.unity) | 10 x 10; nearby exit; exploration reward disabled | [qa-player-no-exploration-10x10.yaml](../../Training/qa-player-no-exploration-10x10.yaml) | 7783 |
| [QaMovementStage1Training](QaMovementStage1Training.unity) | 40 x 40; exit 3-6 units away | [qa-player-movement-stage1.yaml](../../Training/qa-player-movement-stage1.yaml) | 7783 |
| [QaSingleRoomTraining](QaSingleRoomTraining.unity) | 10 x 10 | [qa-player-single-room.yaml](../../Training/qa-player-single-room.yaml) | 7781 |
| [QaSingleRoomLargeTraining](QaSingleRoomLargeTraining.unity) | 40 x 40 | [qa-player-single-room-large.yaml](../../Training/qa-player-single-room-large.yaml) | 7782 |

```powershell
python ../unity-reinforcement-learning/PythonEnv/train.py --config Training/qa-player-single-room.yaml
```

The shared RL training dependencies must be installed in the chosen Python environment. Each scene listens on its configured localhost port and waits for the trainer; these scenes contain no QA recorder or report adapter.

## Local movement experiment

Objective: reach a single-room exit using local vision and movement X/Y. `QaLocalMovementPolicyMapping` defines **local-vision-v1**, a **134 -> 64 -> 64 -> 2** policy. The 10 x 10 config starts fresh; later configs resume a compatible local-vision checkpoint and its optimizer. Grid checkpoints are incompatible. Keep 12-unit rays, 30 seconds/episode, learning rate 0.0002, entropy 0.01, and the existing completion/progress/time/stuck rewards; exploration reward remains zero.

The visible-exit stages place the exit 3-6 units away in 10 x 10 and 20 x 20 rooms. The hidden-exit stage continues the 20 x 20 checkpoint in a **40 x 40 room**, with randomized direction and **14-18 unit** spawn-to-exit distance. The 14-unit minimum leaves the 2 x 2 exit collider beyond the 12-unit rays. Both actors remain at least 1.5 units inside the floor edges. The config records the expected initial visibility and placement range; the launcher verifies them at runtime. No exit summary is supplied until a ray actually detects the exit.

| Input indexes | Encoding |
| --- | --- |
| 0-1 | Previous movement X/Y, each in [-1,1]. |
| 2-129 | 64 world-space rays starting at +Z and rotating toward +X. Each ray has separate wall and exit proximity channels: `1 - distance / 12` for the corresponding hit; otherwise zero. |
| 130-133 | Visible-exit flag, direction X, direction Z, normalized hit distance. Direction and distance describe the nearest current exit ray, not the true exit center. All four values are zero when no ray sees an exit. |

No health, inventory, combat actions, map cells, visited history, or remembered exit is supplied. The sensor's map can still be collected for diagnostics. The separate waypoint stage below uses this sensor memory through an external navigator.

From the game repository, with Unity closed:

```powershell
python -B Training/run_local_movement.py --config Training/qa-player-local-movement-hidden-40x40.yaml --mode train
```

This maintained command launches Unity, verifies the configured room size, exit distances, and initial visibility on 100 seeds, checks sensor-guided completion in all eight compass directions and hidden-exit signal clearing, then trains exactly 500 episodes and runs 100 frozen deterministic evaluations. Per-episode records also check the starting visibility and distance. Runtime records, source hashes and PPO metrics go under the config's `output_dir`; retained periodic/final checkpoints and completed model/config/results go under `SavedModels/qa/local-vision-v1/qa-local-movement-<run>/`. The maintained Editor entry point is `Assets/Editor/QaLocalMovementSession.cs`; no per-run scripts are generated. The hidden-exit experiment retains current-vision-only inputs and zero exploration reward, so it tests search/transfer without a new navigator or memory input. Review results before manually changing difficulty. The existing five-output deployment adapter does not support this contract yet.

## Waypoint navigation

Waypoint navigation uses a deterministic map/goal/route/recovery orchestrator and one learned local policy; the [QA changelog](../../Training/changelog/qa-player.md) records outcomes. The setup in this subsection reproduces the historical 20 x 20 exit task using [QaWaypoint20Training](QaWaypoint20Training.unity), or the maintained batch runner with Unity closed. Initial exits are randomized 14-16 units away and checked to be unseen by the unchanged 12-unit rays. All 600 prepared training/evaluation placements are checked during verification. There are no internal training barriers. The current skill curriculum and frozen distant-exit comparison are separate subsections below.

| Input indexes | `local-waypoint-v1` encoding |
| --- | --- |
| 0-1 | Previous movement X/Y. |
| 2-129 | Existing 64 wall/exit ray-channel pairs, in world X/Z. |
| 130 | Assigned waypoint valid. |
| 131-132 | World-X/Z direction to the assigned waypoint. |
| 133 | Waypoint distance / 12-unit sensor range. |

The 134 -> 64 -> 64 -> 2 actor receives neither map memory nor the distant route. The existing persistent grid supplies the replaceable goal selector and eight-neighbour A* planner; unknown cells and diagonal corner cutting are blocked. Observed walls are inflated by radius + skin width + 0.05. Reachable discovered exits supersede frontiers. The nearest reachable frontier must be at least one unit away, and completed/abandoned locations within 0.5 units remain excluded for the attempt.

The coordinator keeps each visible, actor-clear waypoint fixed until arrival (0.5 units), obstruction, or exit priority. Its default maximum assignment distance is 10 units; the historical recipes in this subsection use six via the batch runner's legacy fallback. Set `max_waypoint_distance` explicitly in a run config to change it, as in the [100-case 10-unit evaluation](../../Training/qa-player-waypoint-max10-evaluate.yaml). The furthest usable point is selected within that maximum; shorter points remain valid for turns and final approach. It validates at action boundaries and replans once after 20 actions without 0.05 units of progress. Repeated failure abandons the frontier or explores around a deferred exit. `no_reachable_goal` reports exhaustion; the real exit trigger alone marks completion. All navigation state resets with the sensor attempt.

When the sensors discover a reachable exit, the next 0.1-second action boundary replaces the exploration goal and its active waypoint with an exit route. An exit 6-12 units away is visible but still approached through local waypoints within six units; the observed exit cell becomes the waypoint when it is within reach and has usable clearance. The policy keeps the same waypoint inputs throughout and can also identify the visible exit through its ray channels. There is no separate exit-pursuit input flag. A discovered exit remains in map memory when it leaves current vision.

Recovery currently allows a repeated failed exit approach to defer that exit and select an exploration frontier, even while the exit remains visible. Completing an exploration leg makes the exit eligible again. Making a reachable exit remain the goal through recovery is a proposed navigation improvement; the exit-priority experiment keeps this behavior unchanged while testing a continuation with a larger terminal reward. Without a parallel unchanged-reward training arm, a before/after comparison cannot isolate the reward change from further learning.

| Event | Reward |
| --- | --- |
| Actual exit trigger | +50 default; run-owned `completion_reward` can override it |
| Assigned waypoint arrival | +1, once after approaching from outside its radius; no repeat within 0.5 units; shares the combined +5 waypoint budget |
| New closest approach to active waypoint | +0.1/unit; baseline reset on assignment; shares the combined +5 waypoint budget |
| Each action | -0.001 |
| Sustained requested movement without displacement | Existing -0.01/action after 20-action grace period |
| Newly explored space / target assignment | 0 |

Moving away adds no retreat penalty. Approach and all arrivals share a +5 budget across every waypoint in the attempt; target changes never refill it. This budget must be below the smallest active local-objective reward, and the current room environment rejects an actual completion reward of +5 or less. Waypoints are navigation instructions toward real gameplay objectives. In these exit-navigation scenes, waypoint arrival never completes an episode. Placement truth is used only for setup and diagnostic assertions, not goal selection, policy observations, or progress rewards. The isolated skill drill below ends its exercise on arrival without reporting gameplay completion.

The maintained runner passes `completion_reward` to the Editor session, which applies it to the environment at runtime and reports the actual value in its policy contract. Missing values retain +50. The [exit-priority retry config](../../Training/qa-player-waypoint-exit-priority.yaml) uses **+200 exit completion**. Arrival stays +1 and approach stays +0.1/unit, now sharing the combined +5 budget. Historical QA-19 through QA-26 records used the earlier arrival-only cap; their evidence is retained unchanged. Verification checks both the applied field and payout from real exit triggers. Checkpoint metadata records the training reward; observation/action semantics remain `local-waypoint-v1`.

```powershell
# Prepare the actor-only transfer, with a fresh critic and optimizer (no Unity or learning).
python -B Training/run_local_movement.py --config Training/qa-player-waypoint-20x20.yaml --mode prepare
# Reference controller, route/reward/reset/clearance checks; no learned-policy score.
python -B Training/run_local_movement.py --config Training/qa-player-waypoint-20x20.yaml --mode verify
# Ten deterministic frozen smoke episodes; no learning.
python -B Training/run_local_movement.py --config Training/qa-player-waypoint-20x20.yaml --mode evaluate --episodes 10
# Manual future launch: 500 training episodes then 100 frozen evaluations.
python -B Training/run_local_movement.py --config Training/qa-player-waypoint-20x20.yaml --mode train
```

The runner defaults to verification and requires an explicit mode for learning. `prepare` copies the policy MLP, action head and distribution log standard deviation from QA-17 (SHA-256 `76226e50dee7f0586b4c6b5b1bb8a3f3358980a5983fa712a7e0a413cc22833d`). Its 2,000 historical episodes / 198,495 steps are recorded separately; transfer training starts at zero steps with fresh critic/optimizer. The parent is hash-checked before and after every session. The checkpoint, config and Editor contract must all name `local-waypoint-v1`; historical exit-only models retain `local-vision-v1`. The ordinary shared trainer does not enforce this game-specific semantic contract, so use this maintained runner for waypoint sessions.

PPO retains learning rate 0.0002, entropy 0.01, gamma 0.99, rollout 256, batch 64, CPU, and 30-second episodes. The prepared budget is 500 training seeds 2000-2499, followed by 100 frozen seeds 200000-200099; `--episodes 10` evaluates the first ten without training. Session records include waypoint bonus, assignments, replans and abandonments. Evaluation compares all policy tensors, update count, checkpoint and parent hashes. Runtime diagnostics are local under `Logs/qa-player-waypoint-20x20/`; the [waypoint checkpoint folder](../../SavedModels/qa/local-waypoint-v1/README.md) retains periodic/final weights, episode records and summaries. Essential lineage lives beside the [transfer checkpoint](../../SavedModels/qa/local-waypoint-v1/qa-waypoint-transfer-start/transfer.json) and in the [QA changelog](../../Training/changelog/qa-player.md).

Reference verification uses the actual environment and observation-only direction actions. The fixed physical barrier and synthetic observed-map fixture test routing/clearance only and are removed before the open-room episodes. Reference results and frozen-policy smoke results must be reported separately. No stage advances automatically.

Combat interruption is **planned**: a future local system may evade, defend, or fight, preserve the navigation goal, then revalidate its route before resuming. Threat inputs, an action policy and survival evaluation are required. The current movement-only model cannot decide combat, and reward magnitudes alone do not establish survival priority. Generated-dungeon integration and standalone waypoint ONNX deployment remain deferred.

### Waypoint reaching skill drill

[QaWaypointReachTraining](QaWaypointReachTraining.unity) uses `QaWaypointReachRlEnvironment` in an empty 40 x 40 room with a fixed centre spawn and one random assigned waypoint in any direction. The scene defaults to **3-6 units**; the Editor session applies the selected config's distance band at runtime. There is no exit object or trigger, no navigation coordinator and no discovery bonus. The waypoint marker is not a sensed exit. The policy receives the existing `local-waypoint-v1` observation encoding through `QaWaypointPolicyMapping` and keeps its two movement actions.

Arrival within 0.5 units ends the **skill exercise**; `QaPlayerController.Metrics.Completed` stays false. Reward is +1 for a new arrival, +0.1/unit of new closest approach and -0.001/action, using the shared tracker with the combined +5 budget. The configured minimum local-objective reward is +50; startup rejects a budget greater than or equal to that floor. No +50 completion payout exists in the drill. There is no stuck penalty in this isolated exercise. Episodes time out after 60 game seconds / 600 actions. Future local objectives with smaller rewards require a smaller waypoint budget; reward magnitudes alone do not prove learned objective priority.

```powershell
# Creates the dedicated scene if missing, then verifies the drill without learning.
python -B Training/run_waypoint_reach.py --mode verify
# Reproduce the first nearby stage: 500 training episodes, then 100 frozen evaluations.
python -B Training/run_waypoint_reach.py --mode train
# Verify the selected 8-12-unit stage without learning.
python -B Training/run_waypoint_reach.py --config Training/qa-player-waypoint-reach-8to12.yaml --mode verify
# Reproduce its QA-31 continuation only through an explicit training launch.
python -B Training/run_waypoint_reach.py --config Training/qa-player-waypoint-reach-8to12.yaml --mode train
# Frozen original-QA-32 baseline on the configured 1-10-unit evaluation targets.
python -B Training/run_waypoint_reach.py --config Training/qa-player-waypoint-reach-1to10.yaml --mode evaluate
# Separate QA-32 -> QA-39 continuation: 500 episodes, then matched frozen child evaluation.
python -B Training/run_waypoint_reach.py --config Training/qa-player-waypoint-reach-1to10.yaml --mode train
```

The [nearby config](../../Training/qa-player-waypoint-reach-nearby.yaml) copies QA-24's actor, action head and log standard deviation into a fresh critic and optimizer; the parent checkpoint is preserved. Architecture, input semantics and PPO settings stay unchanged. Training seeds are 6000-6499, evaluation seeds 280000-280099. The 5,000 parent-lineage episodes remain separate from new training. The runner saves starting, periodic and final models with source/config provenance under `SavedModels/qa/local-waypoint-v1/`, then evaluates frozen weights. Per-episode records distinguish `reached` from `game_completed` and record the positive reward total. Use the [QA changelog](../../Training/changelog/qa-player.md) for measured outcomes.

The successful skill branch uses explicit full-checkpoint continuations after the actor-only transfer:

| Stage | Config | Parent -> retained checkpoint |
| --- | --- | --- |
| 3-6 units | [nearby](../../Training/qa-player-waypoint-reach-nearby.yaml) | QA-24 actor transfer -> QA-27 |
| 3-7 units | [gradual](../../Training/qa-player-waypoint-reach-gradual.yaml) | QA-27 -> QA-30 |
| 6-10 units | [medium](../../Training/qa-player-waypoint-reach-medium.yaml) | QA-30 -> QA-31 |
| 8-12 units | [8-12](../../Training/qa-player-waypoint-reach-8to12.yaml) | QA-31 -> QA-32, preserved 8-12-unit baseline |

The separate [1-10-unit recipe](../../Training/qa-player-waypoint-reach-1to10.yaml) pins original QA-32 as its parent for QA-39. `--mode evaluate` measures that configured parent without learning; it does not select the latest child automatically. `--mode train` saves and evaluates the new child in its own run folder. This fixed-target drill resets previous movement each episode and does not test changing navigator assignments while moving.

Each stage runs 500 training episodes and 100 frozen evaluations only when explicitly selected. The [checkpoint index](../../SavedModels/qa/local-waypoint-v1/README.md) records results, branch counters and provenance. No config automatically advances or resumes the newest timestamp.

The separate historical [farther config](../../Training/qa-player-waypoint-reach-farther.yaml) resumes the full [QA-27 nearby checkpoint](../../SavedModels/qa/local-waypoint-v1/qa-waypoint-reach-20261107-034515-train/README.md), including critic and optimizer, at 13,718 model steps and 5,500 lineage episodes. It increases target distance to **13-18 units**, keeping rewards, observation/action meanings, PPO settings and the 60-second limit unchanged. Training seeds are 6500-6999, frozen evaluation seeds 281000-281099. These targets begin beyond 12-unit vision, while assigned direction remains available and normalized distance clips at one. This is direct distant-waypoint following, not discovering a hidden objective or testing the normal navigator's six-unit assignments. That QA-28/29 branch remains preserved separately; its episodes are excluded from QA-30/31/32's counters.

### Random waypoint-chain curriculum

The [chain recipe](../../Training/qa-player-waypoint-chain-curriculum.yaml) resumes the full QA-39 checkpoint and explicitly selects four blocks: 100 episodes of two targets, 100 of three, 100 of four, then 200 with uniformly random length 1-10. `QaWaypointChainSession` adapts the existing empty 40 x 40 scene at runtime; it disables the exit and bypasses the navigator. It precomputes a fresh seeded bounded random walk for every episode. Nominal target separations are uniform 1.5-9.5 units, with points constrained to +/-15 X/Z; the 0.5-unit arrival tolerance keeps actual assignments 1-10 units away. The mixed block uses a separate length RNG, and evaluation reuses the exact ten-target benchmark coordinates.

Only chain start resets position, orientation, previous action and sensors. Each arrival immediately selects the next target with those states preserved. There is one 60-second / 600-action deadline for the whole chain, no exit payout and no exploration/replanning. During training, each leg uses the existing +0.1/new closest unit and +1 arrival divided by planned chain length, so every leg retains feedback within the combined +5 limit; action cost remains -0.001. Frozen diagnostics keep zero reward. Input/action semantics and PPO settings match the standalone drill.

From the game directory with other Unity sessions closed:

```powershell
# Random-length reference checks and reward accounting only; no learning.
python -B Training/run_waypoint_chain.py --config Training/qa-player-waypoint-chain-curriculum.yaml --mode verify
# Explicitly selected 500-episode curriculum, parent replay and frozen child comparison.
python -B Training/run_waypoint_chain.py --config Training/qa-player-waypoint-chain-curriculum.yaml --mode train
```

The separate [continuation recipe](../../Training/qa-player-waypoint-chain-continuation.yaml) pins QA-42 as parent, starts from 8,000 lineage episodes and selects fresh seeds 11500-11999 with the same four blocks. Launch it by passing that config to the same runner and explicit `--mode train`. It checks QA-42's saved evaluation as the parent replay; it does not resume QA-39 or auto-select a timestamp.

The runner preserves the configured parent, records exact seeds/coordinates and saves periodic plus per-block full checkpoints in a new `SavedModels/qa/local-waypoint-v1/qa-waypoint-chain-<timestamp>-train/` folder. Verification covers lengths 1-10 before learning; parent replay must reproduce that parent's configured 100-case chain outcomes/steps, and final evaluation uses the same cases with unchanged child tensors/counters. The original recipe's training seeds 11000-11499 are separate from benchmark seeds 288000-288099; the reused benchmark is a matched diagnostic, not an untouched test set. Intermediate PPO block stops discard an unfinished rollout rather than starting extra episodes. The [QA history](../../Training/changelog/qa-player.md) and [model index](../../SavedModels/qa/local-waypoint-v1/README.md) own outcomes. No later block beyond this explicit recipe or generated-dungeon stage follows automatically.

### Pre-waypoint restart and timed arrivals

The independent QA-44 branch starts from QA-18's zero-step transfer of the QA-17 local-exit actor, with no assigned-waypoint or navigator training, fresh critic and empty optimizer. It retains the 2,000 historical actor-lineage episodes separately from new learning. The [nearby restart](../../Training/qa-player-waypoint-restart-nearby.yaml), [middle](../../Training/qa-player-waypoint-restart-middle.yaml) and [8-10](../../Training/qa-player-waypoint-restart-8to10.yaml) recipes each run 500 single-target episodes plus 100 frozen cases through `run_waypoint_reach.py`. Bands are 3-6, 6-8 and 8-10 units. Each recipe pins the preceding stage's actual checkpoint/hash; QA-43 remains backed up independently.

The [timed recipe](../../Training/qa-player-waypoint-restart-throughput.yaml) continues that third stage for 500 episodes with `run_waypoint_throughput.py`. Each episode always lasts 60 game seconds / 600 actions. Arrival within 0.5 immediately selects another seeded random target without resetting player state, sensors or previous action. Primary outcome is arrivals per 60 seconds; there is no target-count completion threshold or exit. The bounded random walk has nominal legs 1.5-9.5 units and actual assignments 1-10, within +/-15 X/Z in the empty 40 x 40 room. Precomputing 601 targets exceeds all possible arrivals in 600 actions and preserves matched target sequences.

Timed reward is +0.015 per arrival, +0.0015 per new closest unit and -0.001/action. With minimum assignment one, speed four and 0.5 arrival tolerance, at least two actions are required per target: at most 300 arrivals plus 240 moved units produce <=4.86 positive credit. This fits the unchanged +5 attempt limit below the +50 objective floor while preserving feedback for every arrival. The first three stages retain the original single-target reward scale. Thus this curriculum comparison changes both exposures and timed reward scale; it is not a controlled proof of one input's effect.

With Unity closed, from the game directory:

```powershell
python -B Training/run_waypoint_reach.py --config Training/qa-player-waypoint-restart-nearby.yaml --mode train
python -B Training/run_waypoint_reach.py --config Training/qa-player-waypoint-restart-middle.yaml --mode train
python -B Training/run_waypoint_reach.py --config Training/qa-player-waypoint-restart-8to10.yaml --mode train
python -B Training/run_waypoint_throughput.py --config Training/qa-player-waypoint-restart-throughput.yaml --mode verify
python -B Training/run_waypoint_throughput.py --config Training/qa-player-waypoint-restart-throughput.yaml --mode train
```

These commands reproduce pinned stages rather than select a fresh run's model automatically. Frozen timed comparisons use QA-43, the new third-stage model and final child on the same 100 seeds. QA-43's first-ten arrival outcomes and timing must match its earlier fixed-ten benchmark. Reference and idle checks verify full time budgets and reward accounting before learning. Timed frozen episodes use zero reward and fixed weights. Production navigator/exit integration and causal previous-action ablations remain separate work. The [training history](../../Training/changelog/qa-player.md) owns results and all manually authorized stage counts.

### Frozen distant-exit integration test

The [distant-exit test config](../../Training/qa-player-waypoint-distant-exit-test.yaml) loads the saved QA-32 8-12-unit skill checkpoint and tests the existing navigator in [QaWaypoint40Training](QaWaypoint40Training.unity). Spawn and exit are 30-34 units apart, initially beyond the 12-unit vision range. Sensor memory, frontier selection, observed-space routing and the normal visible/clear waypoints within six units are active. Only the actual exit trigger completes the room; waypoint arrival never substitutes for it.

```powershell
# Separate environment/reference checks; these are not learned-policy results.
python -B Training/run_local_movement.py --config Training/qa-player-waypoint-distant-exit-test.yaml --mode verify
# Exactly 500 deterministic frozen attempts; no training or optimizer updates.
python -B Training/run_local_movement.py --config Training/qa-player-waypoint-distant-exit-test.yaml --mode evaluate --episodes 500
# Matched no-navigator control: same saved policy, real rays, exit conditions and 500 seeds.
python -B Training/run_local_movement.py --config Training/qa-player-waypoint-distant-exit-no-navigator.yaml --mode verify
python -B Training/run_local_movement.py --config Training/qa-player-waypoint-distant-exit-no-navigator.yaml --mode evaluate --episodes 500
```

The run-owned deadline is 60 game seconds / 600 actions; the reusable scene retains its original 30-second value. `episode_seconds` defaults to 30 for older configs, and `time_scale` defaults to 20; the test selects 60 and 100 respectively. Readiness records the actual applied deadline and Python checks it. Episode records include waypoint assignments, replans, abandoned goals, final navigation status and whether the observed map discovered an exit. Discovery diagnostics read sensor memory, not the hidden scene destination. The existing recovery behavior can defer a repeatedly failed exit approach; this test does not change the navigator.

No training episodes are configured. The checkpoint, tensors and counters are checked unchanged; model lineage stays at 7,000 training episodes. The actual exit reward remains +50 and the combined waypoint reward budget +5, but evaluation rewards do not update weights. This is an empty-room integration test, not generated-dungeon, obstacle or combat validation. The [QA changelog](../../Training/changelog/qa-player.md) owns measured results; keep the saved skill checkpoint separately from frozen-test evidence.

The [navigator-disabled control](../../Training/qa-player-waypoint-distant-exit-no-navigator.yaml) sets the evaluation-only `navigator_disabled` flag (default false). The environment bypasses goal selection, route planning and recovery, leaving the four assigned-waypoint features zero while preserving current wall/exit rays, prior movement, real exit triggers, seeded placements and the deadline. It does not substitute the historical direct-exit summary. Sensor memory remains available for diagnostics but does not select goals or enter the policy observation. Readiness reports the actual flag, every policy step checks the zero waypoint features, and episode records require zero assignments/replans/abandonments/waypoint bonus. This is an ablation of the same waypoint-trained policy, not a comparison against a separately trained navigator-free agent. The Python-only `--without-waypoint` control described elsewhere still leaves the navigator running; the new flag actually disables it.

### Larger room: waypoint continuation

[QaWaypoint40Training](QaWaypoint40Training.unity) expands the open floor from 20 x 20 to **40 x 40** and spawn-to-exit distance from 14-16 to **30-34 units**. Vision stays at **12 units**, and each assigned waypoint stays within **six units**. The exit starts more than two sensor ranges away. Reference verification requires several local waypoint handoffs before discovery; this geometry alone does not prove learned-policy dependence on those inputs. The actual exit trigger still determines completion. No barriers or combat are added.

The [40 x 40 config](../../Training/qa-player-waypoint-40x40.yaml) continues the full QA-19 `local-waypoint-v1` checkpoint, including its critic and optimizer, at 36,286 model steps. It verifies SHA-256 `67c50b7f30f38eb7194979f4e63ec836c4ee1e45fecc7f2a89961f84631a03cd` and preserves that parent. The 2,500 prior lineage episodes remain separate from any new stage episodes. This is a compatible continuation; it does not repeat the earlier actor-only transfer or reset the critic.

Keep the same 134 -> 64 -> 64 -> 2 architecture, rewards, 30-second episode limit and PPO settings. The prepared training budget is 500 episodes on seeds 2500-2999, followed by 100 frozen evaluations on seeds 250000-250099. Stage advancement is manual; reference verification and frozen comparisons do not start training.

```powershell
python -B Training/run_local_movement.py --config Training/qa-player-waypoint-40x40.yaml --mode verify
python -B Training/run_local_movement.py --config Training/qa-player-waypoint-40x40.yaml --mode evaluate --episodes 10
# Matched frozen control: zero only the assigned-waypoint fields, keep current rays and previous movement.
python -B Training/run_local_movement.py --config Training/qa-player-waypoint-40x40.yaml --mode evaluate --episodes 10 --without-waypoint
# Explicit training launch, when requested:
python -B Training/run_local_movement.py --config Training/qa-player-waypoint-40x40.yaml --mode train
```

The reference check validates all 600 configured placements and records the minimum waypoint handoffs before exit discovery. The learned-policy comparison uses the same seeds, placements and frozen weights; masking affects inputs 130-133 only. This checks dependence on the assigned-waypoint signal in this task. QA-21's extended [100-case comparison](../../SavedModels/qa/local-waypoint-v1/qa-waypoint-20261103-172142-train/comparison-100/README.md) shows an uncertain difference when the signal is enabled. The waypoint-dependence criterion remains unmet; the original ten-case masked advantage did not persist in the full set. It does not establish obstacle navigation or combat readiness. The [QA changelog](../../Training/changelog/qa-player.md) owns measured outcomes; retained checkpoints remain in the same [waypoint architecture folder](../../SavedModels/qa/local-waypoint-v1/README.md).

## Manual movement curriculum: stage 1

The two historical grid-based movement scenes use **37,771 observations and two outputs: movement X/Y**. Attack, interact, and ability commands are disabled. `QaMovementNoExploration10Training` retains the earlier 10 x 10 task; `QaMovementStage1Training` retains the earlier 40 x 40 task. Existing five-output checkpoints cannot resume these policies, and the five-output inference adapter needs a matching movement-only adapter before using an export.

Each seeded reset places the exit **3-6 units away at a random angle**, then samples a player position keeping both actors at least 1.5 units inside the room. The exit begins within the unchanged 12-unit sensor range and in unobstructed view. Episodes end on completion, death, or **30 simulated seconds / 300 movement actions**. The current 10 x 10 scene disables all exploration reward settings while retaining sensors, progress/completion rewards, and the stuck penalty.

The config uses learning rate **0.0002**, entropy coefficient **0.01**, and `resume_from: null`. Its 150,000-step budget covers 500 maximum-length episodes; the ordinary trainer does not interpret `max_episodes`. Exact 500-episode sessions use the stopping callback and final-reset suppression described below, and save their config, scene verification, and per-episode records under `Logs/qa-player-movement-stage1-sessions/`. Remove temporary session and analysis scripts after use.

After an episode-limited run, evaluate the frozen checkpoint with deterministic actions on 100 separate seeds `100000..100099`. These attempts perform no learning updates. Record success rate, timeouts, and successful exit times separately from training results. Evaluation reports require manual review and do not change the stage.

**Advance manually only after reviewing reliable success in the separate evaluation.** The current campaign plans 500 episodes each at 10 x 10, 20 x 20, and 30 x 30, continuing the checkpoint between sizes. Later tasks can increase distance, hide exits, or add obstacles. Version copied scenes and preserve a compatible two-output contract for resume. The nearby-placement bounds must fit both actors with 1.5-unit clearance; validate new geometry before training. See the [changelog](../../Training/changelog/qa-player.md) for run settings and results.

## Task and reset

- One fixed room with four solid, sensor-marked walls and no internal obstacles. The large variant has four times the side length and 16 times the floor area.
- In the two legacy single-room scenes, each reset samples both the player spawn and exit position. Both stay at least 1.5 units inside the room boundary and at least 3 units apart. Stage 1 uses the nearby-exit sampling described above.
- The green 2 x 2 exit marker has a trigger matching the dungeon's exit dimensions. Its sensor classification is LevelEnd; walls are Wall.
- Reaching the exit, death, or the configured action-time limit ends an episode: 30 seconds in stage 1 and 100 seconds in the legacy scenes. The trainer controls the next reset; actors and room geometry are reused.

```text
+----------------------------------------------------------+
| Python resets with seed                                  |
+----------------------------------------------------------+
                |
                v
+----------------------------------------------------------+
| Sample player/exit positions; clear sensor map           |
+----------------------------------------------------------+
                |
                v
+----------------------------------------------------------+
| Observe -> policy action -> physics -> reward / done     |
+----------------------------------------------------------+
                |
                +-- Continue --> Next policy action
                |
                +-- Ended ----> Trainer resets next episode
```

## Model contract and rewards

Single-room training also subtracts 0.01 reward per action after 20 consecutive movement actions (2 simulated seconds) within 0.05 horizontal units of an anchor position. The penalty continues while blocked and resets when the player moves at least 0.05 units from the anchor or stops requesting movement. It uses attempted movement and actual displacement, without requiring hidden obstacle geometry. Successful completion and death do not receive this penalty. This reward is separate from the controller's periodic `StuckEvents` diagnostic counter; full-dungeon training retains its existing rewards.

`QaSingleRoomRlEnvironment` uses the existing `QaPlayerController`, `ModelSensorPackage`, and observation encoding: 37,771 inputs, with 0.1 simulated seconds per action. The legacy scenes use the five-action `QaPlayerPolicyMapping`; stage 1 selects movement-only decoding with two outputs. Exit position is not supplied directly to the model; perception comes from its rays and discovered grid.

Rewards match dungeon training: +50 completion, +0.1 per world unit of new closest approach toward a ray-visible exit, -0.001 per action, and +0.001 per newly viewed open cell capped at +0.02 per action and +2 per attempt. Spawn views, wall/exit hit cells, visits, and repeated views earn no exploration bonus; visible exits take priority. The sensor range stays at 12 units in both variants. The small room keeps the exit within range throughout the valid spawn area; the large room can begin with the exit out of sight and requires searching the room. See [the sensor contract](../PCGDungeon/Scripts/QA/ModelSensorPackage.README.md).

The game-owned configs start fresh PPO policies and save checkpoints in separate `Logs/qa-player-single-room-runs/` and `Logs/qa-player-single-room-view-runs/` directories. Set `resume_from` to a compatible checkpoint when continuing a policy. The small-room config requests 500,000 timesteps; the large-room config requests 1,000,000 with `ent_coef: 0.01`. These budgets are not episode-count guarantees: early completions produce more episodes, and PPO completes its final rollout. Exact episode-limited sessions use an episode-count stopping callback and suppress the vector environment's unused reset after the final episode. Keep session configuration, runner snapshots, per-episode metrics, and results in game-owned `Logs/` session directories.

The [full-dungeon training scene](QaPlayerTraining.unity) remains a separate task with its own config and run artifacts.
