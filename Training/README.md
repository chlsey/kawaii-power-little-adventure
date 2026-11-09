# Model training

See the [training change logs](changelog/README.md) for the [QA player's iteration history](changelog/qa-player.md) and [PCG experiments](changelog/pcg.md): changes, checkpoint lineage, measured results, and proposals that have not been tried.

## Checkpoint storage

Use the [saved-model index](../SavedModels/README.md) to find retained weights. QA models are grouped under `SavedModels/qa/<architecture>/<run>/`. The current [waypoint workflow](../SavedModels/qa/local-waypoint-v1/README.md) keeps transfers, periodic snapshots, final checkpoints, episode records and summaries together; frozen integration tests live in that architecture's `diagnostics/`. Historical local-vision and grid architectures have separate folders. Maintained YAML configs and runners stay here; generated Python caches and local `Logs/` stay ignored.

The maintained runner writes periodic snapshots into the retained run folder and saves final weights before frozen evaluation. A `checkpoints.json` manifest records relative filenames, byte sizes and SHA-256 values. `Logs/` continues to hold local diagnostics; historical logs have not been deleted. Run-owned configs/JSON remain unchanged historical evidence, including original paths. Use the templates here, whose checkpoint paths reflect the current layout, when launching a new session.

## QA player training

The current system combines a deterministic observed-map/navigation orchestrator with one learned `local-waypoint-v1` movement policy. Read the [ownership and lifecycle contract](PCG-system-design.md#overall-navigation-and-action-design) before changing either layer. Training and frozen evaluation are separate modes; each curriculum change requires an explicit selection.

| Task | Runner and maintained configs | Meaning |
| --- | --- | --- |
| Isolated waypoint skill | `run_waypoint_reach.py`; [nearby](qa-player-waypoint-reach-nearby.yaml), [gradual](qa-player-waypoint-reach-gradual.yaml), [medium](qa-player-waypoint-reach-medium.yaml), [8-12](qa-player-waypoint-reach-8to12.yaml) | No exit or navigator. QA-27 -> QA-30 -> QA-31 -> QA-32 follows 3-6 -> 3-7 -> 6-10 -> 8-12 units. Explicit `--mode train` runs 500 training episodes then 100 frozen evaluations. Default mode is verification. |
| 1-10-unit skill continuation | `run_waypoint_reach.py`; [1-10](qa-player-waypoint-reach-1to10.yaml) | QA-39 is a separate full QA-32 continuation: 500 training episodes then 100 frozen evaluations, no exit/navigator. Use `--mode evaluate` first to measure the configured parent on the same evaluation seeds without learning. |
| Random waypoint-chain curriculum | `run_waypoint_chain.py`; [2 -> 3 -> 4 -> random 1-10](qa-player-waypoint-chain-curriculum.yaml) | Full QA-39 continuation: 100/100/100/200 training episodes, fresh seeded targets, no resets between targets. Divides each leg reward by planned length, combined positive limit +5. Defaults to verification; `--mode train` runs only these four selected blocks and a frozen ten-target comparison. [Scene/session guide](../Assets/Scenes/QaSingleRoomTraining.README.md#random-waypoint-chain-curriculum). |
| Another 500 chain episodes | `run_waypoint_chain.py`; [QA-42 continuation](qa-player-waypoint-chain-continuation.yaml) | Same four-block schedule, full QA-42 optimizer continuation, fresh seeds 11500-11999 and 8,000 starting lineage episodes. Frozen parent replay checks its saved 100-case evaluation before learning; final child uses the same benchmark. |
| Pre-waypoint restart and timed arrivals | `run_waypoint_reach.py` then `run_waypoint_throughput.py`; [3-6](qa-player-waypoint-restart-nearby.yaml), [6-8](qa-player-waypoint-restart-middle.yaml), [8-10](qa-player-waypoint-restart-8to10.yaml), [continuous](qa-player-waypoint-restart-throughput.yaml) | QA-17 actor via zero-step QA-18; 500 episodes per stage. First three end on one arrival, final stage always lasts 60 seconds and rewards every arrival. [Session guide](../Assets/Scenes/QaSingleRoomTraining.README.md#pre-waypoint-restart-and-timed-arrivals). |
| Alternate farther skill branch | `run_waypoint_reach.py`; [farther](qa-player-waypoint-reach-farther.yaml), [farther continuation](qa-player-waypoint-reach-farther-continuation.yaml) | Historical QA-27 -> QA-28 -> QA-29 at 13-18 units. Frozen arrivals fell 51/100 -> 33/100 on the continuation; this branch is separate from QA-32. |
| Frozen distant-exit integration | `run_local_movement.py`; [navigator enabled](qa-player-waypoint-distant-exit-test.yaml), [navigator disabled](qa-player-waypoint-distant-exit-no-navigator.yaml) | QA-32, 500 matched seeds, real exit 30-34 units away, 60 seconds; use `--mode evaluate --episodes 500`. No training. |
| QA-39 longer-goal navigation | `run_local_movement.py`; [30-40-unit goal](qa-player-waypoint-qa39-navigation-30to40.yaml) | Frozen QA-39, 100 cases in a separate 50 x 50 empty room, 10-unit local cap, 12-unit vision, 60 seconds. [Scene setup](../Assets/Scenes/QaWaypoint50Training.README.md), [recorded result: 67/100](../SavedModels/qa/local-waypoint-v1/diagnostics/qa39-navigation-30to40-20261108-175349-evaluate/README.md). No training or generated-dungeon claim. |
| Historical exit curriculum | `run_local_movement.py`; [20 x 20](qa-player-waypoint-20x20.yaml), [40 x 40](qa-player-waypoint-40x40.yaml) | QA-18 actor-only transfer from QA-17, then QA-19 and the larger-room exit continuations. These configs preserve their original parents and 30-second task. |

Select the config with `--config Training/<name>.yaml` from the game directory. The [scene guide](../Assets/Scenes/QaSingleRoomTraining.README.md#waypoint-reaching-skill-drill) owns complete launch commands and preflight behavior; the [checkpoint index](../SavedModels/qa/local-waypoint-v1/README.md) and [QA changelog](changelog/qa-player.md) own results and lineage. All positive waypoint rewards share a +5 attempt budget, below the current +50 local-objective reward floor. Skill arrival ends only the drill; the exit environment requires actual game completion. `test_waypoint_reach.py` and `test_waypoint_reach_batch.py` are the earlier frozen diagnostic launchers, not unit tests or the curriculum runner.

### Historical grid and full-dungeon workflows

The historical [local movement experiment](../Assets/Scenes/QaSingleRoomTraining.README.md#local-movement-experiment) uses the maintained `run_local_movement.py` command to train a 134-input policy without map encoding. The [hidden-exit config](qa-player-local-movement-hidden-40x40.yaml) continues the validated 20 x 20 checkpoint in a 40 x 40 room with the exit initially outside vision. The scene guide owns placement and launch details; the changelog owns results. Historical grid/full-dungeon workflows follow below.

For the simpler navigation task, use [the separate single-room training scene](../Assets/Scenes/QaSingleRoomTraining.README.md) and `Training/qa-player-single-room.yaml`. It provides 10 x 10 and 40 x 40 variants with randomized player and exit positions while retaining the current observations, actions, and rewards. The instructions below cover full-dungeon training.

Open `Assets/Scenes/QaPlayerTraining.unity` in Unity 6000.3.2f1 and press Play. The shared `RlTrainingHost` listens on localhost port 7780. From the game repository, run:

```powershell
python -m pip install -r ../unity-reinforcement-learning/PythonEnv/requirements.txt
python ../unity-reinforcement-learning/PythonEnv/train.py --config Training/qa-player-grid.yaml
```

The game owns the environment, reward, configuration, and run artifacts. The shared RL library supplies the TCP bridge and PPO trainer. The training scene has no QA report adapter or recorder; QA recording is a separate, optional integration described in [the QA guide](../QA/README.md).

```text
+------------------------------------------------------------------+
| PYTHON: PPO trainer + UnityGymEnv                                |
| Read game-owned YAML; save game-owned checkpoints/final model.   |
+------------------------------------------------------------------+
             | five action values
             ^ observation / reward / done
             | RL bridge: TCP 7780
             v
+------------------------------------------------------------------+
| UNITY: player-training environment                               |
| Reset -> generate seeded dungeon -> initialize player and map.   |
| Observe gameplay state + rays + grid window.                     |
| Apply action -> advance 0.1 simulated seconds.                   |
| Capture completed action -> calculate reward and done.           |
+------------------------------------------------------------------+
             |
             +-- Continue --> Return observation; select next action.
             |
             +-- Ended ----> Reset dungeon and sensor map for next episode.
```

Each reset generates a new dungeon seed. `QaPlayerPolicyMapping` encodes **37,771 inputs**: seven gameplay values, 64 `(hit identifier, normalized distance)` ray pairs, and a world-aligned 97 x 97 grid window with four flags per cell. `ModelSensorPackage` retains the full expanding map in Unity and supplies this fixed nearby view. It uses sensed traces rather than generator layout data. See [the sensor contract](../Assets/PCGDungeon/Scripts/QA/ModelSensorPackage.README.md) for ordering and lifecycle details.

Five outputs drive movement X/Y, attack, interact, and an unused ability value. Each action advances 0.1 simulated seconds, with physics completing before reward and observation return. Exit completion, death, or the 100-second episode limit ends the episode; Python then resets. Generation failures stop the session through the bridge error response.

| Reward | Rule |
| --- | --- |
| Completion | +50 on reaching the exit. |
| Time | -0.001 per action. |
| Visible-exit progress | +0.1 per world unit of new closest approach when an exit ray hits before the action. |
| Viewed open space | +0.001 per newly ray-revealed open cell, capped at +0.02 per action and +2 per attempt. |

The first sensed exit surface point becomes the fixed progress target for the attempt. Hidden approach updates the closest-distance record without a bonus, so retreating or changing visibility cannot repay earlier progress. Rewards do not read the dungeon manager's exit position. Exploration rewards exclude the spawn scan, wall/exit hit cells, and visits. They pay only for previously unknown open cells revealed by rays after the action, and are suppressed when the exit is visible before/after the action or the action completes the level. Suppressed and capped discoveries are consumed; rescans and revisits cannot repay the same cells.

## Configure and save runs

| Config | Requested timesteps | Output directory |
| --- | --- | --- |
| [qa-player-single-room.yaml](qa-player-single-room.yaml) | 500,000 | `Logs/qa-player-single-room-runs/` |
| [qa-player-single-room-large.yaml](qa-player-single-room-large.yaml) | 1,000,000 | `Logs/qa-player-single-room-view-runs/` |
| [qa-player-grid.yaml](qa-player-grid.yaml) | 1,000,000 | `Logs/qa-player-grid-runs/` |
| [qa-player-grid-smoke.yaml](qa-player-grid-smoke.yaml) | 2,000 | `Logs/qa-player-grid-smoke/` |

The large-room config starts a fresh policy with `ent_coef: 0.01` to encourage varied actions. The shared trainer also accepts an explicit entropy coefficient when resuming a policy.

PPO completes its final 256-step rollout, so the smoke config normally collects 2,048 steps and may begin a third episode. A timestep budget does not guarantee an exact episode count. Restart Play before connecting another trainer.

The trainer requires `--config`; relative `output_dir` and `resume_from` paths resolve beside that YAML file. Each run gets a timestamped directory with checkpoints and `final_model.zip`. Keep these in the model-owning game or its chosen external directory. `Logs/` artifacts are local and ignored by Git. Start a fresh policy for the current layout: retained 119-input checkpoints cannot resume with 37,771 inputs. The older `qa-player.yaml`, `qa-player-smoke.yaml`, and `qa-player-1m.yaml` recipes remain available, but the active Unity scene determines observations and rewards.

## Deploy and evaluate

Install the shared export dependencies, then export a completed compatible checkpoint from the game directory:

```powershell
python -m pip install -r ../unity-reinforcement-learning/PythonEnv/requirements-export.txt
python ../unity-reinforcement-learning/PythonEnv/export_onnx.py Logs/qa-player-grid-runs/<run>/final_model.zip Assets/DemoModels/QAPlayerGrid/qa_player.onnx
```

Assign the export to `RlModelRunner` in [QaPlayerModelTest](../Assets/Scenes/QaPlayerModelTest.README.md). Training and inference share the observation/action mapping. The runner rejects incompatible static input shapes; the scene still references the earlier 119-input million-step model and needs a 37,771-input/five-output replacement. The evaluation scene runs a fixed policy without weight updates and can record 100 attempts through the optional QA integration.

| Evidence | Interpretation |
| --- | --- |
| [Grid smoke result](../Logs/qa-opt-in-smoke/20261024-175910/result.json) | Two complete training episodes, both timeouts; checkpoint reload and finite five-action output verified. This exact-episode check used a temporary stopping callback, rather than the ordinary CLI timestep budget. |
| [Baseline model](../Assets/DemoModels/QAPlayerBaseline/README.md) | Retained 119-input model, provenance, deployment checks, and results. |
| [Million-step model](../Assets/DemoModels/QAPlayer1M/README.md) | Retained 119-input model, provenance, and 100-attempt QA results. |

The grid smoke verifies the training pipeline; it does not demonstrate learned level clearing. A full grid-policy training and evaluation run is still required. QA outcomes do not currently feed PCG rewards.

## PCG parameter training

See [the game-specific design](PCG-system-design.md) for generator architecture and planned player-evaluation feedback.

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
