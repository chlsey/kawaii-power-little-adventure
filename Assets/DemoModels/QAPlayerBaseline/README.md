# QA player baseline model

This retained ONNX policy is the first 100,096-step player-training baseline and has not demonstrated level clearing: training cleared 0/100 attempts and held-out evaluation cleared 0/20. It expects the **earlier 119-input layout** and cannot consume the current [model sensor package's 37,771-input observation](../../PCGDungeon/Scripts/QA/ModelSensorPackage.README.md). The shared `RlModelRunner` rejects that static input-shape mismatch. Preserve this asset as a historical baseline; train a fresh policy for the current pipeline.

## Model contract

| Tensor | Shape | Meaning |
| --- | --- | --- |
| float32 `obs` | `[1, 119]` | Health, powerup count, previous action, and 16 classified sensor rays. |
| float32 `actions` | `[1, 5]` | Deterministic policy outputs clipped to `[-1, 1]`. |

- Observation fields 0–6: normalized health, raw powerup count, previous move X, previous move Y, previous attack flag, previous interact flag, and previous ability slot.
- Each remaining seven-value ray block contains hit, normalized distance, and one-hot None/Obstacle/Enemy/Loot/Exit classification. Rays followed the historical controller's world-space order.
- Decode actions 0–1 as movement X/Y, action 2 as attack when positive, and action 3 as interact when positive. The current game adapter ignores action 4 and supplies ability slot 0.
- Actions use the training cadence of 0.1 simulated seconds. The current [QaPlayerRlEnvironment](../../PCGDungeon/Scripts/RL/QaPlayerRlEnvironment.cs) uses the new 37,771-input layout; it no longer supplies this model's historical encoding.

## Evaluation and deployment

| Evaluation | Result |
| --- | --- |
| Training | 0/100 clears. |
| Held-out evaluation | 0/20 clears. |
| Standalone Unity inference | Both 1,000-action attempts timed out. |
| Python / ONNX Runtime / Unity comparison | 65 reference cases and 2,000 gameplay observations; maximum Python/Unity absolute difference `3.58e-7` against a `1e-5` tolerance. Attack/interact decoding matched. |

The opset-15 export was verified with Unity 6000.3.2f1, Inference Engine 2.4.1, Stable-Baselines3 2.5.0, PyTorch 2.6.0, ONNX 1.18.0, and ONNX Runtime 1.22.0. Deployment compatibility does not establish level-clearing ability. [deployment-check.json](deployment-check.json) records validation, build diagnostics, and standalone outcomes.

The [model-test scene guide](../../Scenes/QaPlayerModelTest.README.md) describes fixed-policy evaluation and QA recording. That scene references the later million-step model; both retained policies require replacement for the current sensor contract.

## Provenance

- Source checkpoint: `Logs/qa-player-runs/20261024-045547-797535/final_model.zip`.
- Source config: [Training/qa-player.yaml](../../../Training/qa-player.yaml).
- Checkpoint SHA-256: `f6af559b1268c46c1da7de9ca622be69f6ead5d140afc1aa3a3972357f5af7dc`.
- ONNX SHA-256: `69b0df600d3e4ebd6a13b4870b07b17552ebaee1b48826fc6af2f4da9318b799`.
- Local probe build, source, observations, and logs: `Logs/qa-player-deployment/20261024-064835/` (ignored by Git).

To repeat the export from the game directory with the shared export dependencies installed:

```powershell
python ../unity-reinforcement-learning/PythonEnv/export_onnx.py Logs/qa-player-runs/20261024-045547-797535/final_model.zip Assets/DemoModels/QAPlayerBaseline/qa_player.onnx
```
