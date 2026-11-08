# QA-29: another 500 farther-waypoint episodes

**Complete: 221/500 training arrivals (44.2%), then 33/100 frozen arrivals (33%).** Frozen completion regressed from the parent's 51/100 on identical seeds and target placements. Preserve the parent as the better candidate on this case set; retain this child as an experiment without promotion. No further training started; all sessions stopped.

| Phase | Arrivals | Timeouts | Mean successful arrival | Median | 90th percentile | Successful range |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Stochastic training | 221/500 | 279 | 15.07 s | 9.80 s | 30.30 s | 3.30-59.90 s |
| Frozen deterministic evaluation | 33/100 | 67 | 11.98 s | 9.50 s | 20.12 s | 3.30-32.10 s |

Times are game seconds, 0.1 seconds per movement action, and describe successful attempts only. Every failed attempt timed out at 60 seconds / 600 actions. No deaths or actual game completions. Training arrivals per hundred: **42, 59, 42, 38, 40**.

## Matched frozen comparison

All 100 seeds **281000-281099**, target X/Z positions and initial distances exactly match [QA-28's evaluation](../qa-waypoint-reach-20261107-051415-train/README.md).

| Paired outcome | Cases |
| --- | ---: |
| Both arrived | 29 |
| New arrival, parent timeout | 4 |
| Parent arrival, new timeout | 22 |
| Both timed out | 45 |

Completion change **-18 percentage points**, paired case-bootstrap 95% interval **-27 to -9**, exact paired p = **0.000534**. Among the 29 cases both completed, mean arrival time increased from **5.10 to 11.53 seconds**. Full-condition successful-time means concern different subsets and should not be treated as a paired speed comparison.

Intervals use 20,000 paired case-bootstrap resamples, RNG seed 281000; the exact paired test uses 26 discordant pairs. These are exploratory, unadjusted comparisons on a reused evaluation set. They quantify case sampling, not variability across independent training runs or fresh unseen cases. They identify regression on this set without establishing its cause. [Paired analysis](paired-comparison.json).

## Continuation and unchanged settings

Resumed the complete [QA-28 checkpoint](../qa-waypoint-reach-20261107-051415-train/README.md), including critic and optimizer, SHA-256 `e4ddd962f8f63fd739d5711eda23d5d40b307e2faf15a85e8a173e8982c32a3b`. Parent checkpoint bytes are unchanged. No actor transfer or reset. Starting branch steps **196,848**; added **200,700 training steps / 500 episodes**, reaching **397,548 branch model steps** and **6,500 lineage training episodes**. This is now 1,000 farther-waypoint training episodes, plus the earlier 500 nearby episodes, separate from the original 5,000-episode parent actor history. New optimizer updates: **7,830**; frozen evaluation updates: **zero**.

Only resume parent, training seed range and lineage count changed in the actual configs. Task, rewards, PPO settings, evaluation seeds and source hashes are identical to QA-28:

- Empty 40 x 40 room, fixed centre spawn, random fixed waypoint **13-18 units** away, 12-unit vision, 0.5-unit arrival radius and 60-second limit. Assigned direction is supplied outside vision; normalized distance clips at one. No exit or automatic navigator. All `game_completed` flags remain false; skill arrival is not game-objective completion.
- `local-waypoint-v1`, 134 -> 64 -> 64 -> 2; PPO learning rate 0.0002, entropy 0.01, gamma 0.99, rollout 256, batch 64, CPU. Training seeds **7000-7499**.
- +1 arrival, +0.1/unit new closest approach, -0.001/action; no exploration or stuck reward in the drill. Combined positive waypoint budget **+5**, below the **+50 local-objective floor**. Observed positive maxima **2.769315 training / 2.713540 evaluation**. Competing-objective priority and nearby retention remain untested.

## Retained evidence

- [Actual config](training-config.yaml), [run/source provenance](run.json), [preflight](verification.json), [checkpoint manifest](checkpoints.json).
- [Training result](training-result.json), [500 training records](training-episodes.json), [frozen result](evaluation-result.json), [100 frozen records](evaluation-episodes.json).
- [Continuation template](../../../../Training/qa-player-waypoint-reach-farther-continuation.yaml), [runner](../../../../Training/run_waypoint_reach.py). Detailed local-only logs: `Logs/qa-waypoint-reach-training/20261107-053815-train/`.

Preflight range, reference following, no-exit, timeout and reward-budget checks passed. All 600 episode records, source hashes, frozen tensor/counter invariance and unchanged parent were verified. Twenty periodic snapshots plus the final model passed byte-count, SHA-256 and ZIP CRC checks. Project settings restored; Unity sessions exited successfully.

Final model SHA-256: `63af6871b286f5339cd76171764e6ef95c439db794239a68df66481039ccf87b`. Periodic filenames use cumulative branch steps, from 206,848 through 396,848. The runner's `new_model_steps` is cumulative; `initial_model_steps` and `new_training_steps` distinguish the starting counter and added steps.
