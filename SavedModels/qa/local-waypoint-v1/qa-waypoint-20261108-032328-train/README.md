# QA-36: initial interrupted segment

**Initial segment failed; the [full campaign subsequently completed with recovery](../qa-waypoint-20261108-051102-train/README.md).** Historical failure check: 110/500 completed training episodes: 91 clears (82.7%), 19 timeouts, no deaths. Unity suffered a native crash and Python received `ConnectionResetError: [WinError 10054]`. Both processes have stopped. Root cause is unknown. At the initial failure check, the remaining training and evaluation had not run; both subsequently completed in recovery. This initial segment saved no final model. Recovery subsequently completed training and evaluation in the linked run; no child was promoted.

This is partial stochastic training performance, not a frozen-policy result or evidence of improvement over QA-32's 364/500 frozen benchmark. Completed seeds 9000-9109 contributed 32,196 steps. Latest PPO log: 95,758 cumulative model steps and 3,720 optimizer updates. Three periodic snapshots are valid; the latest retains **93,246 model steps / 3,630 optimizer updates**, or **30,000 new saved steps**. Later in-memory changes were lost. A resumed segment must distinguish retained checkpoint progress from rolled-back work rather than simply treating all 110 attempts as retained training.

The parent is the [verified QA-32 backup](../qa32-backup-20261108-032207/BACKUP.md), starting at 63,246 model steps / 2,460 optimizer updates / 7,000 actor-lineage episodes, SHA-256 `0ca5d55955bf6ea7489e2495c1a1cbd9c3be773154981483096dd17d4b4479e0`. All 12 original and backup files were verified unchanged at reconciliation. Project settings were restored. [Startup snapshot](startup-status.json) is historical, not current status.

## Retained interruption evidence

- [Partial training result](training-result.json), [110 completed episode records](training-episodes.jsonl), [PPO progress](training-progress.csv).
- [Failure and traceback](failure.json), [Unity crash excerpt](unity-crash-excerpt.txt).
- [All three checkpoint hashes and counters](checkpoints.json), [latest recoverable checkpoint](checkpoints/rl_model_93246_steps.zip).
- [Training history](../../../../Training/changelog/qa-player.md).

## Active task

QA-32's isolated 8-12-unit waypoint drill is replaced by integrated exit navigation using the existing observed-map/frontier/A* coordinator and the same `local-waypoint-v1` observation/action contract. Empty 40 x 40 room; initial exit 30-34 units away and unseen; 12-unit ray vision; normal visible, clear waypoints within six units. Only the real exit trigger completes the task. No internal obstacles or enemies.

Rewards: +50 actual exit, +1 waypoint arrival and +0.1 per unit of new closest approach, with all positive waypoint rewards sharing the existing +5 attempt cap; -0.001/action and existing stuck penalty rule; no exploration reward. Deadline 60 game seconds / 600 actions, playback 100. PPO LR 0.0002, entropy 0.01, gamma 0.99, rollout 256, batch 64, CPU; full optimizer state retained.

Exactly 500 training episodes, seeds 9000-9499; a 300,000 new-step ceiling is sufficient for 500 full timeouts. Periodic checkpoints every 10,000 new steps use cumulative model-step filenames. A successful run would save the final child checkpoint here before frozen evaluation; the crash prevented both.

The configured frozen deterministic evaluation, which never started, uses seeds 285000-285499, distinct from training seeds but the same earlier QA-33/35 benchmark placements. Parent baseline: 364/500 exit clears (72.8%). This is a repeated benchmark, not a new held-out case set. Only partial training evidence and periodic checkpoints are present; no final evaluation result exists. No later curriculum stage starts automatically.

The documented single-`done` timeout/termination handling remains unchanged; this run does not implement a timeout-bootstrap correction. Model task metadata now records `exit-navigation` for trained children rather than retaining the old drill label; that change does not alter movement, rewards or observations.

## Evidence and monitoring

- [Run-owned config](training-config.yaml), [run/source identities](run.json).
- Local-only runtime: `Logs/qa-waypoint-exit-from-qa32/20261108-032328-train/`.
- Local-only progress: `Logs/qa-waypoint-exit-from-qa32/20261108-032328-train/progress.json` once preflight completes.
- Local-only training/PPO metrics: the runtime's `training/progress.csv` and `training/episodes.jsonl`.
- Local-only launcher logs/process record: `Logs/qa-waypoint-exit-from-qa32/launcher-stdout.log`, `launcher-stderr.log`, `launcher-process.json`.

Started with:

```powershell
python -u -B Training/run_local_movement.py --config Training/qa-player-waypoint-exit-from-qa32.yaml --mode train
```

The processes are stopped. This command records the original launch; recovery from a periodic snapshot requires a separately identified continuation config.
