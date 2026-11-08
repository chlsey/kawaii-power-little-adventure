# QA-27: nearby-waypoint skill training

**Complete: 500/500 training arrivals, followed by 100/100 frozen deterministic evaluation arrivals.** No timeouts or deaths. Arrival ends only this skill drill; all 600 records have `game_completed: false`. The farther 500-episode stage has not started.

| Phase | Arrivals | Mean arrival | Median | 90th percentile | Range |
| --- | ---: | ---: | ---: | ---: | ---: |
| Training, stochastic policy | 500/500 | 2.744 s | 1.50 s | 6.10 s | 0.70-7.30 s |
| Frozen deterministic evaluation | 100/100 | 2.605 s | 1.40 s | 5.71 s | 0.70-6.50 s |

Times are game seconds, 0.1 seconds per movement action. This stage added **13,718 training steps / 500 episodes**, with **530 optimizer updates**. Evaluation made **zero updates**; all policy tensors and the saved checkpoint hash remained unchanged. Parent actor lineage remains separate: 5,000 prior episodes / 613,364 steps; total lineage training episodes are now 5,500. The fresh critic/optimizer branch's model counter is 13,718 steps, not 627,082 steps.

## Scenario and reward hierarchy

Dedicated [scene](../../../../Assets/Scenes/QaWaypointReachTraining.unity), empty 40 x 40 room, fixed centre spawn, random angle and distance **3-6 units**, 12-unit vision, one fixed assigned waypoint and 0.5-unit arrival radius. Maximum 60 game seconds / 600 actions. There is no exit object, exit trigger or automatic navigator. The waypoint is not a sensed exit, and no +50 gameplay-completion reward is paid. The normal `local-waypoint-v1` encoding supplies the assigned instruction, current wall rays and previous movement; action contract remains two movement outputs.

Reward is **+1 arrival**, **+0.1 per unit of new closest approach**, **-0.001 per action**, with no exploration or stuck reward in this isolated drill. All positive waypoint rewards share a **+5 per-attempt budget**, including approach and arrival across target changes. This budget is below the configured **+50 minimum local-objective completion reward**. The highest positive total observed was **1.577555 in training / 1.580236 in evaluation**. No exit-sized reward is substituted for a waypoint arrival.

The shared tracker also applies the combined cap in exit-navigation scenes; only a new attempt refills its budget. Navigation resets and target assignment do not. The exit environment rejects an actual completion reward at or below its waypoint budget. If future local objectives have smaller rewards, lower the waypoint budget below the smallest active reward. Magnitude ordering does not establish learned priority under competing goals, combat or survival; those behaviors require their own scenarios and evaluation.

## Starting point and reproducibility

The preserved [QA-24 parent](../qa-waypoint-20261104-170721-train/README.md), SHA-256 `78548d2ca50e90f61b737e9f74a6bf983c7682ea96c2e431b94cf71810076c36`, supplies its actor, action head and log standard deviation. A [separate zero-step transfer](../qa-waypoint-reach-transfer-start/transfer.json) uses a fresh critic and optimizer for the waypoint-only reward scale. Parent checkpoint bytes are unchanged.

PPO: architecture 134 -> 64 -> 64 -> 2, learning rate 0.0002, entropy 0.01, gamma 0.99, rollout 256, batch 64, CPU. Training seeds **6000-6499**, frozen evaluation seeds **280000-280099**. Training stopped at exactly 500 episodes. No automatic curriculum advancement or model promotion.

Final model SHA-256: `3ab41e4c309ca405028f28728ac0b8619a8835e8d9744a83aa20e79d31118613`. The 10,000-step snapshot and final ZIP archives passed CRC, byte-count and hash checks. Project settings were restored and all training/evaluation Unity sessions stopped.

- [Actual config](training-config.yaml), [run/source provenance](run.json), [preflight verification](verification.json), [checkpoint manifest](checkpoints.json).
- [Training result](training-result.json), [500 training records](training-episodes.json), [frozen result](evaluation-result.json), [100 evaluation records](evaluation-episodes.json).
- [Runner](../../../../Training/run_waypoint_reach.py), [scenario guide](../../../../Assets/Scenes/QaSingleRoomTraining.README.md#waypoint-reaching-skill-drill). Detailed local-only logs: `Logs/qa-waypoint-reach-training/20261107-034515-train/`.
- Supplementary [shared exit-navigation verification](exit-navigation-verification.json) and [its source/config provenance](exit-navigation-verification-run.json): after preserving reward budgets through navigation resets, 600 placements, eight compass completions, 10/10 reference room clears and routing/recovery/reward checks passed. This uses a reference controller, not new learned-policy training or evaluation. Local-only log: `Logs/qa-player-waypoint-exit-priority/20261107-034745-verify/`.

Preflight checked 100 seeded placements across all quadrants, eight reference arrivals without game completion, the exact idle timeout, zero exit signals and the aggregate reward budget. The held-out result validates these nearby, unobstructed assignments only. There is no matched pre-training nearby evaluation, so no improvement attributable to these 500 episodes is established. QA-26's distant 13-18-unit test is a different task and is not a before/after comparison. Farther targets, obstacles and competing local objectives remain outside this stage.
