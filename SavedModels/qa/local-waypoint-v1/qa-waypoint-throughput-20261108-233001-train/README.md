# QA-44: pre-waypoint restart, distance curriculum and timed arrivals

**Complete: all 2,000 requested training episodes. Final frozen policy averages 44.43 waypoints per 60 seconds, range 37-53, with 100/100 cases reaching at least ten.** Preserved QA-43 averages 10.93 and reaches ten in 42/100 identical cases. All models use the same seeded target sequences and uninterrupted movement history. Every timed episode lasts the full 60 seconds.

The restarted model already averages **38.98** and reaches ten in **95/100** cases after the three single-target stages. The final 500 timed episodes add **5.45 arrivals/minute** on average. The large overall gain is therefore already present before timed training; this experiment does not prove previous-action feedback caused the old loops.

## Starting point and preservation

QA-17 is the last trained actor before assigned-waypoint or navigator training: local-vision-v1, 198,495 historical model steps / 2,000 actor-lineage episodes, SHA-256 `76226e50dee7f0586b4c6b5b1bb8a3f3358980a5983fa712a7e0a413cc22833d`. It has local exit-movement training; it is not a random/untrained actor.

[QA-18](../qa-waypoint-transfer-start/transfer.json) is its existing waypoint-compatible zero-step transfer, SHA-256 `9657ec0745b6163cc14f91fa2dc171a56e0a1cd4c190568d5ceb100f0f58fba9`: all seven actor tensors exactly match QA-17, with a fresh critic, empty optimizer and zero waypoint updates. The new branch starts here and excludes QA-19 through QA-43 learning. Subsequent stages continue the preceding stage's full actor/critic/optimizer.

[QA-43 full backup](../qa43-backup-20261108-231031/BACKUP.md) preserves 91 source files, including 20 full model archives and recorded evaluations. Every backup file was hash-verified again after completion; original QA-43 model archives and QA-18 are unchanged. The former QA-43 continuation request was held before launch and superseded by this new curriculum.

## Four authorized stages

| Stage | Training episodes / outcome | Frozen result | Saved checkpoint evidence |
| --- | --- | --- | --- |
| One target, 3-6 units | 500/500 arrivals | 100/100, mean 1.152 s | [Stage 1](../qa-waypoint-reach-20261108-232326-train/README.md) |
| One target, 6-8 units | 500/500 arrivals | 100/100, mean 1.745 s | [Stage 2](../qa-waypoint-reach-20261108-232529-train/README.md) |
| One target, 8-10 units | 500/500 arrivals | 100/100, mean 2.235 s | [Stage 3](../qa-waypoint-reach-20261108-232749-train/README.md) |
| Continuous targets for 60 seconds | 500 episodes, 20,202 arrivals; mean 40.404/min | 100 cases, mean 44.43/min | This run |

First-three training seeds: 12000-12499, 12500-12999 and 13000-13499; their frozen seeds: 290000-290099, 291000-291099 and 292000-292099. Timed training: 13500-13999, exactly 500 full episodes / 300,000 actions, with 493 episodes reaching ten, median 42 arrivals and range 2-51. All training seeds differ from evaluation.

The first three stages end on the single arrival or a 60-second deadline and reset movement only at episode start. The final stage has no waypoint-count termination: each arrival immediately assigns the next random target with player position, orientation, sensors, previous action and metrics preserved. It always ends after 600 actions / 60 game seconds, so a deadline ending is expected and is not classified as episode failure.

## Frozen timed comparison

All three policies receive the same 100 coordinate sequences, seeds **288000-288099**, deterministic CPU actions, zero diagnostic reward and no optimizer/model-step updates.

| Measure | Preserved QA-43 | Restart after three single-target stages | Final QA-44 after timed training |
| --- | ---: | ---: | ---: |
| Mean arrivals / 60 seconds | 10.93 | 38.98 | **44.43** |
| Median arrivals | 7 | 44 | 45 |
| Minimum / maximum | 1 / 35 | 5 / 52 | **37 / 53** |
| Cases reaching at least ten | 42/100 | 95/100 | **100/100** |
| Total arrivals | 1,093 | 3,898 | 4,443 |

QA-43's first-ten outcomes, action counts and target coordinates exactly reproduce the earlier fixed-ten benchmark. Continuing beyond target ten does not alter its earlier prefix. This permits direct comparison with the old 42/100 chain result without treating the 60-second exercise as a fixed-count task. The final policy reaches at least 37 in every recorded case; no tested case collapses to single-digit throughput.

Paired case-bootstrap intervals (20,000 draws, seed 44):

- QA-43 -> restart after single-target stages: **+28.05 arrivals/min**, 95% interval **+25.07 to +30.94**; 94 cases improve, six decrease.
- Restart before -> after timed training: **+5.45 arrivals/min**, interval **+3.14 to +7.90**. Counts increase in 32 cases, decrease in 51 and tie in 17. The higher mean comes from improving the low-throughput tail; this is not a uniform speed increase on every case. The minimum rises from five to 37 and the median from 44 to 45.
- QA-43 -> final: **+33.50 arrivals/min**, interval **+31.66 to +35.25**; every case improves. The ten-target threshold rises 42 -> 100 cases.

These intervals describe case resampling for these specific checkpoints, not independent-training variability. The benchmark was already diagnosed and reused; it is not an untouched test set. Starting actor lineage, distance curriculum and final reward scale differ from the older branch. No masked-input control was run. Strong performance after single-target-only training also shows that such training does not inevitably prevent sequence following; the exact cause of QA-43's loops remains unisolated.

## Environment, rewards and verification

