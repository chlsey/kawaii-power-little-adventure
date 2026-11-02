# QA player training change log

Updated through 2026-11-02. Dates below follow the UTC run records where available. This history tracks the player-control agent used for gameplay QA; the QA report receiver/evaluator is a separate framework.

## Current position

- New campaign: exploration reward disabled in a dedicated 10 x 10 scene; fresh movement-only policy, followed by 20 x 20 and 30 x 30 continuations, **500 training episodes at each size**. The 10 x 10 session launched at 17:05 UTC on Nov 2, passed runtime preflight, and is training. Partial results are recorded in QA-11 below. Advancement remains manual.
- Current contract: 37,771 observations and two movement outputs, with 0.1 simulated seconds per action and a 30-second episode limit.
- Previous 40 x 40 lineage completed 1,000 training episodes across two 500-episode runs. Both training runs and their separate 100-attempt evaluations are complete. The new room-size campaign starts independently of these checkpoints.
- The first stage-1 checkpoint scored 19/100 in deterministic evaluation; its continuation scored 13/100 on the same seeds. Preserve both; the first is the better candidate on this evaluation set, not an established reliable navigation policy.
- The earlier 71.4% result below is a training rate in a different 10 x 10 task. It is not a held-out evaluation of the current task.

## Interface and perception changes

These changes establish the training pipeline. Verification outcomes are distinct from learned-policy performance.

| Change | Result / evidence |
| --- | --- |
| Separate the shared RL interface from game mechanics: `RlEnvironmentBehaviour` defines reset, observation, action, action duration, and reward/done; `RlTrainingHost` bridges Unity to Python; `UnityGymEnv` exposes it to PPO/A2C. | Training, checkpoint reload, and export/inference paths operate end to end. The game owns observations, rewards, configs, and model artifacts. See the [RL design](../../../../google-docs/reinforcement-learning-framework-system-design.md) and [training guide](../README.md). |
| Give training and local inference a shared observation/action mapping in `QaPlayerPolicyMapping`. | The retained 119-input policies' Python/ONNX/Unity actions agreed within the recorded tolerances, even though those policies did not clear levels. [Baseline deployment evidence](../../Assets/DemoModels/QAPlayerBaseline/deployment-check.json). |
| Make QA reporting opt-in through `DungeonQaAdapter`, state-source interfaces, and a Python recorder/evaluator, instead of using QA transport to control training. | Controlled reporting and training-without-reporting checks passed. The two-episode grid smoke contained zero QA reporting components. Reporting provides evidence, not automatic learning feedback. [QA guide](../../QA/README.md), [smoke result](../../Logs/qa-opt-in-smoke/20261024-175910/result.json). |
| Move perception out of `QaPlayerController` into `ModelSensorPackage`, with marked wall/exit targets, world-space rays, and explicit attempt lifecycle. | The first package prototype used 16 `(hit identifier, distance)` rays: 39 inputs including seven gameplay values. Boundary visibility needed correction during verification; the final checks found all 16 expected wall hits. [Verification](../../Logs/model-sensors/20261024-132548/verification.json). No learning-rate improvement was established by this check. |
| Increase that package prototype from 16 to 32 rays. | Input count became 71; geometry, runtime, and Python reset/step checks passed. This was verification-only, not a separate trained success result. [Verification](../../Logs/model-sensors-32/20261024-161254/verification.json). |
| Add 64 rays plus a persistent discovered grid. Cells are 0.25 units with Observed/Wall/Exit/Visited flags; the model receives a 97 x 97 x 4 nearby window. | Contract became 37,771 inputs and five outputs. Grid lifecycle, encoding, reward cases, and bridge/inference checks passed. Old 119-input checkpoints are incompatible and are rejected by the runner. [Verification](../../Logs/qa-grid/20261024-165659/verification.json), [sensor contract](../../Assets/PCGDungeon/Scripts/QA/ModelSensorPackage.README.md). |
| Reward new closest approach to a ray-sensed exit; prevent retreat/revisit/visibility changes from repaying earlier progress. | Controlled approach and terminal-trigger checks passed. These early checks used a 39-input prototype and approximately +10 completion reward; the later/current recipe uses +50. [Reward verification](../../Logs/visible-exit-reward/20261024-153936/verification.json). |
| Add reward for newly ray-revealed open cells, excluding the spawn scan, wall/exit hits, and revisits; suppress it while the exit is visible. | Grid/reward verification and smoke training passed. In the current recipe, the bonus is +0.001 per cell, capped at +0.02/action and +2/episode. Later diagnosis found that leaving a visible target can still earn exploration reward once it leaves view; see diagnostics below. |

