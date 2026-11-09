# QA-39: QA-32 continuation on 1-10-unit waypoints

**Complete: 500/500 training arrivals, then 100/100 frozen arrivals.** Original QA-32 also reached all 100 matched baseline targets. Mean frozen arrival time improved **1.829 -> 1.594 game seconds**, a **0.235 s / 12.8% reduction**. Completion is retained at 100% on this cohort; it did not increase above the parent. Original QA-32 and its backup remain unchanged. New child is preserved separately; no automatic navigation-model promotion.

## Matched frozen evaluation

| Metric | Parent QA-32 | QA-39 child |
| --- | ---: | ---: |
| Arrivals | 100/100 | 100/100 |
| Timeouts/deaths | 0/0 | 0/0 |
| Mean arrival time | 1.829 s | 1.594 s |
| Median | 1.750 s | 1.600 s |
| p90 | 3.510 s | 2.600 s |
| Slowest arrival | 4.700 s | 4.200 s |

Same evaluation seeds 286000-286099, initial distances and target positions verified across all 100 pairs. Child was faster on **29 cases**, slower on six, tied on 65. Exploratory paired case-bootstrap 95% interval for mean time improvement **0.120-0.364 s** (20,000 resamples, RNG seed 39); this describes case sampling, not variability across independent training runs. These frozen cases are separate from training seeds. Parent's older 99/100 at 8-12 units is a different task/cohort and is not the before/after control here.

| Target range | Cases | Parent arrivals | Child arrivals | Mean parent -> child time |
| --- | ---: | ---: | ---: | ---: |
| 1-3 units | 22 | 22 | 22 | 0.955 -> 0.586 s |
| 3-6 units | 34 | 34 | 34 | 1.565 -> 1.324 s |
| 6-8 units | 23 | 23 | 23 | 2.187 -> 2.013 s |
| 8-10 units | 21 | 21 | 21 | 2.781 -> 2.629 s |

## Training and lineage

Full actor/critic/optimizer continuation directly from original QA-32's verified backup, SHA-256 `0ca5d55955bf6ea7489e2495c1a1cbd9c3be773154981483096dd17d4b4479e0`, starting **63,246 model steps / 2,460 optimizer updates / 7,000 actor-lineage episodes**. This is a separate child of QA-32, not a continuation of QA-36.

Trained exactly **500 episodes**, seeds 9500-9999: **500 arrivals, zero timeouts/deaths, mean 1.762 s**, median 1.700 s, p90 3.000 s, max 5.000 s. Added **8,808 model steps and 340 optimizer updates**; final **72,054 model steps / 2,800 updates / 7,500 lineage training episodes**. Periodic snapshot at 68,246 steps and final checkpoint retained together. Full new-step ceiling was 300,000; the episode callback stopped at 500 attempts. Frozen evaluation added no training/optimizer updates and changed no policy tensors/checkpoint bytes.

Final checkpoint SHA-256: `a90f3a238b4d6e6b236db357467edafd97b89c5b2dc57bccba50263c72102d2f`.

## Task and unchanged settings

Empty 40 x 40 room, centre spawn reset for each attempt, a single fixed assigned target with uniform angle and uniform radius **1-10 units**, no exit object or navigation coordinator. **0.5-unit arrival radius**, 60 game seconds / 600 actions, 12-unit rays, local-waypoint-v1 / 134 -> 64 -> 64 -> 2 world-axis movement. Previous-action inputs remain present and unchanged. Reward: +1 arrival, +0.1/unit new closest approach, all positive shaping sharing a +5 cap, -0.001/action, no stuck penalty or exit payout. The +50 local-objective floor is only the reward-hierarchy guard, not a payment in this drill.

PPO LR 0.0002, gamma 0.99, entropy 0.01, rollout 256, batch 64, CPU; optimizer and critic resumed. Checkpoints every 5,000 new actions. All 700 parent-baseline/training/child-evaluation attempts have `game_completed=false` and no deaths. Arrival ends the skill episode only.

Preflight passed 100 seeded resets, all four directional quadrants, eight observation-driven reference arrivals, no-exit checks, 600-action idle timeout and combined reward budget checks. The maintained runner gained an explicit `--mode evaluate` path for the frozen parent baseline; default remains verification and training still requires `--mode train`. No environment/reward/control change was introduced for baseline evaluation.

## Scope

This validates standalone fixed-waypoint arrival in the sampled 1-10-unit band and shows a timing improvement on the matched frozen cases. It does **not** validate moving waypoint handoffs, integrated exit navigation, 10-12-unit skill retention, obstacles, combat, generated dungeons or deployment. The drill resets previous movement at each attempt; it does not reproduce changing the target while the bot is already moving. No zero-history input ablation or fix from QA-37 was applied. Existing timeout/termination handling is unchanged. No subsequent curriculum stage or PCG run started.

A [separate frozen integrated navigation test](../diagnostics/qa39-navigation-30to40-20261108-175349-evaluate/README.md) subsequently reached 67/100 initially unseen goals 30-40 units away in a 50 x 50 empty room with local assignments capped at ten. Thirty failures preceded goal discovery and three followed it; 505 arrival-driven handoffs were recorded. That setup differs from the older QA-32 benchmark and does not establish a matched improvement.

The [subsequent predefined chain test](../diagnostics/waypoint-chain-20261108-182109/README.md) removes exploration: 100/100 first targets reached, but only 33/100 ten-target chains completed without resets. Reference controls cleared all chains. This exposes a continuing-control limitation absent from the standalone score; its exact input/state cause remains unresolved.

## Artifacts

[Final model](final_model.zip), [checkpoint hashes](checkpoints.json), [parent comparison](parent-comparison.json), [completion checks](completion-verification.json), [run config](training-config.yaml), [parent/source identities](run.json), [preflight](verification.json).

[Training result](training-result.json), [500 training records](training-episodes.json), [child frozen result](evaluation-result.json), [100 child records](evaluation-episodes.json), [parent frozen result](parent-evaluation-result.json), [100 parent records](parent-evaluation-episodes.json), [parent evaluation provenance](parent-evaluation-run.json).

[Parent QA-32](../qa-waypoint-reach-20261107-214655-train/README.md), [preserved backup](../qa32-backup-20261108-032207/BACKUP.md), [baseline run](../diagnostics/waypoint-reach-20261108-145121-evaluate/README.md), [maintained recipe](../../../../Training/qa-player-waypoint-reach-1to10.yaml), [change history](../../../../Training/changelog/qa-player.md).

Local-only runs: `Logs/qa-waypoint-reach-1to10/20261108-145121-evaluate/` (parent) and `Logs/qa-waypoint-reach-1to10/20261108-145257-train/` (training/child evaluation). Both completed and all sessions stopped. Commands below reproduce the experiment rather than resume an active process:

```powershell
python -B Training/run_waypoint_reach.py --config Training/qa-player-waypoint-reach-1to10.yaml --mode evaluate
python -B Training/run_waypoint_reach.py --config Training/qa-player-waypoint-reach-1to10.yaml --mode train
```