Existing empty 40 x 40 room, centre spawn per episode, 12-unit wall/exit rays, 0.5-unit arrival tolerance and world-X/Z actions. No exit, navigator, exploration/recovery, barriers, enemies or generated dungeon. Single stages use uniform angle/radius in their selected band. Timed targets are a seeded bounded random walk with nominal legs 1.5-9.5, points inside +/-15 X/Z; the half-unit handoff tolerance guarantees actual assignments in 1-10 units. All observed continuous assignments span **1.04910-9.95259** units.

The timed adapter precomputes 601 points, exceeding the maximum possible arrivals within 600 actions; it has no target-count episode-ending rule. Its first ten points exactly match the established benchmark. Both previous-action inputs and player position continuity were verified at all frozen handoffs: QA-43 1,092, new before-timed 3,895 and final 4,438 observed handoffs. Arrivals on the final action can end without another action on the next target, explaining why handoff and arrival totals differ.

Single stages retain +1 arrival, +0.1/unit new closest approach, -0.001/action and combined positive budget five. Timed training uses the same shaping scaled by 0.015: **+0.015 arrival / +0.0015 new closest unit / -0.001 action**. Every new leg earns credit. At least two actions are needed to cover a one-unit assignment to the 0.5 radius at speed four; <=300 arrivals plus <=240 moved units bound total positive credit at **4.86**, below the existing +5 cap and +50 objective floor. Maximum measured positive timed training credit was **1.08377**, so feedback was never exhausted early. The score is arrivals, not reward.

PPO remains local-waypoint-v1 / 134 -> 64 -> 64 -> 2, LR 0.0002, gamma 0.99, entropy 0.01, rollout 256, batch 64, ten optimization epochs, CPU. Previous movement remains an input throughout; no handoff masking/reset was introduced. Exact episode stops discard **497 collected transitions** in incomplete rollouts across the four stages rather than run extra episodes. Existing timeout/termination handling remains unchanged.

Timed reference verification passed ten full episodes (mean 48 arrivals, range 45-51) and one idle episode (zero arrivals, -0.6 return); reward, assignment bounds and 600-action deadlines passed. Reference seeds differ from the model benchmark and serve as preflight feasibility checks, not a paired optimality comparison. An initial CLI attempt was rejected for missing required config keys before any run/Unity startup or learning; keys were supplied before this recorded run.

## Final checkpoint and integrity

Final **328,433 branch model steps / 12,810 optimizer updates / 4,000 actor-lineage episodes**: 2,000 historical QA-17 episodes plus these 2,000 new ones. Branch optimization starts at zero under QA-18's fresh critic/optimizer. The final timed stage adds 300,000 steps / 11,710 updates to the third-stage parent's 28,433 steps / 1,100 updates.

Final SHA-256 **`1094cd0cea9a3fe2bc183b5bd3a7c1dc2ea6d22f4d5d5939629d7ed7ccafde8e`**. Verified **38** experiment archives across all four stages (31 here, seven single-stage archives), model hashes/CRC, source snapshots, unchanged parents/backup and restored settings. Sixty sampled saved-model predictions match final evaluation traces. All six single-stage training/evaluation phases, four timed model phases and verification sessions finished; no experiment process remains. No deployment, actual-exit navigation run, later curriculum or extra training followed.

## Evidence and reproduction

[Final checkpoint](final_model.zip), [timed checkpoint manifest](checkpoints.json), [training summary](training-result.json), [500 timed training records](training-episodes.json), [QA-43 comparison](qa43-evaluation-result.json), [before-timed baseline](before-continuous-result.json), [final frozen result](evaluation-result.json), [paired comparisons and continuity checks](comparison.json), [analysis script](analyze.py), [completion verification](completion-verification.json), [training completion](completion.json), [run settings/source identities](run.json), [run-owned config](training-config.yaml), [reference/idle verification](verification/result.json).

[QA-43 frozen actions](qa43-evaluation/actions.jsonl.gz), [before-timed actions](before-continuous/actions.jsonl.gz), [final frozen actions](evaluation/actions.jsonl.gz), [final cases](evaluation/episodes.jsonl), [final waypoint records](evaluation/legs.jsonl), [final precomputed sequences](evaluation/chains.jsonl). Every phase retains its exact coordinates and session settings. The 601-point buffers are implementation capacity, not an episode objective count.

From the game directory with other Unity sessions closed:

```powershell
python -B Training/run_waypoint_reach.py --config Training/qa-player-waypoint-restart-nearby.yaml --mode train
python -B Training/run_waypoint_reach.py --config Training/qa-player-waypoint-restart-middle.yaml --mode train
python -B Training/run_waypoint_reach.py --config Training/qa-player-waypoint-restart-8to10.yaml --mode train
python -B Training/run_waypoint_throughput.py --config Training/qa-player-waypoint-restart-throughput.yaml --mode verify
python -B Training/run_waypoint_throughput.py --config Training/qa-player-waypoint-restart-throughput.yaml --mode train
```

Configs reproduce their pinned parents and stages; they do not auto-select newly created timestamps. To reanalyze saved timed data only:

```powershell
python -B SavedModels/qa/local-waypoint-v1/qa-waypoint-throughput-20261108-233001-train/analyze.py
```

[Session guide](../../../../Assets/Scenes/QaSingleRoomTraining.README.md#pre-waypoint-restart-and-timed-arrivals), [training history](../../../../Training/changelog/qa-player.md). Local timed runtime `Logs/qa-waypoint-restart-throughput/20261108-233001-train/`; first-three runtime paths follow their configs. Full requested curriculum complete, all sessions stopped.
