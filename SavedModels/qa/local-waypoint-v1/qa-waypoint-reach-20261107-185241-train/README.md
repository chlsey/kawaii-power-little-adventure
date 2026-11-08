# QA-30: nearby rollback with a one-unit distance increase

**Complete: 500/500 training arrivals and 100/100 frozen deterministic arrivals.** No timeouts, deaths or actual game completions. Returned to QA-27's successful nearby checkpoint and extended its range from **3-6 to 3-7 units**, retaining the lower bound and increasing only the maximum by one unit. No reward changes; no automatic next distance increase.

| Phase | Arrivals | Mean arrival | Median | 90th percentile | Range |
| --- | ---: | ---: | ---: | ---: | ---: |
| Stochastic training | 500/500 | 2.573 s | 1.65 s | 5.70 s | 0.70-7.80 s |
| Frozen deterministic evaluation | 100/100 | 2.211 s | 1.50 s | 5.11 s | 0.70-5.50 s |

Times are game seconds at 0.1 seconds per movement action. These results describe this nearby task and its sampled cases; there was no matched pre-training 3-7-unit evaluation. They do not establish an improvement caused by these 500 episodes or a speed comparison with earlier runs using different seeds/distances.

## Checkpoint lineage and unchanged settings

Resumed the **full [QA-27 nearby model](../qa-waypoint-reach-20261107-034515-train/README.md)**, including critic and optimizer, SHA-256 `3ab41e4c309ca405028f28728ac0b8619a8835e8d9744a83aa20e79d31118613`. Parent bytes are unchanged. This is a separate fork from the nearby checkpoint; QA-28 and QA-29 farther-training episodes are not included in this fork's lineage. All farther-experiment checkpoints remain preserved separately.

Starting branch counter **13,718 steps**; added **12,864 steps / 500 training episodes**, reaching **26,582 branch model steps**. Added **500 optimizer updates**. Total lineage training episodes **6,000**: original 5,000 actor-parent episodes, 500 nearby QA-27 episodes and these 500 gradual-stage episodes. No fresh actor transfer, critic or optimizer.

Same empty 40 x 40 room, fixed centre spawn and random target direction/radius; one assigned waypoint, 12-unit vision, 0.5-unit arrival radius, 60-second / 600-action limit, no exit and no navigation coordinator. Arrival ends only the skill drill; all 600 episode records have `game_completed: false`. Waypoints remain instructions toward gameplay objectives, not substitute game-completion signals.

Observation/action contract remains `local-waypoint-v1`, 134 -> 64 -> 64 -> 2. PPO learning rate 0.0002, entropy 0.01, gamma 0.99, rollout 256, batch 64 and CPU unchanged. New training seeds **7500-7999**, frozen evaluation seeds **282000-282099**. The runner applies 3-7 at runtime; the reusable scene retains its nearby 3-6 defaults. The normal navigator's six-unit assignment limit is unchanged.

Reward remains **+1 arrival**, **+0.1/unit new closest approach**, **-0.001/action**, with no exploration or stuck reward in the isolated drill. All positive waypoint rewards share the **+5 per-attempt cap**, below the configured **+50 local-objective reward floor**. Observed maxima: **1.686615 training / 1.671469 evaluation**. No exit-sized completion payout. The previously identified timeout termination/truncation behavior was not changed in this curriculum experiment; no learned-policy episode timed out here. Competing-objective/combat priority remains untested.

## Evidence and verification

- [Actual config](training-config.yaml), [run/source provenance](run.json), [preflight](verification.json), [checkpoint manifest](checkpoints.json).
- [Training result](training-result.json), [500 training records](training-episodes.json), [frozen result](evaluation-result.json), [100 frozen records](evaluation-episodes.json).
- [Gradual config](../../../../Training/qa-player-waypoint-reach-gradual.yaml), [runner](../../../../Training/run_waypoint_reach.py). Detailed local-only logs: `Logs/qa-waypoint-reach-training/20261107-185241-train/`.

Preflight range/encoding checks, eight reference arrivals, no-exit checks, idle timeout and combined reward budget checks passed. All 600 episode records were checked. Frozen evaluation made zero optimizer updates; all policy tensors, counters and final checkpoint bytes were unchanged. Source hashes matched; parent preserved, project settings restored and Unity sessions stopped.

The cumulative 23,718-step snapshot and final ZIP passed CRC, SHA-256 and byte-count checks. Final model SHA-256: `b3292b0e46a2824451de516221a2479889b1854224d58e59ac06bea941891202`. The result field `new_model_steps` is cumulative; `initial_model_steps` and `new_training_steps` distinguish starting and added steps. No subsequent stage started.
