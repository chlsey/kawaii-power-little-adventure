# PCG room-count demo model

`pcg_room_count.onnx` is the PPO policy trained against the real Unity dungeon generator on 2026-10-18. It demonstrates the training bridge and ONNX export pipeline; it is not a clearability or difficulty model.

- Input: float32 `obs`, shape `[1, 1]`, containing `2 * (targetRoomCount - 8) / 7 - 1` for targets 8 through 15.
- Output: float32 `actions`, shape `[1, 1]`, containing a deterministic action clipped to `[-1, 1]`. Decode with `PcgPolicyMapping.DecodeRoomCount`; all other layout parameters come from the training Settings asset.
- Format: ONNX opset 15. SHA-256: `ADE566E629E817763DE7C01A22BCADDBCD9DAC2CC193C2E4D95C1532E6DFAB3B`.
- Training: Unity 6000.3.2f1, Stable-Baselines3 2.5.0 PPO/MlpPolicy on CPU, seed 0, 10,000 requested steps, 10,240 completed steps, 327.2 seconds. See `Training/pcg.yaml` for the hyperparameters.
- Results: final reported mean training reward `0.843`. Deterministic decoded counts for targets `8, 9, 10, 11, 12, 13, 14, 15` are `8, 8, 9, 11, 13, 15, 15, 15`: 3/8 exact matches. The reward is not an exact-match success rate. This model falls short of the 80% exact-match goal.
- Runtime verification: maximum SB3/ONNX Runtime/standalone Unity action difference `1.79e-7` across all eight targets. All eight regeneration cases and all 40 held-out seeds `100000..100039` generated successfully. The policy matched 15/40 targets exactly, with mean absolute error `0.75` versus `2.725` for the seeded uniform random baseline.

To run the prewired demo, open `Assets/Scenes/PcgModelDemo.unity` and press Play, or launch `Builds/PcgModelDemo/PcgModelDemo.exe`. The panel displays target, model-selected, and actual room counts. Use its buttons to change the target or regenerate; R also regenerates.

To attach the asset in another scene, select `PcgDungeonManager`, assign this ONNX asset to `RlModelRunner`'s Model Asset field, set `PcgPolicyRunner` Mode to Trained, and choose Target Room Count. Keep its Generation Settings reference equal to the generator's training Settings asset. Startup and R-key regeneration each request one prediction. `InitialGenerationScene` remains in Random mode as a separate starting scene.

The full run remains outside Git at `%USERPROFILE%\Documents\PCGTrainingRuns\20261018-093558`. It contains the frozen player and trainer, `training.log`, `pcg.yaml`, and `artifacts/20261018-134400-819667/final_model.zip`. Use that checkpoint for resumption; this ONNX asset is for inference. The checkpoint is not copied into this demo folder.

See `Training/README.md` and the system design document's PCG room-count example for the implementation and training steps. Use matching versions of the game and shared RL package with the PCG adapter, exporter, and runner accompanying this asset.
