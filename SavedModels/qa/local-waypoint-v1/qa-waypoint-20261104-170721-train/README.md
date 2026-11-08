# QA-24: 500-episode exit-priority retry

**Complete; all training and evaluation sessions stopped.** Trained **500 new episodes**, with **232 clears (46.4%)**, 268 timeouts and no deaths. Frozen evaluation cleared **54/100 with waypoint inputs versus 50/100 masked**. Parent enabled completion was 48/100; the **+6-point change has a paired 95% interval of -4 to +16**, so a completion improvement is not established. This child is retained as an experiment without promotion over earlier candidates.

## Configuration and lineage

Resume the latest [QA-23 checkpoint](../qa-waypoint-20261104-084138-train/README.md), including actor, critic and optimizer: SHA-256 `13cd921420cb72eb44a2b5c08491589d14c37e134cea518e49e750a2f0a7c98e`. Starting model steps **496,365** and prior lineage episodes **4,500** remain distinct from this run's **116,999 new steps / 500 episodes**. Final model steps: **613,364**; total lineage training episodes: **5,000**. No critic or optimizer reset.

Actual exit completion reward increased **+50 -> +200**. Waypoint arrival stays **+1, capped +5/episode**, approach progress +0.1/unit, time -0.001/action, existing stuck penalty and exploration 0. Same open **40 x 40** room, initially unseen exits **30-34 units** away, vision 12, waypoints within six units, 30-second episodes. No barriers are used in training. Contract **local-waypoint-v1**, actor **134 -> 64 -> 64 -> 2** movement outputs. PPO learning rate 0.0002, entropy 0.01, gamma 0.99, rollout 256, batch 64, CPU; other PPO settings inherited unchanged. Training seeds **4500-4999**; 150,000-step ceiling, stopped at exactly 500 episodes.

[Actual training config](training-config.yaml), [maintained config](../../../../Training/qa-player-waypoint-exit-priority.yaml), [run/source provenance](run.json), [training result](training-result.json), [500 episode records](training-episodes.jsonl). The maintained config remains pinned to QA-23 for reproducibility; it does not automatically resume this child. Checkpoints record `training_completion_reward: 200`; historical configurations without an override retain +50.

## Measured results

| Phase | Clears | Timeouts | Mean successful exit | Rewarded waypoint arrivals |
| --- | --- | --- | --- | --- |
| New training, 500 episodes | 232/500 (46.4%) | 268 | 15.775 s | 1,660 |
| Parent frozen, enabled | 48/100 | 52 | 13.292 s | 346 |
| Parent frozen, masked | 62/100 | 38 | 12.440 s | 105 |
| New frozen, enabled | 54/100 | 46 | 14.448 s | 337 |
| New frozen, masked | 50/100 | 50 | 11.222 s | 104 |

No deaths in any condition. All new phases recorded zero exploration reward and zero stuck penalty. Training clears per consecutive hundred: **42, 43, 52, 45, 50**. Training made 4,570 optimizer updates. Both new evaluation phases made **zero updates**; policy tensors, step counters and checkpoint bytes stayed unchanged. Successful-only times concern different successful subsets and are descriptive.

All four frozen conditions use the same **100 unique seeds 250000-250099 and exact spawn/exit placements**. The masked control zeros only observation inputs 130-133; navigation, ray channels, rewards and completion rules remain active. [Enabled result](evaluation-result.json), [enabled records](evaluation-episodes.jsonl), [masked result](waypoint-masked-result.json), [masked records](waypoint-masked-episodes.jsonl), [masked provenance](waypoint-masked-run.json), [frozen control config](frozen-control-config.yaml), [paired analysis and checks](waypoint-comparison.json).

| Paired comparison | Change in percentage points | Paired bootstrap 95% interval | Exact paired p |
| --- | --- | --- | --- |
| New enabled minus parent enabled | +6 | -4 to +16 | 0.327 |
| New enabled minus new masked | +4 | -9 to +16 | 0.636 |
| New masked minus parent masked | -12 | -20 to -4 | 0.00754 |
| Change in enabled-minus-masked gap | +18 | +5 to +31.03 | Not a binary paired test |

The enabled comparison gained 16 cases, lost 10, retained 38 clears and failed 36 in both. The gap moved from -14 to +4 points, largely because masked performance declined. Neither overall completion improvement nor a positive latest waypoint-input benefit is established. The gap increase is evidence of changed sensitivity to the inputs on this case set; it is not sufficient evidence of reliable waypoint following.

Intervals use 20,000 paired case-bootstrap resamples, RNG seed 250000; gap changes resample all four outcomes together by seed. Exact tests use discordant pairs. These exploratory comparisons are unadjusted for multiple testing and cover case sampling, not variability across independent training runs.

This is one continuation with a larger exit reward. There is **no parallel 500-episode unchanged-reward continuation**, so before/after changes cannot be attributed solely to the reward. Parent evaluation used +50 and child evaluation +200; compare completion/arrival behavior, not raw returns across reward scales. Frozen input masking tests waypoint observations, not the arrival-bonus reward itself.

## Navigation behavior and remaining design issue

On discovery of a reachable exit, the next 0.1-second action boundary replaces the exploration goal with the exit route. A visible exit 6-12 units away is approached through local waypoints within six units. The observed exit cell can become the waypoint when close enough and clear. Only the actual exit trigger completes the room. The local actor has waypoint direction/distance and exit ray channels, without an explicit exit-pursuit flag; the external map remembers discovered exits.

Current recovery can defer a repeatedly failed exit approach and select exploration even while the exit is visible. Keeping a known reachable exit as the goal through local recovery is **proposed**, not implemented in this trial. Navigation/observation/geometry sources and scene assets are byte-identical to QA-23. Only three runtime source files changed: the runner, Editor session and existing verification checks, to carry, apply, validate and record the configured reward. [Owning scene guide](../../../../Assets/Scenes/QaSingleRoomTraining.README.md#waypoint-navigation).

## Verification and preservation

Deterministic reference verification is separate from learned-policy performance: **10/10 reference clears**, 185 handoffs, all ten exits discovered, at least five handoffs before discovery in every attempt. All **600 configured starts** were unseen; eight compass completions paid the actual +200 reward. Route/barrier, unknown-gap, clearance, recovery, reward-cap and reset checks passed. The fixed barrier is verification-only. [Verification record](verification.json). Standalone preflight and the training session's preflight both passed.

Final checkpoint: [final_model.zip](final_model.zip), SHA-256 `78548d2ca50e90f61b737e9f74a6bf983c7682ea96c2e431b94cf71810076c36`. Retained **11 periodic snapshots plus the final model**, all ZIP CRCs, metadata, byte counts and hashes verified. Periodic filenames use cumulative steps: 506,365 through 606,365; final steps are 613,364. [Checkpoint manifest](checkpoints.json), [complete archive manifest](files.json). All **37 files in the preserved parent archive** still match their hashes, including its final checkpoint.

Native training, enabled evaluation and masked evaluation exited successfully. Project settings were restored and source hashes remained stable throughout. Detailed local-only logs: [training/enabled session](../../../../Logs/qa-player-waypoint-exit-priority/20261104-170721-train/), [masked session](../../../../Logs/qa-player-waypoint-exit-priority/waypoint-control/20261104-185226-evaluate-without-waypoint/), [standalone verification](../../../../Logs/qa-player-waypoint-exit-priority/20261104-170250-verify/). No further training or curriculum stage started; no commit, push or document sync.
