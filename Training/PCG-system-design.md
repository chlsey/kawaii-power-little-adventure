# Dungeon PCG and player-training design

This is the game-specific application of the shared RL and QA frameworks. For reusable package setup and contracts, see the [RL package guide](../../unity-reinforcement-learning/README.md) and [QA package guide](../../rein-powered-QA-framework/README.md). [Training/README.md](README.md) provides commands for this game.

## PCG

The goal is to train a PCG model to generate clearable maps. A trained QA bot will attempt to complete each generated map, and its results will provide feedback for PCG training.

### Current Dungeon Generation

The game generates a complete dungeon from a seed and a set of parameters. `PcgPolicyRunner` supports Random and Trained modes. The room-count proof of concept selects one layout parameter through a trained model; the generator still creates the room positions and corridor routes.

- **Parameters:** room count, dungeon width and height, and minimum/maximum room width and height. The generation seed is supplied separately from these seven layout parameters.
- **Setup:** `Generator.RunProgram` seeds randomness, clears the previous dungeon, loads fixed settings and prefab references, and creates an empty grid on Unity's X/Z plane.
- **Rooms:** the generator samples rectangular room positions and sizes, rejects placements that violate boundaries or spacing, and retries failed placements. One room uses the configured boss-room sizes.
- **Connections:** the generator selects nearby room connections, builds straight or L-shaped corridors, and attempts to connect disconnected room groups.
- **Layout and content:** the player spawn is selected using distance from the boss room. Walls, corners, and configured content are added, with an exit zone in the boss room. The checked-in settings currently have no enemy, boss, or decoration prefabs assigned.
- **Validation:** `DungeonValidator` checks that generated objects and a player spawn exist. It does not independently establish that the exit or every room can be reached.

The trained examples control room count alone or room count plus regular-room width and height. The existing generator chooses individual room positions and corridor routes from those parameters and the seed. Adding QA feedback is the next training objective.

### PCG Training Loop

#### Layered Training Flow

The boxes show which repository owns each layer. The arrows show requests going down to Unity and results returning to Python. This is the implemented room-count training path.

```text
+----------------------------------------------------------------+
| PYTHON PROCESS: unity-reinforcement-learning/PythonEnv         |
| train.py: Stable-Baselines3 PPO policy + optimizer             |
|   observation -> action; reward/done -> policy update          |
|                          |                                     |
| UnityGymEnv (unity_env.py): Gymnasium environment              |
+----------------------------------------------------------------+
                          | actions / resets DOWN
                          ^ observations / reward / done UP
                          | localhost TCP, port 7777
+----------------------------------------------------------------+
| UNITY PROCESS: shared RL package /Runtime                      |
| RlTrainingHost + RlProtocol                                    |
|   dispatch callbacks on Unity's main thread                    |
| Contract: RlEnvironmentBehaviour (abstract base class)         |
+----------------------------------------------------------------+
                          | calls game implementation
                          v
+----------------------------------------------------------------+
| UNITY PROCESS: kawaii-power-little-adventure                   |
| PcgRlEnvironment : RlEnvironmentBehaviour                      |
|   ResetEpisode / CollectObservation / ApplyAction              |
|                          |                                     |
| PcgPolicyMapping: target -> obs; action -> parameters          |
|                          |                                     |
| PcgDungeonManager.GenerateDungeon                              |
|                          |                                     |
| Generator.RunProgram -> DungeonValidator                       |
|                          |                                     |
| actual room count + generation/validation result               |
|                          |                                     |
| PcgRlEnvironment.CalculateReward(out isDone)                   |
|   success: 1 - abs(actual - target) / 7; failure: -1           |
|   done = true after each generation attempt                    |
+----------------------------------------------------------------+
                          |
                          +--> result returns UP through host
                               and wrapper to Python PPO
                               -> update -> reset -> repeat
```

`PcgRlEnvironment` implements the shared `RlEnvironmentBehaviour` contract. Python PPO supplies the actions during training; `PcgPolicyRunner` and `RlModelRunner` are used when the exported model runs inside the game.

#### Target QA Feedback Loop