The full grid observation flattens into feedforward actor and critic networks, each with two 64-unit hidden layers. This is a narrow representation with roughly 4.8 million total parameters for the current movement-only policy, not proof that parameter count is insufficient. No convolutional grid encoder, compact exit-vector observation, or recurrent policy has been tried in the runs below.

## Experiment sequence: change -> result

| ID / UTC date | Change and starting point | Training result | Separate evaluation / interpretation |
| --- | --- | --- | --- |
| QA-01 / Oct 24 | Historical 119-input, five-output full-dungeon baseline; PPO, learning rate 0.0003. | 100,096 steps; 0/100 clears. | 0/20 held-out clears; two standalone attempts also timed out. Deployment agreement passed, learning goal did not. [Model record](../../Assets/DemoModels/QAPlayerBaseline/README.md). |
| QA-02 / Oct 24 | Fresh historical 119-input policy; increase training budget to about one million steps. | 1,000,192 steps; 0/1,000 clears. | 0/20 held-out clears; separate Unity QA batch 0/100. Increasing the budget did not solve this task. [Model record](../../Assets/DemoModels/QAPlayer1M/README.md). |
| QA-03 / Oct 24-25 | Start a compatible 37,771-input, five-output grid policy for the full dungeon. | Pipeline smoke: 0/2 clears in 2,000 steps. Full campaign: 0/500 clears, all timeouts. | Smoke verified reload and finite actions, not level clearing. Full campaign recovered from Unity interruptions; its continuation chunks are part of the same 500 episodes. [Smoke](../../Logs/qa-opt-in-smoke/20261024-175910/result.json), [campaign](../../Logs/qa-player-grid-500-episodes/20261024-192151/result.json). |
| QA-04 / Oct 25 | Simplify to an empty 10 x 10 room; random player/exit positions; fresh grid policy, five outputs, 100-second limit, learning rate 0.0003, entropy not explicitly configured. | **357/500 clears (71.4%)**; 143 timeouts; successful exits averaged 15.30 seconds; 197,619 steps. | No separate held-out evaluation is recorded for this campaign. It established training success in the easier room. [Config](../../Logs/qa-player-single-room-sessions/20261025-112910/training-config.yaml), [result](../../Logs/qa-player-single-room-sessions/20261025-112910/result.json). |
| QA-05 / Oct 25-26 | Resume QA-04 and enlarge the room from 10 x 10 to 40 x 40. Retain 12-unit sensing, five outputs, 100-second limit, and learning rate 0.0003. Exit can start out of view. | **15/500 clears (3%)**; 485 timeouts; successful exits averaged 9.06 seconds. Total checkpoint steps reached 684,196. | Weak transfer to the larger search task. The final 55-episode conditional/recovery segment is already included in this result, not an additional 55-episode experiment. [Config](../../Logs/qa-player-single-room-large-sessions/20261025-145124/training-config.yaml), [aggregate result](../../Logs/qa-player-single-room-large-sessions/20261025-145124/result.json). |
| QA-06 / Oct 26 | Start fresh in the 40 x 40 room with viewed-open-cell exploration rewards and explicit entropy coefficient **0.01**. Learning rate 0.0003, five outputs, 100-second limit. | **114/1,000 clears (11.4%)**; 886 timeouts, no deaths; successful exits averaged 29.53 seconds. Saved model has 920,658 steps. | No independent frozen-policy evaluation is recorded. Multiple changes plus a fresh start prevent attributing improvement specifically to entropy. [Config](../../SavedModels/qa-player-single-room-view-entropy-20261026/original-training-config.yaml), [result](../../SavedModels/qa-player-single-room-view-entropy-20261026/result.json). |
| QA-07 / Oct 31 | Continue a copy of QA-06 with unchanged task/settings on seeds 1000-1999, targeting another 1,000 episodes. | **Halted at 559 episodes: 24 clears (4.3%)**. The first four 100-episode blocks scored 7, 10, 2, and 2 clears. Recovery checkpoint preserved. | Stopped at the user's request after declining success and increasing stuck metrics. This branch is not the parent of the later stuck-penalty run. [Halted summary](../../Logs/qa-player-view-entropy-continuation/20261031-020553/halted.json). |
| QA-08 / Oct 31-Nov 1 | Restart from the original QA-06 checkpoint, not QA-07. Add a sustained blocked-movement penalty; retain learning rate 0.0003, entropy 0.01, five outputs, 40 x 40 room, and 100-second limit. | **92/1,000 clears (9.2%)**; 908 timeouts, no deaths; successful exits averaged 26.84 seconds. Last two hundreds scored 11 and 13 clears. | Penalty applied to 578 actions, totaling about -5.78 reward across the campaign. No independent policy evaluation is recorded. Training rates on different seeds do not establish whether the penalty helped. [Result](../../SavedModels/qa-player-single-room-stuck-penalty-20261031-221905/result.json). |
| QA-09 / Nov 1 | Fresh manual stage 1: restrict exit to visible 3-6 unit placements in random directions within the same 40 x 40 room; **five outputs -> two movement outputs**; **100 -> 30 seconds**; **learning rate 0.0003 -> 0.0002**. Retain 37,771 inputs, entropy 0.01, and existing rewards including exploration/stuck terms. | **78/500 clears (15.6%)**; 422 timeouts, no deaths; 129,093 steps. Successful exits averaged 3.20 seconds. | Frozen deterministic policy: **19/100 clears (19%)**, 81 timeouts, on seeds 100000-100099. Successful evaluation exits averaged 0.88 seconds. Several settings changed together, so their individual effects are unmeasured. [Config](../../SavedModels/qa-player-movement-stage1-20261101-190054/training-config.yaml), [result/evaluation](../../SavedModels/qa-player-movement-stage1-20261101-190054/result.json). |
| QA-10 / Nov 2 | Resume QA-09 for 500 more episodes, keeping stage, observations, two outputs, rewards, learning rate 0.0002, entropy 0.01, and 30-second limit unchanged. Training seeds 500-999. | **63/500 clears (12.6%)**; 437 timeouts, no deaths. Model reaches 1,000 lineage episodes and 261,043 steps. Successful training exits averaged 1.35 seconds. | Same frozen deterministic evaluation seeds: **13/100 clears (13%)**, 87 timeouts; successful exits averaged 0.83 seconds. The checkpoint did worse on this fixed set. It lost 14 previously successful seeds, gained 8, and retained 5. [Config](../../SavedModels/qa-player-movement-stage1-20261102-024341/training-config.yaml), [result/evaluation](../../SavedModels/qa-player-movement-stage1-20261102-024341/result.json). |
| QA-11 / Nov 2 | Fresh 10 x 10 movement-only policy with exploration reward disabled. Keep 37,771 inputs, two outputs, learning rate 0.0002, entropy 0.01, 30-second limit, completion/progress rewards, and stuck penalty. Training seeds 0-499; 500 complete episodes. | **Running.** Snapshot 17:21 UTC Nov 2: 65/500 complete, 24 successes, 41 timeouts, 14,500 steps; still running. Exploration reward remains zero. [Config](../qa-player-no-exploration-10x10.yaml), [run metadata](../../Logs/qa-player-no-exploration-sessions/20261102-170309-10x10/run.json). | Separate 100-seed frozen evaluation planned. Fresh initialization, smaller room, reward removal, and placement adaptation change together; this experiment cannot isolate the effect of reward removal alone. |
| QA-12 / planned | Continue QA-11's checkpoint in a 20 x 20 room for 500 episodes after manual advancement. | **Not started.** | Record actual parent checkpoint, seeds, settings, and evaluation when launched. |
| QA-13 / planned | Continue QA-12's checkpoint in a 30 x 30 room for 500 episodes after manual advancement. | **Not started.** | Planned campaign total: 1,500 training episodes; evaluations are additional attempts without learning. |

