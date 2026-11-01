# Million-step QA player model

This retained policy is exported from the fresh 1,000,192-step PPO run and expects the **earlier 119-input layout**. The current [model sensor package](../../PCGDungeon/Scripts/QA/ModelSensorPackage.README.md) produces 37,771 inputs, so this model cannot drive the current player pipeline. It remains assigned to `RlModelRunner` in [QaPlayerModelTest.unity](../../Scenes/QaPlayerModelTest.unity) for provenance; the runner rejects its incompatible static input shape before inference. Train/export and assign a new 37,771-input model before running another trained-player QA batch. The policy does not fall back to Random.

- Source checkpoint: `Logs/qa-player-1m-runs/20261024-092227-760849/final_model.zip`.
- Training config: [qa-player-1m.yaml](../../../Training/qa-player-1m.yaml).
- Checkpoint SHA-256: `641d27eacb23b206331fcb124c45a956cd9c545eb492b54f7f75839c71cd6774`.
- ONNX SHA-256: `c29cc53374431c2546333d49fce84f7588ff23cf871a4931b9f4ac70ec488d41`.
- Training: 0/1,000 clears. Held-out deterministic evaluation: 0/20 clears; all attempts timed out.

| Tensor | Shape | Meaning |
| --- | --- | --- |
| float32 `obs` | `[1, 119]` | The historical observation layout, before `ModelSensorPackage`. |
| float32 `actions` | `[1, 5]` | Deterministic actions clipped to `[-1, 1]`. |

The action mapping uses movement X/Y, attack/interact when positive, and the ignored fifth ability output. The historical observation had seven gameplay values followed by 16 seven-value ray blocks: hit, normalized distance, and one-hot None/Obstacle/Enemy/Loot/Exit. Its 0.1-second action cadence matches training. The scene keeps 120-second reset windows and a 100-attempt cap; with a compatible replacement, Python records/evaluates reports while Unity performs inference.

The shared PPO exporter produced ONNX opset 15. ONNX graph validation passed, and ONNX Runtime actions matched the Python checkpoint on 65 reference cases within `3.58e-7`. See [export-check.json](export-check.json) and [the scene guide](../../Scenes/QaPlayerModelTest.README.md). Model and run artifacts belong to this game repository.

## QA evaluation

| Check | Result |
| --- | --- |
| Model-test batch, historical 119-input contract | 0 successes / 100 failures; all ended at the scheduled 120-second reset. |
| Report accounting | 100 unique attempt IDs; Unity launched and stopped the Python recorder through TCP readiness. |
| Python / Unity actions | Maximum difference `2.98e-7` on 65 reference cases and `1.79e-7` on 128 gameplay observations; attack/interact decoding matched. |

The Unity 6000.3.2f1 evaluation used a fixed 60 Hz clock at time scale 1. [qa-check.json](qa-check.json) records the checks; [the result file](../../../Logs/qa-results/qa-player-model-1m-20261024-122836-1349921.jsonl) contains attempt evidence and totals. Detailed local artifacts are under `Logs/qa-model-1m-evaluation/20261024-122525/` (ignored by Git). These results apply to this retained model and its original observation contract.

To repeat the export from the game directory with the shared export dependencies installed:

```powershell
python ../unity-reinforcement-learning/PythonEnv/export_onnx.py Logs/qa-player-1m-runs/20261024-092227-760849/final_model.zip Assets/DemoModels/QAPlayer1M/qa_player.onnx
```
