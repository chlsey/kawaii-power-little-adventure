# QA-28: farther-waypoint continuation

**Complete: 234/500 training arrivals (46.8%), followed by 51/100 frozen deterministic evaluation arrivals (51%).** Training had 266 timeouts; evaluation had 49. No deaths or actual game completions. All sessions stopped; no later stage started. Retain this experiment and its nearby parent separately; reliable farther-waypoint following is not established.

| Phase | Arrivals | Mean successful arrival | Median | 90th percentile | Successful range |
| --- | ---: | ---: | ---: | ---: | ---: |
| Stochastic training | 234/500 | 10.06 s | 6.35 s | 21.47 s | 3.20-59.10 s |
| Frozen deterministic evaluation | 51/100 | 6.13 s | 5.20 s | 7.00 s | 3.30-46.60 s |

Times describe successful attempts only, in game seconds at 0.1 seconds per movement action. All failed attempts timed out at 60 game seconds / 600 actions. Arrivals per consecutive 100 training episodes: **34, 39, 56, 49, 56**; these are training counts, not frozen evaluations.

## Continuation and scenario

Resumed the full [QA-27 nearby model](../qa-waypoint-reach-20261107-034515-train/README.md), including actor, critic and optimizer, from SHA-256 `3ab41e4c309ca405028f28728ac0b8619a8835e8d9744a83aa20e79d31118613`. No fresh critic or optimizer. Starting model counter 13,718; this stage added **183,130 steps / 500 training episodes**, reaching **196,848 branch model steps**. Added **7,150 optimizer updates**. Parent lineage 5,500 training episodes; total lineage now **6,000**, including 1,000 episodes across the two dedicated waypoint-skill stages. Evaluation episodes are excluded from training lineage.

Distance increased from **3-6 to 13-18 units**. The empty 40 x 40 room, fixed centre spawn, uniformly sampled target angle/radius, 12-unit vision, fixed assigned waypoint, 0.5-unit arrival radius and 60-second limit are retained. There is no exit or automatic navigator. The selected config applies its distance range at runtime; the scene asset retains its nearby defaults. Assigned direction remains supplied outside vision, with normalized distance clipped at one. This tests following direct distant instructions, not discovering a hidden objective or the normal navigator's six-unit waypoint assignments.

The `local-waypoint-v1` contract remains 134 -> 64 -> 64 -> 2. PPO settings unchanged: learning rate 0.0002, entropy 0.01, gamma 0.99, rollout 256, batch 64, CPU. Training seeds **6500-6999**, frozen evaluation seeds **281000-281099**.

Reward unchanged: +1 arrival, +0.1/unit new closest approach, -0.001/action, no exploration/stuck reward in this isolated drill. Approach and all arrivals share a **+5 per-attempt budget**, below the configured **+50 minimum local-objective reward**. Highest observed positive waypoint totals: **2.757035 training / 2.719533 evaluation**. No exit-sized payout exists; every record has `game_completed: false`. Skill arrival is distinct from actual gameplay-objective completion. Competing-objective/combat priority remains outside this scenario.

## Evidence and limits

- [Actual config](training-config.yaml), [run/source provenance](run.json), [farther preflight](verification.json), [checkpoint manifest](checkpoints.json).
- [Training result](training-result.json), [500 training records](training-episodes.json), [frozen result](evaluation-result.json), [100 frozen records](evaluation-episodes.json).
- [Runner](../../../../Training/run_waypoint_reach.py), [scenario guide](../../../../Assets/Scenes/QaSingleRoomTraining.README.md#waypoint-reaching-skill-drill). Detailed local-only logs: `Logs/qa-waypoint-reach-training/20261107-051415-train/`.

Preflight passed 100 seeded range/direction/encoding checks, eight reference arrivals, no-exit checks, the exact idle timeout and combined reward cap/reset checks. All 600 recorded seeds and outcome counts were checked. Frozen evaluation made zero updates; policy tensors, counters and final checkpoint bytes remained unchanged. All 19 retained ZIP archives (18 periodic snapshots plus final) passed byte-count, CRC and SHA-256 checks. Nearby parent bytes are unchanged, source hashes matched, project settings were restored and Unity sessions exited successfully.

Final model SHA-256: `e4ddd962f8f63fd739d5711eda23d5d40b307e2faf15a85e8a173e8982c32a3b`. Snapshot filenames use cumulative branch steps, from 23,718 through 193,718. The result field `new_model_steps` retains the runner's cumulative-counter naming; `initial_model_steps` and `new_training_steps` explicitly separate starting and added steps.

No matched frozen farther evaluation of the QA-27 starting checkpoint was recorded. These results therefore establish current farther-task performance, not an improvement attributable to this continuation. QA-27's 100/100 nearby result and QA-26's 1,000-case distant evaluation use different tasks or seeds and are not matched before/after comparisons. Nearby performance after this farther continuation has not been measured. No automatic model promotion or subsequent training.