The next objective is to train the PCG policy from actual gameplay attempts. The external Python backend learns the PCG policy through the PCG bridge. Inside Unity, the game controller runs the deployed player model. The optional QA adapter reports final evidence to Python for evaluation against developer-defined requirements. For this PCG use case, the requirement is to clear the generated level under the configured conditions.

- Keep the QA player model fixed throughout a PCG training run so the bot's capabilities stay consistent.
- Train that player model separately through its curriculum, then export and validate a new ONNX version before using it in a later PCG run.
- Use the completion result and configured success conditions to reward or penalize the PCG policy.

```text
PYTHON EXTERNAL BACKEND
PCG policy + PPO optimizer
          | generation action / reset
          v
UnityGymEnv <-> PCG bridge: RlTrainingHost + RlProtocol
          |
          v
UNITY RUNTIME
PcgRlEnvironment -> generator -> structural validation
          |
          v
Game controller runs the candidate test
observation -> fixed player ONNX model -> player action
     ^                                  |
     +---------- updated game state <---+
          |
          v
Developer-defined requirement results + episode metrics
          |
          v
PcgRlEnvironment computes PCG reward + done
          |
          v
PCG bridge -> Python rollout -> PPO update -> next episode
```

One PCG episode covers generation and the configured QA attempt budget:

1. Python selects generation parameters for a candidate. Unity records its identity, seed, parameters and game/settings revision.
2. Unity generates and validates that candidate. A generation or structural-validation failure receives the configured generation penalty and ends the PCG episode without starting QA.
3. Unity resets the QA player on that same map and runs the fixed player ONNX policy at the configured gameplay cadence. The QA runtime adapter encodes observations and decodes player actions using the model's training contract.
4. The game preserves final signals and metrics before resetting the player or destroying the map. If using the Python QA evaluator, the result consumer must explicitly route its result into the pending PCG evaluation; the report protocol does not return evaluated results to Unity.
5. A clear that satisfies the configured conditions produces a positive PCG reward. An unsuccessful gameplay attempt produces a negative PCG reward. The completed PCG transition returns through the bridge with `done = true`; Python then resets for the next candidate.

The existing environment contract exposes `IsActionRunning()`. The clearability adapter should keep the PCG action running while generation and QA evaluation are in progress, then allow `CalculateReward(out isDone)` to return the final result. Unity must continue advancing gameplay while Python waits for that result.

The initial return policy is:

| Outcome | PCG return | Episode handling |
| --- | --- | --- |
| QA reports that the player achieved the developer-defined clear requirement and all configured conditions pass | Positive completion reward | End the episode and reset for the next candidate. |
| Player dies, exceeds the step/time budget, or fails a required completion condition | Negative gameplay reward | Record the failure reason, end the episode and reset. |
| Generation or structural validation fails | Negative generation reward | Skip QA, record generation failure and reset. |
| Model, connection or runtime execution fails | No gameplay reward | Abort the evaluation and report an infrastructure failure; do not use it as evidence about the candidate. |

- Define a clear using the game's completion signal plus the active objectives and episode limits. Generation success alone is insufficient.
- Keep the QA policy's own learning reward separate from the PCG reward assigned to its evaluation result.
- Associate every result with the candidate, QA model version, observation/action schema, game/settings revision and evaluation configuration.
- A failed QA attempt shows that this bot did not clear the map under those conditions; it does not establish that the map is impossible.
- The first PCG objective is clearability. Difficulty, pacing and immersion remain later extensions.
- → Add the positive and negative reward magnitudes, active completion conditions, QA step/time limits, attempt count and rule for combining multiple attempts.

## Planned player curriculum

The curriculum trains the player model to acquire the gameplay capabilities needed for QA tasks. Adding a gameplay feature should add or extend a training stage with observable state, usable actions and developer-defined requirements whose achievement can be measured.

