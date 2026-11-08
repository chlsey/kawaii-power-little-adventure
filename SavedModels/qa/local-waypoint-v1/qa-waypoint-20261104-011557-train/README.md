# QA-22: second 500-episode larger-room waypoint continuation

**Completed; training and evaluation stopped.** Continued the full QA-21 actor, critic and optimizer for 500 new episodes in the same open 40 x 40 room, followed by 100 frozen evaluations with waypoint inputs and 100 matched frozen evaluations with those inputs masked. No subsequent run or curriculum advancement.

| Phase | Seeds | Clears | Timeouts / deaths | New training steps / evaluation requests | Mean successful exit |
| --- | --- | --- | --- | --- | --- |
| Training | 3000-3499 | **324/500 (64.8%)** | 176 / 0 | 111,006 | 17.965 s |
| Frozen enabled | 250000-250099 | **64/100** | 36 / 0 | 21,099 requests | 16.092 s |
| Frozen masked | 250000-250099 | **55/100** | 45 / 0 | 19,931 requests | 11.693 s |

Training clears per hundred: **57, 59, 69, 74, 65**. Training earned 1,025 waypoint bonuses, zero exploration reward and 0.01 stuck penalty. Frozen evaluation performed **zero weight updates**, preserved all policy tensors and checkpoint files, and earned no exploration reward or stuck penalty. Successful-exit times describe each condition's successful subset.

## Did waypoint following improve?

| Checkpoint on the same 100 room setups | Waypoint inputs enabled | Waypoint inputs masked |
| --- | --- | --- |
| QA-21, before this continuation | 61/100 | 56/100 |
| QA-22, after 500 more episodes | **64/100** | **55/100** |

Enabled completion increased by **3 points**, with a paired 95% interval of **-10 to +16 points**. The latest waypoint-input advantage is **9 points**, interval **-2 to +20**. Its increase from the parent's five-point gap is **4 points**, interval **-15 to +23**. All intervals include zero. These cases establish neither a clear completion improvement nor stronger reliance on the waypoint inputs. The checkpoint remains a measured continuation, without promotion to a reliably waypoint-dependent policy.

Rewarded waypoint arrivals in enabled evaluation decreased from **349 to 201**; episodes with any waypoint bonus decreased from **96 to 85**. These counts are capped at five rewarded arrivals per episode and depend on the trajectory and episode length. They do not establish more waypoint following. The masked child earned 120 waypoint bonuses versus its parent's 70; navigation and reward collection still operate while the actor's waypoint fields are masked.

Masking zeros only inputs **130-133** during deterministic prediction. Current wall/exit rays and previous movement remain available. Both checkpoints were trained with waypoints; this is an input ablation, rather than a separately trained model without waypoints. All four conditions use exactly matching spawn/exit coordinates on the same 100 unique seeds. Initial exits were unseen in every case. [Paired counts, intervals, provenance and verification](waypoint-comparison.json); [preserved parent comparison](../qa-waypoint-20261103-172142-train/comparison-100/README.md).

Intervals use 20,000 paired case-bootstrap resamples, RNG seed 250000. Exact two-sided tests on discordant pairs are retained for binary comparisons. The gap-change interval resamples all four outcomes together by seed. No multiple-comparison adjustment; uncertainty covers sampled room setups, not variability across training seeds. Action/position trajectories would be needed to explain the policy's decision mechanism.

## Checkpoints and configuration

Resume parent SHA-256: `de32a90630b506d97610a918cadea89039518720d0ae2e96c1af58a231e1fe6b`, preserved unchanged. Start at **160,362 waypoint-model steps** and **3,000 prior lineage training episodes**. Add **111,006 steps / 500 episodes**, reaching **271,368 cumulative waypoint-model steps** and **3,500 total lineage training episodes**. Older actor-transfer history stays in the parent lineage records. Continue the full model/critic/optimizer; no fresh critic or optimizer.

Final SHA-256: `0ab90c438098dbd19b540b16fe72092c2e4a190ed6f10efc769fb75c99fa3bd2`.

[Final model](final_model.zip), [all eleven periodic checkpoints](checkpoints/), [checkpoint hashes](checkpoints.json). Periodic filenames use cumulative steps, from 170,362 through 270,362; these correspond to 10,000 through 110,000 new steps. All twelve model archives passed CRC, semantic-version, step-count and manifest/hash checks. Intermediate checkpoints have not each received independent frozen evaluation.

Same `local-waypoint-v1` **134 -> 64 -> 64 -> 2** movement policy; 40 x 40 open room, unseen exits 30-34 units away, 12-unit vision, six-unit waypoints and 30-second episodes. Completion +50; waypoint arrival +1 capped at +5; best approach +0.1/unit; action -0.001; existing stuck penalty; exploration 0. PPO learning rate 0.0002, entropy 0.01, gamma 0.99, rollout 256, batch 64, CPU. Game, navigator and policy sources match the parent comparison.

[Run config](training-config.yaml), [maintained continuation config](../../../../Training/qa-player-waypoint-40x40-continuation.yaml), [source/model provenance](run.json), [frozen control config](frozen-control-config.yaml), [control provenance](waypoint-masked-run.json). The maintained continuation config deliberately selects QA-21 to reproduce this run; it does not silently select this child.

[Training summary](training-result.json), [training records](training-episodes.jsonl), [enabled summary](evaluation-result.json), [enabled records](evaluation-episodes.jsonl), [masked summary](waypoint-masked-result.json), [masked records](waypoint-masked-episodes.jsonl).

[Preflight verification](verification.json) passed 600 placement checks, eight compass completions, map/barrier/unknown-gap/recovery/reward checks and 10/10 reference episodes, with 185 handoffs and at least five before exit discovery. Reference-controller results are separate from learned-policy outcomes. All three Unity sessions closed cleanly and project settings were restored. The [QA changelog](../../../../Training/changelog/qa-player.md) records the experiment history. Generated dungeons, obstacle training, combat and standalone ONNX remain deferred.
