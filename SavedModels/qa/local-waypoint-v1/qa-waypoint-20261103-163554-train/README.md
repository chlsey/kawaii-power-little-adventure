# QA-19: waypoint policy after 500 training episodes

Completed on November 3, 2026 (UTC run `20261103-163554-train`). [Training summary](training-result.json), [frozen evaluation](evaluation-result.json), [config snapshot](training-config.yaml), [source hashes and starting checkpoint](run.json).

| Phase | Seeds | Clears | Timeouts / deaths | Collected steps | Mean successful exit |
| --- | --- | --- | --- | --- | --- |
| Training | 2000-2499 | 468/500 (93.6%) | 32 / 0 | 36,286 | 5.702 s |
| Frozen deterministic evaluation | 200000-200099 | 100/100 (100%) | 0 / 0 | 4,323 | 4.323 s |

Training clears per hundred: 75, 95, 98, 100, 100. Evaluation performed zero weight updates and left every policy tensor and checkpoint hash unchanged. Training earned 712 waypoint bonuses, zero exploration reward and 0.89 total stuck penalty; evaluation earned 149 waypoint bonuses, zero exploration reward and zero stuck penalty. All 600 recorded starts had unseen exits 14-16 units away. Episode reward bonuses stayed within their +5 cap.

## Model and lineage

- Contract: `local-waypoint-v1`, 134 -> 64 -> 64 -> 2, movement X/Y. Previous movement, 64 current wall/exit ray-channel pairs, assigned waypoint validity/direction/distance; map and route remain external.
- Start: [QA-18 zero-step transfer](../qa-waypoint-transfer-start/final_model.zip), SHA-256 `9657ec0745b6163cc14f91fa2dc171a56e0a1cd4c190568d5ceb100f0f58fba9`. Fresh critic and optimizer, with QA-17's actor MLP/head/distribution parameters.
- Actor parent: [QA-17](../../local-vision-v1/qa-local-movement-20261103-020805/final_model.zip), SHA-256 `76226e50dee7f0586b4c6b5b1bb8a3f3358980a5983fa712a7e0a413cc22833d`. Parent history is 2,000 episodes / 198,495 model steps, separate from these 500 new episodes / 36,286 steps. Lineage episode count is 2,500; this model's step counter is 36,286. Both parent/start files remain unchanged.
- Saved checkpoint SHA-256: `67c50b7f30f38eb7194979f4e63ec836c4ee1e45fecc7f2a89961f84631a03cd`.
- Task: open 20 x 20 room, 12-unit vision, unseen exits 14-16 units away, 30-second episodes. PPO learning rate 0.0002, entropy 0.01, gamma 0.99, rollout 256, batch 64, CPU. Rewards and navigation settings match the config and [scene guide](../../../../Assets/Scenes/QaSingleRoomTraining.README.md#waypoint-navigation).

[Preflight verification](verification.json) passed all 600 configured reset placements, eight compass completions, map/clearance/recovery/reward checks, and 10/10 reference-controller clears. Reference results remain separate from learned-policy performance.

On the same first ten evaluation placements used by the zero-step transfer smoke test, clears changed from 5/10 to 10/10: five retained, five gains, zero losses. This is a small paired subset; only those ten have a measured pre-training baseline. The 100/100 result covers this open-room task and does not establish obstacle, generated-dungeon, combat or standalone ONNX performance. Stage advancement remains manual.

The [training episodes](training-episodes.jsonl), [evaluation episodes](evaluation-episodes.jsonl), and [10k/20k/30k snapshots](checkpoints/) are retained with this checkpoint. [File hashes](checkpoints.json) cover all four model files. Full Unity/PPO diagnostics remain local under `Logs/qa-player-waypoint-20x20/20261103-163554-train/`. Unity sessions shut down cleanly, source and parent hashes passed, and project settings were restored. See the [QA changelog](../../../../Training/changelog/qa-player.md).
