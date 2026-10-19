# PCG training POC

Second model: [PCG room count and size](../Assets/DemoModels/PCGLayout/README.md) learns three controls using the same bridge and ONNX runner. Open `Assets/Scenes/PcgLayoutTraining.unity` and use `Training/pcg-layout.yaml` on port 7779 to train it. Open `Assets/Scenes/PcgLayoutModelDemo.unity` to run its saved model. The instructions below describe the original room-count-only example on port 7777.

Use case: Train a policy to choose the requested number of dungeon rooms, then run its exported ONNX model inside the game. This first POC uses room count as its reward; QA completion feedback comes later.

Inputs: One target room count from 8 through 15, normalized to `[-1, 1]`. Reset seed `s` selects target `8 + s % 8`. The policy returns one action in `[-1, 1]`, decoded as `8 + RoundToInt((action + 1) * 3.5)`. Area and room dimensions come from the generator's `GenerationSettings` asset in both training and inference.

Outputs: One complete dungeon per episode. Successful generation and validation earns `1 - abs(actual rooms - target rooms) / 7`; failure earns `-1`. Actual rooms are counted from the generated `Room` components. The current validator checks generated objects and a player spawn room; this reward does not establish that a dungeon is clearable.

Demo: Open `Assets/Scenes/PcgModelDemo.unity` and press Play. The saved ONNX asset is already attached to `RlModelRunner`, and `PcgPolicyRunner` is set to Trained with a target of 12 rooms. The on-screen controls change the target and regenerate the dungeon; R regenerates with a new seed. The panel shows the requested count, model choice, and actual generated count. The current model chooses 13 rooms for a target of 12.

To attach another compatible model, select `PcgDungeonManager` and assign it to the Model Asset field on `RlModelRunner` before pressing Play. `PcgPolicyRunner` is the game-specific adapter: it normalizes the target, calls `Predict`, decodes the action, and supplies parameters to the manager. The shared runner is available through Add Component > Reinforcement Learning > Model Runner. Models with a different observation/action contract need a corresponding game adapter.

Windows demo: `Builds/PcgModelDemo/PcgModelDemo.exe`. The demo scene is selected in Build Profiles. It contains no training host and runs without a Python process.

Run these commands from the game repository with the sibling `unity-reinforcement-learning` repository present:

```powershell
python -m pip install -r ../unity-reinforcement-learning/PythonEnv/requirements-export.txt
python ../unity-reinforcement-learning/PythonEnv/train.py --config Training/pcg.yaml
```

Before starting Python, open `Assets/Scenes/PcgTraining.unity` in Unity 6000.3.2f1 and press Play. The scene's `RlTrainingHost` listens on localhost port 7777. `PcgRlEnvironment` owns generation while enabled, so the local R-key and QA loop stay idle. Unity runs in the background. Each host session serves one Python connection; restart Play before another training or evaluation run.

The config runs PPO on CPU for 10,000 requested timesteps, with 256-step rollouts, batches of 64, and checkpoints every 2,000 steps. PPO finishes its last rollout, so the final total can exceed 10,000. Checkpoints and `final_model.zip` go under `Logs/pcg-runs/`.

Deploy the final checkpoint:

```powershell
python ../unity-reinforcement-learning/PythonEnv/export_onnx.py Logs/pcg-runs/<run>/final_model.zip Assets/DemoModels/PCGRoomCount/pcg_room_count.onnx
```

`Assets/Scenes/PcgModelDemo.unity` is already configured for this model. For another scene, follow the setup in `Assets/Scenes/InitialGenerationScene.unity`: on `PcgDungeonManager`, assign the imported model to `RlModelRunner`, set `PcgPolicyRunner` Mode to Trained, and choose Target Room Count. Keep the policy and generator on the same Settings asset used during training. Startup and R-key regeneration each request one inference. Random mode samples a room count uniformly while using the same fixed layout settings.

Acceptance: Evaluate seeds `100000..100039` (five per target) using deterministic policy actions and a uniform random room-count baseline. Require all generations to succeed, at least 80% exact target matches, and lower room-count mean absolute error than the baseline. Compare SB3, ONNX Runtime, and Unity actions within `1e-5` for all eight target observations, then regenerate repeatedly in a standalone build with Python disconnected.

Validation status: The 11 existing Python bridge/trainer tests pass. Unity 6000.3.2f1 successfully compiled and built the standalone training scene. The completed PPO run generated 10,240 dungeon episodes in 327.2 seconds, ending with reported mean training reward `0.843`. The trained model's SB3, ONNX Runtime, and standalone Unity outputs agree within `1.79e-7` across all eight target observations. All eight model-driven regeneration cases succeeded. All 40 held-out seeds `100000..100039` generated successfully, with 15/40 exact target matches and mean absolute room-count error `0.75`, compared with `2.725` for the seeded uniform random baseline. The deployment pipeline works; the policy still falls short of the 80% exact-match goal. The detailed standalone report and captured game view are in `Logs/pcg-model-e2e/`.

Background run: The standalone player, trainer snapshot, config, and export dependencies for the completed first run are in `C:\Users\chenk\Documents\PCGTrainingRuns\20261018-093558`, outside this checkout. Progress was written to `training.log`, checkpoints to `artifacts/<timestamp>/`, and the final model was automatically exported to `pcg_room_count.onnx` in that run folder. A copy is saved at `Assets/DemoModels/PCGRoomCount/pcg_room_count.onnx`, with its contract, results, and provenance in the adjacent README. Normal Git branch switches do not change a job running entirely from an external snapshot.
