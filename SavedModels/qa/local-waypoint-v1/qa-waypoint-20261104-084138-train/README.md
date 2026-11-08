# QA-23: 1,000 more larger-room waypoint episodes

**Completed; training and evaluation stopped. Room completion regressed on the matched benchmark.** The parent checkpoint and its complete archive remain unchanged. Retain this child as an experiment; it has not been promoted to a better navigation policy. No subsequent training or curriculum advancement.

| Phase | Seeds | Clears | Timeouts / deaths | New training steps / evaluation requests | Mean successful exit |
| --- | --- | --- | --- | --- | --- |
| Training | 3500-4499 | **559/1,000 (55.9%)** | 441 / 0 | 224,997 new steps | 16.583 s |
| Frozen enabled | 250000-250099 | **48/100** | 52 / 0 | 21,980 requests | 13.292 s |
| Frozen masked | 250000-250099 | **62/100** | 38 / 0 | 19,113 requests | 12.440 s |

Training clears per hundred: **69, 60, 68, 56, 49, 57, 58, 54, 47, 41**. Training earned 2,426 waypoint bonuses; exploration reward and stuck penalty were zero. Both frozen evaluations made **zero weight updates**, preserved every policy tensor and checkpoint file, and earned zero exploration reward or stuck penalty. Successful-exit times describe different successful subsets; they do not offset fewer clears.

## Completion and waypoint behavior

| Checkpoint on the same 100 room setups | Waypoint inputs enabled | Waypoint inputs masked | Enabled rewarded waypoint arrivals |
| --- | --- | --- | --- |
| QA-22, before this continuation | **64/100** | 55/100 | 201 |
| QA-23, after 1,000 more episodes | **48/100** | 62/100 | **346** |

The child reached more rewarded waypoints, but completed fewer rooms. Episodes with any waypoint bonus increased **85 -> 97**. Arrival rewards are capped at five per attempt; these counts measure rewarded arrivals and depend on trajectories and episode length. They do not establish the policy's internal decision mechanism. The actual exit trigger remains the completion condition.

| Paired comparison (A minus B) | Clear-rate difference | Paired bootstrap 95% interval | A-only / B-only clears | Exact paired p |
| --- | --- | --- | --- | --- |
| Child vs parent, waypoint inputs enabled | **-16 points** | **-30 to -2** | 18 / 34 | 0.0365 |
| Enabled vs masked, child | -14 points | -27 to -1 | 17 / 31 | 0.0595 |
| Child vs parent, waypoint inputs masked | +7 points | -5 to +19 | 23 / 16 | 0.337 |
| Change in enabled-minus-masked gap | -23 points | -40 to -5 | Four outcomes paired by seed | Not computed |

Enabled completion declined on this benchmark, with 18 gained clears and 34 lost clears relative to QA-22. The waypoint-input advantage changed from **+9 to -14 points**. Masking scored higher in the child, although the direct input-effect comparison is close to the 5% boundary: its percentile bootstrap interval excludes zero while its exact paired test has p = 0.0595. Keep both results and avoid a broad claim that masking is universally better. There is no evidence here that this continuation improved useful waypoint navigation; its main completion objective became worse while rewarded waypoint arrivals increased.

All four conditions use the same 100 unique seeds and **exactly matching spawn/exit coordinates**, with all exits initially unseen. Masking zeros only inputs **130-133** during deterministic prediction. Current wall/exit rays and previous movement remain; external navigation and reward collection still operate. Both checkpoints were trained with waypoints, so this is an input ablation, not a separately trained policy without waypoints. The child masked condition earned 105 waypoint bonuses versus the parent's 120.

[Full paired counts, provenance and verification](waypoint-comparison.json); [preserved parent results](../qa-waypoint-20261104-011557-train/README.md).

Intervals use 20,000 paired case-bootstrap resamples, RNG seed 250000. Exact two-sided tests use discordant pairs for binary comparisons. The gap-change interval resamples all four outcomes together by seed. Results are exploratory with no multiple-comparison adjustment; uncertainty covers sampled room setups, not variability across training seeds. Further action/position evidence would be needed to explain the behavior change.

## Checkpoint preservation and settings

Continue the full QA-22 actor, critic and optimizer from **271,368 waypoint-model steps** and **3,500 prior lineage training episodes**, rather than resetting the critic or optimizer. Add **224,997 steps / 1,000 episodes**, reaching **496,365 cumulative waypoint-model steps** and **4,500 total lineage training episodes**. Earlier actor-transfer history remains separate in the parent lineage records.

Parent SHA-256: `0ab90c438098dbd19b540b16fe72092c2e4a190ed6f10efc769fb75c99fa3bd2`. All 26 files covered by its archive manifest remain unchanged.
Final child SHA-256: `13cd921420cb72eb44a2b5c08491589d14c37e134cea518e49e750a2f0a7c98e`.

[Final model](final_model.zip), [22 periodic snapshots](checkpoints/), [model hashes](checkpoints.json). Periodic filenames use cumulative model steps, **281,368-491,368**, corresponding to **10,000-220,000 new steps**. All 23 model archives passed CRC, semantic-version, step-count, byte-size and manifest/hash checks. Intermediate snapshots have not each received independent frozen evaluation; do not label the latest or any intermediate checkpoint best by default.

Same open 40 x 40 task; initially hidden exits 30-34 units away, vision 12 units, waypoint reach six units, 30-second episodes. Contract **local-waypoint-v1**, **134 -> 64 -> 64 -> 2** movement outputs. Completion +50; waypoint +1 capped +5; progress +0.1/unit; action -0.001; existing stuck penalty; exploration 0. PPO learning rate 0.0002, entropy 0.01, gamma 0.99, rollout 256, batch 64, CPU. Only budget, seed range, output folder and parent lineage changed from QA-22; game, navigator and policy sources match.

[Run config](training-config.yaml), [maintained continuation config](../../../../Training/qa-player-waypoint-40x40-continuation-1000.yaml), [source/model provenance](run.json), [frozen control config](frozen-control-config.yaml), [control provenance](waypoint-masked-run.json). The maintained continuation config pins QA-22 to reproduce this run; it does not silently select the child.

[Training summary](training-result.json), [training records](training-episodes.jsonl), [enabled summary](evaluation-result.json), [enabled records](evaluation-episodes.jsonl), [masked summary](waypoint-masked-result.json), [masked records](waypoint-masked-episodes.jsonl).

[Preflight](verification.json) passed **1,100 placements**, eight compass completions, map/barrier/unknown-gap/recovery/reward checks, and 10/10 reference-controller episodes with 185 handoffs and at least five before exit discovery. Reference results are distinct from learned-policy performance. All three Unity sessions closed cleanly, source hashes remained fixed and project settings were restored. [QA changelog](../../../../Training/changelog/qa-player.md). Generated dungeons, obstacle training, combat and standalone ONNX remain deferred.
