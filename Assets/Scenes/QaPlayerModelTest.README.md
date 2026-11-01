# Trained QA player: 100-attempt level test

This scene needs a replacement model before another trained-player batch. It retains [the million-step player ONNX](../DemoModels/QAPlayer1M/qa_player.onnx), which expects 119 inputs; the current [model sensor pipeline](../PCGDungeon/Scripts/QA/ModelSensorPackage.README.md) produces 37,771. `RlModelRunner` rejects incompatible static input shapes before inference, and the policy does not fall back to Random. Train and export a fresh 37,771-input/five-output policy, then assign it to the scene's `RlModelRunner`. The old model and results remain historical records; see [its provenance](../DemoModels/QAPlayer1M/README.md).

Once a compatible model is assigned, open [QaPlayerModelTest.unity](QaPlayerModelTest.unity) in Unity 6000.3.2f1. This scene duplicates `InitialGenerationScene`, with `QaPolicyRunner` in Trained mode. `RlModelRunner` performs inference locally; the PCG policy generates random dungeons.

## Run a batch

After assigning a compatible model, press Play. The saved `QaRecorderLauncher` starts `python -u QA/record_dungeon_runs.py` in the game repository, with the sibling framework's `src` directory on `PYTHONPATH`. Python must be on PATH; choose an interpreter path in the launcher's Inspector if needed. No separate terminal command or package installation is needed for this sibling-checkout setup.

The recorder opens port 8800. Unity connects through `DungeonQaAdapter`, validates the receiver's TCP readiness message and launch session, then enables the dungeon manager. Reports reuse that same connection. Each Play session writes a fresh `Logs/qa-results/qa-player-model-1m-<UTC timestamp>.jsonl`. The Unity Console shows the exact path, individual results, and cumulative totals. Keep port 8800 free; startup errors leave the dungeon paused and appear in the Console.

```text
+------------------------------------------------------------------+
| STARTUP                                                          |
| Press Play -> launch Python -> validate TCP readiness.           |
+------------------------------------------------------------------+
             |
             v
+------------------------------------------------------------------+
| DUNGEON ATTEMPT                                                  |
| Generate dungeon -> initialize player and sensor map.            |
| Attached DungeonQaAdapter calls StartUnit.                       |
| Fixed ONNX policy reads observations and applies actions.        |
| Exit / death / scheduled reset -> EndUnit.                       |
+------------------------------------------------------------------+
             |
             v
+------------------------------------------------------------------+
| REPORTING                                                        |
| Send final evidence -> Python records pass/fail and totals.      |
+------------------------------------------------------------------+
             |
             v
      100 attempts ended?
             |
             +-- No ---> Generate next dungeon at the next 120-second reset.
             |
             +-- Yes --> Stop simulation. Wait for 100 stored reports,
                         then stop Play.
```

The policy uses the [shared sensor observation contract](../PCGDungeon/Scripts/QA/ModelSensorPackage.README.md#observation-contract) and five action outputs at a 0.1-second cadence. A clear/death ends its unit early; the next dungeon still waits for the scheduled reset. An already-ended attempt is not reported again. The batch contains the initial level plus 99 replacements.

There is no RL training host in this scene, and the model does not update its weights during evaluation. At normal time scale, a batch can take about 3 hours 20 minutes plus generation time. Failed generations do not start/count a player attempt and can extend the batch.

## Read results

Each JSONL line contains the original evidence, a `passed` result, successful `time_to_exit_seconds` or `null`, and cumulative statistics. To read the latest totals in PowerShell:

```powershell
$latest = Get-ChildItem Logs/qa-results/qa-player-model-*.jsonl | Sort-Object LastWriteTime -Descending | Select-Object -First 1
(Get-Content $latest.FullName -Tail 1 | ConvertFrom-Json).summary
```

- `successes`: attempts that reached the exit.
- `failures`: ended attempts that did not reach the exit.
- `total_runs`: successes plus failures; expect 100 for a fresh, fully received batch.
- `success_rate`: successes divided by total runs.
- `mean_exit_seconds`: average successful exit time, or `null` when there are no successes.

Exit time uses the controller's existing simulated action time (`StepCount * 0.1`), while the 120-second reset uses the Unity gameplay clock. Keep the receiver running throughout the batch; totals cover reports actually received and stored. See [the QA guide](../../QA/README.md) for transport behavior and result-file resumption.

## Scene settings

| Component/field | Saved value |
| --- | --- |
| `QaPolicyRunner.mode` | Trained |
| `RlModelRunner.modelAsset` | Retained `QAPlayer1M/qa_player.onnx`; incompatible until replaced with a 37,771-input policy |
| `PcgDungeonManager.qaStepIntervalSeconds` | 0.1 |
| `PcgDungeonManager.autoResetIntervalSeconds` | 120 |
| `PcgDungeonManager.maxQaRuns` | 100 |
| `PcgDungeonManager.maxQaSteps` | 0; the 120-second window controls timeout |
| `PcgDungeonManager.enabled` | Initially false; launcher enables it after Python readiness |
| `QaRecorderLauncher.arguments` | Game-owned recorder with timestamped output |
| `QaRecorderLauncher.adapter` | The same manager's `DungeonQaAdapter`; supplies the host/port and shared TCP connection |

The original `InitialGenerationScene` retains its random player baseline and unlimited batch length. Stop and restart Play to begin a new model-test batch.

Model-specific deployment comparisons and retained QA results are recorded beside [the baseline](../DemoModels/QAPlayerBaseline/README.md) and [million-step model](../DemoModels/QAPlayer1M/README.md). Those evaluations used the earlier observation contract and do not measure a grid-trained policy.
