# QA-31: shift the full waypoint band to 6-10 units

**Complete: 500/500 training arrivals and 100/100 frozen deterministic arrivals.** No timeouts, deaths or actual game completions. Raised the full distance band from **3-7 to 6-10 units**, increasing both bounds rather than retaining the easier 3-6-unit cases. Rewards and PPO settings unchanged. All sessions stopped; no subsequent stage started.

| Phase | Arrivals | Mean arrival | Median | 90th percentile | Range |
| --- | ---: | ---: | ---: | ---: | ---: |
| Stochastic training | 500/500 | 2.817 s | 2.50 s | 4.70 s | 1.50-10.50 s |
| Frozen deterministic evaluation | 100/100 | 2.522 s | 2.30 s | 3.81 s | 1.50-4.50 s |

Times are game seconds at 0.1 seconds per movement action. Observed starting distances were **6.0032-9.9996 units in training** and **6.0439-9.9963 in evaluation**. All targets remain inside the 12-unit vision range; the waypoint is an assigned instruction, not a sensed exit.

## Continuation and unchanged hierarchy

Continued full [QA-30 checkpoint](../qa-waypoint-reach-20261107-185241-train/README.md), SHA-256 `b3292b0e46a2824451de516221a2479889b1854224d58e59ac06bea941891202`, preserving its actor, critic and optimizer. No fresh transfer. Parent checkpoint unchanged.

Starting branch counter **26,582**; added **14,086 training steps / 500 episodes** and **550 optimizer updates**, reaching **40,668 branch model steps / 6,500 lineage training episodes**. This lineage follows QA-27 -> QA-30 -> QA-31 and excludes the separate QA-28/QA-29 farther experiments. Those checkpoints remain preserved.

Same empty 40 x 40 room, fixed centre spawn, random target direction/radius, one fixed waypoint, 0.5-unit arrival radius and 60-second / 600-action limit. No exit or automatic navigator; arrival ends the skill drill only, and every `game_completed` flag remains false. The normal navigator's six-unit assignment limit was not changed by this direct skill exercise.

Same `local-waypoint-v1`, 134 -> 64 -> 64 -> 2. PPO learning rate 0.0002, entropy 0.01, gamma 0.99, rollout 256, batch 64 and CPU. Training seeds **8000-8499**, frozen evaluation seeds **283000-283099**. Only the selected distance band, new seeds and checkpoint continuation differ from the previous stage; no source or timeout-contract change.

Reward stays **+1 arrival**, **+0.1/unit new closest approach**, **-0.001/action**, with no exploration/stuck reward in the isolated drill. All positive waypoint rewards share the **+5 per-attempt cap**, below the **+50 local-objective reward floor**. Observed positive maxima **1.973497 training / 1.982894 evaluation**. No exit-sized payout. Reward magnitudes alone do not establish competing-objective/combat priority, which remains untested.

## Evidence

- [Actual config](training-config.yaml), [run/source provenance](run.json), [preflight](verification.json), [checkpoint manifest](checkpoints.json).
- [Training result](training-result.json), [500 training records](training-episodes.json), [frozen result](evaluation-result.json), [100 frozen records](evaluation-episodes.json).
- [Medium-range config](../../../../Training/qa-player-waypoint-reach-medium.yaml), [runner](../../../../Training/run_waypoint_reach.py). Detailed local-only logs: `Logs/qa-waypoint-reach-training/20261107-185943-train/`.

Preflight configured-range/encoding checks, reference arrivals, no-exit checks, idle timeout and combined reward budget checks passed. All 600 records checked. Frozen evaluation made zero optimizer updates; tensors, counters and saved model bytes unchanged. Source hashes matched, parent preserved, project settings restored and Unity sessions exited successfully.

The cumulative 36,582-step snapshot and final ZIP passed byte-count, CRC and SHA-256 checks. Final model SHA-256: `5ed4da94b4ab957a61965163b28d0ae1dbfa45e145b9da8ff0712f120bc89062`. The result field `new_model_steps` is cumulative; `initial_model_steps` and `new_training_steps` separate starting and added steps.

The result validates these 100 sampled, unobstructed 6-10-unit assignments. No matched pre-training 6-10 evaluation establishes improvement attributable to the 500 episodes. Outside-vision targets, obstacles, competing objectives and retention of other distance bands were not evaluated in this run. No automatic curriculum advancement.