1. Define each stage's test units/levels, developer requirements, enabled features, seed distribution, observation/action schema, player-training reward, episode limits and promotion criteria.
2. Start or resume a compatible QA checkpoint. At each reset, the curriculum selects a scenario and seed from the active stage; Unity resets the player and generates or loads that scenario.
3. Train the player model in Python through the game-owned RL environment. Unity supplies gameplay observations and event signals; the training environment returns the stage's learning reward and explicit outcomes based on the task definition.
4. Include scenarios from earlier stages in later training so new mechanics do not replace basic navigation and completion skills.
5. Periodically freeze a checkpoint and evaluate it on separate validation scenarios and seeds, including earlier stages. Validation runs do not update the policy.
6. Promote the player model when it achieves the stage's developer-defined requirements at the configured performance threshold and passes earlier-stage checks. Otherwise continue training and inspect the failure reasons.
7. Save the checkpoint together with the active curriculum stage, sampling settings, reward configuration, schema and game revision so training can resume with the same task definition.
8. Export a validated player PPO checkpoint to ONNX, verify Python/ONNX/Unity action agreement, and rerun the QA tasks through the deployed runtime path. For PCG clearability training, select a player model whose ability to attempt that clear requirement has been validated.

The curriculum should be organized around the player's required capabilities:

| Stage | Training task | Capability to establish |
| --- | --- | --- |
| Initial QA task | A developer-defined unit/level with a small set of supported mechanics and explicit requirements. | Perform the actions needed to achieve the selected requirements under the configured conditions. |
| Broader task coverage | Variations of those units/levels, seeds and task conditions. | Retain the learned capability beyond a single training scenario. |
| Added game mechanics | Introduce a supported mechanic in focused QA tasks, then combine it with previously learned tasks. | Achieve requirements involving the new feature while retaining earlier player capabilities. |

- Progress the curriculum from simpler tasks to combinations that reflect the current game. Keep the validation set separate from training samples.
- Define the player's learning objective and reward from the stage's task and requirements. Stage-specific rewards may teach prerequisite behavior; they do not become PCG rewards automatically.
- Version the observation/action contract when new features change policy inputs or outputs. Resume directly only when the checkpoint remains compatible; define model transfer or retraining when the contract changes.
- Keep earlier validation stages as regression checks when the game or QA model changes.
- → Add the first stage's developer-defined unit/level and requirements, concrete scenarios and reward terms, training/validation seed sets, replay proportions, evaluation frequency and promotion thresholds.

### Integration sequence

1. Define the first curriculum stage's scenarios, requirements, and promotion criteria around the existing player environment and reliable exit/death/time-limit signals.
2. Add scenario sampling and stage validation to the training workflow, keeping model configuration and run artifacts in the game repository.
3. Train and validate a policy compatible with the current sensor/grid contract, export it, and evaluate it through the existing `QaPolicyRunner` and optional QA recorder.
4. Connect the deployed attempt outcome to `PcgRlEnvironment`, keeping the PCG action running until evaluation finishes; verify reward and reset behavior end to end.
5. Extend the curriculum as gameplay capabilities expand, validating each player model before replacing the fixed PCG evaluator.

## Implemented examples

The examples below use the same shared environment/host and model runner. Their inputs, rewards, ports, scenes, and evaluation outcomes are game-specific choices.

| Example | Implementation and configuration | Contract, results, and deployment |
| --- | --- | --- |
| Room-count generation | `PcgRlEnvironment`, `PcgPolicyMapping`, [pcg.yaml](pcg.yaml) | [Model provenance](../Assets/DemoModels/PCGRoomCount/README.md), [demo scene](../Assets/Scenes/PcgModelDemo.README.md) |
| Room count and size | `PcgRlEnvironment` with room-size training enabled, `PcgLayoutPolicyMapping`, [pcg-layout.yaml](pcg-layout.yaml) | [Model provenance](../Assets/DemoModels/PCGLayout/README.md), [demo scene](../Assets/Scenes/PcgLayoutModelDemo.README.md) |
| Player navigation | `QaPlayerRlEnvironment`, `QaPlayerPolicyMapping`, [qa-player-grid.yaml](qa-player-grid.yaml) | [Training and artifacts](README.md#qa-player-training), [sensor contract](../Assets/PCGDungeon/Scripts/QA/ModelSensorPackage.README.md), [model-test scene](../Assets/Scenes/QaPlayerModelTest.README.md) |
| Attempt reporting | `DungeonQaAdapter`, game-owned Python recorder | [QA setup, evidence, and logs](../QA/README.md) |

The generation models reward parameter matching, not completion. The player-training scene omits QA recording components; reporting is enabled explicitly in the QA scenes. Completion-based PCG feedback and curriculum scheduling remain planned integrations.