QA-09 starts a new lineage: the earlier five-output checkpoints cannot resume a two-output policy through the existing loader. The learning-rate reduction that actually ran was **0.0003 -> 0.0002**. QA-10 continued that rate; it did not reduce it again.

### QA-11 campaign settings and evidence

- **Lineage:** starts from random weights (`resume_from: null`, zero initial model steps); neither QA-09 nor QA-10 is its parent. Retain the existing flattened feedforward architecture with two 64-unit hidden layers for actor and critic.
- **Curriculum:** 10 x 10 -> 20 x 20 -> 30 x 30; 500 training episodes per size, 1,500 planned training episodes total. Continue the completed checkpoint between room sizes only after manual advancement. QA-12 and QA-13 have not started.
- **Placement and vision:** retain initially visible exits 3-6 units away with randomized direction, as confirmed by the user. Vision remains **12 units** with 64 rays and the existing 97 x 97 grid window; the briefly requested vision expansion was withdrawn before any sensor change. The earlier spawn margin (1.5 + maximum exit distance) cannot fit a 10 x 10 room. Nearby placement now chooses a direction/distance first, then samples the intersection of valid spawn and translated exit bounds, keeping both actors 1.5 units inside the floor edges. This preserves all directions without placing actors outside the small room. The legacy non-nearby placement branch is unchanged.
- **Reward:** dedicated scene sets discovery reward per cell, per-action cap, and per-attempt cap to **zero**. Sensor/map collection remains enabled. Keep completion +50, time cost -0.001/action, visible best-approach +0.1/unit, and the existing sustained-stuck penalty (-0.01/action after its grace period). No signed retreat penalty was added. Historical scenes retain their serialized exploration settings.
- **Training:** PPO, learning rate 0.0002, gamma 0.99, entropy 0.01, rollout 256, batch 64, CPU, 37,771 inputs, two movement outputs, 0.1 seconds/action, 30 seconds/episode. Training seeds 0-499; hard episode stop at 500 complete episodes; timestep ceiling 150,000. No automatic next-stage launch.
- **Evaluation:** planned frozen deterministic policy on seeds 100000-100099, 100 additional attempts, no optimizer updates. Review success, timeouts, direction distribution, and successful exit times separately from training reward.
- **Launch verification passed:** Unity compiled; 100 reset seeds all had visible exits 3-6 units away, all four direction quadrants were represented, and both actors remained inside the 10 x 10 room. Idle timeout occurred after exactly 300 actions / 30 seconds with only time cost; controlled movement reached the exit with no attack/interact/ability use. All three exploration settings were zero and no exploration reward was earned. [Runtime verification](../../Logs/qa-player-no-exploration-sessions/20261102-170309-10x10/verification.json). These are environment checks, not learned-policy success results.
- **Local evidence:** [scene](../../Assets/Scenes/QaMovementNoExploration10Training.unity), [environment implementation](../../Assets/PCGDungeon/Scripts/RL/QaSingleRoomRlEnvironment.cs), [run-owned config](../../Logs/qa-player-no-exploration-sessions/20261102-170309-10x10/training-config.yaml), [source hashes and settings](../../Logs/qa-player-no-exploration-sessions/20261102-170309-10x10/run.json). Runtime progress/results under this `Logs/` directory are local-only; essential outcomes must be copied into this entry when available.

