# PCG room-count and size demo model

`pcg_layout.onnx` is a second PPO policy trained against the real Unity dungeon generator. It learns three generation controls: room count, regular-room width, and regular-room height. The original room-count-only model remains in `../PCGRoomCount/`.

This uses the same PPO `MlpPolicy` architecture as the first example: separate actor and critic networks with two 64-unit Tanh hidden layers. The expanded parameters are the dungeon controls, with three inputs and three outputs.

Open [PcgLayoutModelDemo.unity](../../Scenes/PcgLayoutModelDemo.unity) in **Unity 6000.3.2f1** and press **Play**. The model is already attached. Python and a trainer are not required to run the demo. Keep the sibling `unity-reinforcement-learning` package beside the game repository so Unity can resolve the local package reference. The [scene README](../../Scenes/PcgLayoutModelDemo.README.md) covers setup and controls.

## Model contract

| Tensor | Shape | Values, in order |
| --- | --- | --- |
| float32 `obs` | `[1, 3]` | Requested room count, regular-room width, regular-room height, each normalized to `[-1, 1]`. |
| float32 `actions` | `[1, 3]` | Deterministic actions clipped to `[-1, 1]`, decoded into those same three controls. |

Room count ranges from 8 through 15. Width and height each range from 8 through 12 tiles. Normalization is `2 * (target - minimum) / (maximum - minimum) - 1`; decoding is `minimum + RoundToInt((action + 1) * (maximum - minimum) / 2)`.

`PcgLayoutPolicyMapping` defines this contract for both training and inference. The decoded width and height apply to every regular room. It sets each minimum to the chosen size and each maximum to size plus one because the generator uses Unity's exclusive integer upper bound. The area stays at 100 x 100, and the boss room keeps its separate dimensions from the existing Settings asset. The procedural generator still chooses room positions and corridors using the seed; the model does not output tiles or a complete layout.

For deployment in another scene, add or use `RlModelRunner`, assign this model to **Model Asset**, and connect it to `PcgPolicyRunner`. Set **Mode** to **Trained** and **Parameters Mode** to **Room Count And Size**. Set the three targets and use the same Generation Settings asset as the generator. Assign the model before pressing Play. The original `[1, 1]` room-count model uses **Room Count** mode instead.

## Training and return policy

Each episode generates and validates one dungeon. Reset seeds cycle through all 200 combinations of the three requested targets. A successful generation receives:

```text
count_error  = abs(generated_count - target_count) / 7
width_error  = abs(mean_regular_width - target_width) / 4
height_error = abs(mean_regular_height - target_height) / 4
reward       = 1 - (count_error + width_error + height_error) / 3
```

Failed generation or validation receives `-1`. The environment counts generated `Room` components and reads the dimensions recorded by the generator on each regular room, excluding the boss room. It rewards the generated result rather than only the model's requested parameters. One action ends the episode.

This reward measures matching the three requested controls. It does not use a trained QA bot or prove that a map is clearable.

To repeat training, run these commands from the game repository after installing the dependencies in the shared framework's `PythonEnv/requirements-export.txt`:

```powershell
python -m pip install -r ../unity-reinforcement-learning/PythonEnv/requirements-export.txt
```

Open **Assets/Scenes/PcgLayoutTraining.unity** and press **Play**, then run:

```powershell
$env:OMP_NUM_THREADS = '1'
$env:MKL_NUM_THREADS = '1'
python ../unity-reinforcement-learning/PythonEnv/train.py --config Training/pcg-layout.yaml
```

The bridge listens on localhost port **7779** with three observations and three actions. Restart Play before a new trainer or evaluation connection. `Training/pcg-layout.yaml` requests 30,000 PPO steps on CPU, with 512-step rollouts, batches of 128, learning rate `0.001`, and a checkpoint every 5,000 steps. The trainer finishes its last rollout, so completed steps can exceed the request. Checkpoints go under `Logs/pcg-layout-runs/<run>/`.

Export the final checkpoint and open the demo scene to use it:

```powershell
python ../unity-reinforcement-learning/PythonEnv/export_onnx.py Logs/pcg-layout-runs/<run>/final_model.zip Assets/DemoModels/PCGLayout/pcg_layout.onnx
```

This folder also contains `final_model.zip`, the resumable PPO checkpoint for the saved example. To resume it, set `resume_from: ../Assets/DemoModels/PCGLayout/final_model.zip` in the layout config and start a fresh training session. ONNX is the inference artifact, not the resumable checkpoint.

## Results and provenance

Trained on **2026-10-19** with Unity 6000.3.2f1, Stable-Baselines3 2.5.0, and PyTorch 2.6.0 on CPU, using seed 0 and one PyTorch thread. The run completed **30,208 episodes in 702.8 seconds**. Final reported mean training reward was `0.967`.

The deterministic policy was evaluated through the Unity bridge on 200 unseen layout seeds, covering every target combination:

| Metric | Trained policy | Uniform random baseline (seed 0) |
| --- | --- | --- |
| Successful generations | 200/200 | 200/200 |
| All three targets matched exactly | 200/200 | 2/200 |
| Room-count mean absolute error | 0 | 2.545 |
| Width mean absolute error | 0 | 1.750 |
| Height mean absolute error | 0 | 1.670 |
| Mean evaluation reward | 1.000 | 0.594 |

SB3 and ONNX Runtime actions agree within `3.58e-7` across all 200 targets. The exported model uses **ONNX opset 15**, with SHA-256 `9E6D0DFEB2D54234BB3A13A6CA3D7360E9BFE30A5B51189A0E2AFF4A54B83F02`.

The standalone Windows Unity demo also regenerated all 200 target combinations successfully using the attached ONNX asset and the same policy/manager calls used by the controls. Actual room counts and regular-room dimensions matched the model's choices in every case. Unity and SB3 actions agreed within `2.39e-7`. The default request produced **12 rooms at 10 x 10 tiles**. This player ran with no training host; see [unity-inference.json](unity-inference.json). A temporary verification helper was used for these checks and removed from the final demo.

The saved [evaluation.json](evaluation.json) records the completed step count, model SHA-256, default prediction, and deterministic evaluation against seeds `1000000..1000199`. Those 200 unseen layout seeds cover all 200 target combinations. It compares the model with a seeded uniform random baseline and records SB3 versus ONNX Runtime action differences. The full local training and evaluation logs are under `Logs/pcg-layout/`; these logs and standalone builds are ignored by Git.

Exact matching means all three controls match together. Mean reward is a separate metric and must not be interpreted as the exact-match success rate. This evaluation covers unseen seeds with the same generator settings and target ranges used in training.