## Iteration trends

Counts are successes per consecutive 100 **training** episodes within each campaign. They are not frozen-policy scores.

| Campaign | Successes per hundred |
| --- | --- |
| QA-09: fresh movement-only stage 1 | 12, 17, 15, 13, 21 |
| QA-10: same-stage continuation | 18, 13, 10, 10, 12 |

The independent stage-1 comparison is **19/100 -> 13/100** on the same evaluation seeds. A 100-attempt evaluation has sampling limits; the larger issue is that both policies fail most attempts in the easy task. Short exit times describe successful attempts only and do not offset the high timeout rate.

## What the stuck penalty actually changed

The controller's existing `StuckEvents` counter checks displacement every 20 actions and increments when net movement is less than 0.05 units. It is a diagnostic count, not a count of distinct collisions.

QA-08 added a separate reward rule: after 20 consecutive requested-movement actions within 0.05 horizontal units of an anchor, subtract **0.01 per action** until displacement reaches that threshold or movement is no longer requested. Episode reset clears the timer and totals; completion/death do not receive this penalty. Runtime checks verified the grace period, wall pushing, escape, and reset behavior. [Verification](../../Logs/qa-player-stuck-penalty-continuation/20261031-073035/reward-verification.json).

This narrower detector can reset despite high periodic stuck counts. Its low activation rate does not prove that wall-pushing behavior disappeared. A rolling requested-versus-actual movement detector has been discussed but not implemented.

## Latest diagnosis: observations versus hypotheses

The following uses the two stage-1 policies' saved per-seed evaluation records. Each direction quadrant contains 25 placements. Directions are world X/Z relative to spawn.

| Exit direction | QA-09 successes | QA-10 successes |
| --- | --- | --- |
| -X, -Z | 14/25 | 11/25 |
| -X, +Z | 5/25 | 0/25 |
| +X, -Z | 0/25 | 2/25 |
| +X, +Z | 0/25 | 0/25 |

**Observed:** QA-10 failed all 50 positive-Z placements. This is a directional weakness in outcomes; it does not by itself identify a sensor sign bug or prove a fixed movement command. Sources: [QA-09 evaluation records](../../Logs/qa-player-movement-stage1-sessions/20261101-190054/evaluation-episodes.jsonl), [QA-10 evaluation records](../../Logs/qa-player-movement-stage1-sessions/20261102-024341/evaluation/episodes.jsonl).

**Observed:** a QA-10 training timeout on seed 500 started 3.84 units from the visible exit, finished 33.43 units away, and still earned about **+1.7**: +2 exploration reward minus the 0.3 time cost. There is no signed retreat penalty in the current reward. [Episode record](../../Logs/qa-player-movement-stage1-sessions/20261102-024341/episode-0.json).

**Hypotheses, not isolated experimental results:** exploration can compete with approaching a visible target; the flattened grid may make target extraction difficult; noisy actions or policy updates may disrupt useful behavior. Current evidence does not establish which mechanism caused the regression.

## Proposed changes: not yet tried

| Proposal | Intended question / status |
| --- | --- |
| Verify sensor direction and controlled movement in all four quadrants. | Does observation/action alignment explain the directional failures? Existing preflight verified exit visibility across 100 seeds, a 300-action timeout, and one controlled completion, not a full directional policy diagnosis. [Preflight](../../Logs/qa-player-movement-stage1-sessions/20261101-190054/verification.json). |
| Disable exploration reward for the visible-exit stage; consider feedback for net approach and retreat. | Exploration removal is implemented in QA-11; its result is pending. Signed net-progress/retreat feedback remains proposed and unimplemented. |
| Supply compact sensor-derived exit direction/distance and actual movement feedback, or use a spatial grid encoder. | Can the policy learn target-directed movement more reliably? Not implemented; changing the observation contract needs explicit transfer or a fresh model. |
| Change entropy over training or add a PPO update-divergence limit. | Can training preserve useful behavior? Entropy 0.01 remained enabled in the recent runs; no schedule or update-divergence limit was tried. |
| Expand one policy with weight transfer, or split movement and non-movement action policies. | Alternative designs only; neither migration nor a specialist-policy coordinator is implemented. [Strategy record](../PCG-system-design.md#policy-strategy-options). |

Advancement remains manual. Larger distances, hidden exits, and obstacle stages follow only after reviewing reliable success on the separate evaluation. The current stage-1 policies have not established that reliability.

## Run recovery and accounting

- QA-06 completed 688 recorded episodes before logging recovery supplied the remaining 312. Count the aggregate once; the interrupted seed was retried.
- QA-08 completed 838 recorded episodes before a late terminal callback caused a movement-step/request-count mismatch. Recovery supplied 162 more, retrying seed 1838. These are the same 1,000-episode campaign.
- QA-09 training completed normally. Evaluation hit a temporary Windows read lock; 78 native attempt records were preserved and 22 remaining seeds were evaluated with the same frozen model. Final evaluation totals are 100, not 178.
- Episode-limited session callbacks stop after complete episodes and suppress the unused final vector-environment reset. Ordinary CLI timestep budgets alone do not guarantee episode counts.
- Some aggregate `result.json` summaries retain recovery-session fields. Use aggregate episode records and checkpoint lineage to interpret campaign-wide seed ranges and totals; do not sum aggregate and segment results.

Detailed runtime/scene instructions remain in the [single-room guide](../../Assets/Scenes/QaSingleRoomTraining.README.md), [sensor contract](../../Assets/PCGDungeon/Scripts/QA/ModelSensorPackage.README.md), and [training guide](../README.md).
