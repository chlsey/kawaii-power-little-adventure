# Dungeon PCG and player-training design

This is the game-specific application of the shared RL and QA frameworks. For reusable package setup and contracts, see the [RL package guide](../../unity-reinforcement-learning/README.md) and [QA package guide](../../rein-powered-QA-framework/README.md). [Training/README.md](README.md) provides commands for this game.

## Overall navigation and action design

The QA player has two control levels: a deterministic C# **orchestrator** maintains observed-map memory, goals, A* routes and recovery; a learned **local policy** chooses the next movement action. The policy is a feed-forward PPO actor with two 64-unit hidden layers. Training the local actor does not train the orchestrator.

A waypoint carries a higher-level instruction toward a gameplay objective. The local policy receives that instruction alongside current sensors; the game determines whether the actual objective has been completed. The orchestrator never moves the actor directly or completes the objective by declaring a waypoint reached.

```text
+------------------------------------------------------------------+
| Game world -> sensor rays and observed traces                    |
+------------------------------------------------------------------+
                              |
                              v
+------------------------------------------------------------------+
| Persistent observed map -> reachable goal selector -> A* route   |
| Prefer a discovered exit; otherwise explore an observed frontier |
+------------------------------------------------------------------+
                              |
                              v
+------------------------------------------------------------------+
| Navigation coordinator: retain goal, route and recovery state    |
| Assign one currently visible waypoint with actor clearance       |
+------------------------------------------------------------------+
                              |
                              v
+------------------------------------------------------------------+
| Local policy: current rays + previous movement + waypoint        |
| Choose movement X/Y; no map or distant-route policy inputs       |
+------------------------------------------------------------------+
                              |
                              v
+------------------------------------------------------------------+
| Game controller -> movement -> physics -> new observation        |
| Actual exit trigger determines room completion                   |
+------------------------------------------------------------------+
                              |
                              +-- Continue --> Capture sensors again
                              |
                              +-- Ended ----> Record attempt; reset
```

| Responsibility | Owner and current behavior |
| --- | --- |
| Perception and memory | `ModelSensorPackage` and `ModelSensorGrid` retain observed free cells, walls, exits and visits for one attempt. Unknown space remains unknown. The navigator receives no generator layout or hidden exit coordinates. |
| Objective selection and routing | `IMapGoalSelector` chooses reachable discovered exits or exploration frontiers. `QaObservedRoutePlanner` plans through observed free space; `QaNavigationCoordinator` retains the objective, validates its waypoint and handles route recovery. |
| Local action decisions | `QaWaypointPolicyMapping` supplies the `local-waypoint-v1` policy with previous movement, current wall/exit ray channels and the assigned waypoint. The current model has two movement outputs. |
| Reward and completion | `QaWaypointRewardTracker` bounds all positive waypoint shaping. `QaSingleRoomRlEnvironment` combines it with time/stuck costs and actual exit completion. Changing a target earns nothing; waypoint arrival never substitutes for the exit trigger. |
| Learning and evaluation | The game owns scenes, contracts, rewards, configs and checkpoint lineage. The shared Python/Unity bridge supplies PPO training. Frozen evaluation uses deterministic actions and performs no optimizer updates. |
| QA evidence | The optional QA framework transports attempt evidence and evaluates developer-defined requirements. It does not choose goals, issue actions or train either control layer. |

### Orchestrator state and decisions

`ModelSensorPackage` casts 64 world-space rays with 12-unit range. `ModelSensorGrid` retains their observed free space, wall/exit hits and visits in 0.25-unit cells for the attempt. The map is external memory for the orchestrator; it is not part of the local policy input. Unknown cells remain blocked to route planning. Placement code knows the sampled exit position for setup and measurement, but navigation uses only sensed knowledge.

`IMapGoalSelector` is the replaceable selection boundary. The current `QaFrontierGoalSelector` first chooses a reachable discovered exit when exit pursuit is allowed; otherwise it selects the nearest reachable exploration frontier at least one unit away. `QaObservedRoutePlanner` uses eight-neighbour A*, blocks diagonal corner cutting and inflates observed walls by actor radius + skin width + 0.05 units. Selection and routing have no learned parameters.

| Retained state | Lifetime and use |
| --- | --- |
| Observed grid | Accumulates sensed knowledge for one attempt; survives individual waypoint changes. |
| Goal cell and goal type | Preserve the navigation intention across local actions and waypoint handoffs. A newly reachable exit can supersede an exploration goal. |
| Route, active waypoint and assignment version | Choose the furthest usable route point within the configured maximum distance. The coordinator default is 10 units; historical batch configs retain a 6-unit maximum unless `max_waypoint_distance` is set. Shorter route points remain valid for turns, nearby goals and final approach. It must cross known free cells and pass current visibility and actor-clearance checks. A new assignment increments the version; the version is bookkeeping, not a policy input. |
| Closest approach, stalled actions and replan flag | Detect lack of progress and distinguish the first replan from repeated failure. |
| Excluded frontiers and deferred-exit flag | Avoid retrying completed/abandoned frontier locations within 0.5 units; allow exploration to seek a new approach after exit pursuit fails. |

The coordinator checks the instruction at each action boundary. Arrival within 0.5 units, invalidated clearance/known space, or a newly selected exit can require a new route/waypoint. Twenty completed actions without at least 0.05 units of new closest approach trigger one replan; repeated failure abandons that goal. Abandoning a frontier excludes it; abandoning an exit defers exit pursuit until an exploration goal is completed. This can temporarily select exploration even when an exit has been observed. With no usable goal it reports `no_reachable_goal` and supplies no instruction; the local policy still runs with zero waypoint features.

### Local policy contract

`QaWaypointPolicyMapping` defines **local-waypoint-v1**, with 134 inputs and two continuous outputs. Rays, direction and movement share world X/Z axes, independent of the actor's facing.

| Input offsets, zero-based | Meaning |
| --- | --- |
| 0-1 | Previous movement X/Z. |
| 2-129 | 64 current ray pairs: separate wall and exit strengths, each `1 - normalized hit distance`; zero for no matching hit. |
| 130 | Assigned waypoint present: 1 or 0. |
| 131-132 | Normalized world X/Z direction from actor to assigned waypoint. |
| 133 | Waypoint distance / 12-unit sensor range, clamped to [0, 1]. |

When there is no assignment, inputs 130-133 are all zero; current exit rays remain available. A direct skill drill can supply an instruction beyond vision, where direction remains meaningful but the distance feature saturates. Normal orchestrator assignments remain visible and within the configured cap (10-unit coordinator default; historical batch recipes default to six). The cap is a maximum, not an 8-unit minimum. The [10-unit evaluation recipe](qa-player-waypoint-max10-evaluate.yaml) sets it explicitly; a 10-unit assignment encodes distance as 10/12.

The actor outputs movement X/Z in [-1, 1]. `QaPlayerController` normalizes movement magnitude above one and applies real controller/physics movement at four units per game second over 0.1-second actions. It receives no whole map, route, hidden exit position, goal type, assignment version, recovery state, health or threat features through this contract. The current model cannot choose attack, defend, interact or ability actions. Its PPO critic estimates training value; it is not an orchestrator.

Historical **local-vision-v1** also has 134 inputs and two outputs, but its final four inputs summarize a currently visible exit. Equal tensor shapes do not make these meanings interchangeable. Config, readiness metadata and checkpoint metadata must agree on the contract.

### Action lifecycle and reset

1. Reset the seeded scenario and player, begin a fresh sensor map, clear the waypoint reward history/budget, then reset navigation and select the first assignment from sensed space.
2. Encode current rays, previous movement and that assignment; Python selects an action using the local policy. Frozen evaluation uses deterministic actions; training collects transitions for PPO updates.
3. Apply movement and let Unity physics advance for the action interval, including exit/death callbacks. Simulation continues while the shared host waits for `IsActionRunning()` to become false.
4. `CalculateReward(out isDone)` captures the resulting sensor state, rewards progress against the **previously assigned** waypoint and checks actual exit completion, death or the deadline. A new assignment itself earns no reward.
5. If the attempt continues, advance the coordinator using the new observed map, perform any handoff/recovery and assign the next reward baseline. The host then collects the resulting observation. This reward-before-observation order makes the next action see the updated instruction.
6. On an ended attempt, retain terminal evidence before the next reset. Only a new attempt clears all observed knowledge, exclusions, deferred-exit/replan state and reward history. `ResetNavigation()` alone invalidates/rebuilds navigation while preserving map knowledge and the already consumed reward budget.

### Waypoints and local objective priority

Waypoints express where to pursue the higher-level intention; they are not gameplay achievements. In the real exit task, entering the actual exit trigger sets completion. Arriving at a waypoint only advances navigation. In the isolated `QaWaypointReachRlEnvironment` drill, arrival ends the skill episode while `game_completed` remains false and no exit-completion reward is paid. The separate `QaWaypointChainSession` continuous exercise instead replaces each reached instruction and always runs for its full 60-second budget. It measures arrivals per minute, preserves movement/sensor state across handoffs and supplies no exit or navigator; see the [timed exercise guide](../Assets/Scenes/QaSingleRoomTraining.README.md#pre-waypoint-restart-and-timed-arrivals).

New closest approach pays +0.1 per unit and a new arrival pays up to +1. **All positive approach and arrival rewards combined share a +5 budget for the entire attempt**, below the default +50 actual-objective completion reward. Assignment changes and navigation resets never replenish that budget. Arrival must approach from outside the 0.5-unit radius, and a rewarded location within 0.5 units cannot pay again. Retreat does not create repayable progress for the same assignment. The exit environment also charges -0.001 per action, supports a sustained-stuck penalty and pays no exploration reward in waypoint mode; the isolated skill drill omits the stuck penalty too.

The budget must stay strictly below the smallest active local-objective completion reward; introducing smaller-reward objectives requires reducing it. The current exit environment rejects completion rewards at or below the waypoint budget. This constrains reward magnitude across the attempt, but discounting, learned behavior and conflicting tasks still need explicit evaluation. A high waypoint score is not evidence of local-objective priority or successful gameplay.

### Failure evidence and current limits

Record actual completion/death/timeout, game seconds, initial/final distance, observed exit discovery, assignments, replans and abandoned goals. Assignment counts include recovery replacements and are not arrival counts. Terminal navigation status is a snapshot: an exit trigger can complete an attempt before the next coordinator refresh, so `exploring` at completion does not establish that the exit was ignored.

The isolated drill tests following an instruction; full exit attempts test perception, memory, goal selection, routing and local control together. The navigator-disabled control bypasses selection/planning/recovery, keeps sensor rays, and supplies zero waypoint inputs. Merely masking the four inputs while still running navigation is a different ablation. Neither control compares against an independently trained navigator-free policy.

The current bridge reports one `done` flag; `UnityGymEnv.step()` maps every ended attempt to `terminated=True`, `truncated=False`, including time limits. Separating timeout truncation and value bootstrapping remains unresolved training work. The frozen comparisons perform no updates and do not test a correction to that behavior. Empty-room performance also leaves obstacle traversal by the learned policy, competing local objectives and generated-dungeon completion unvalidated.

The [scene guide](../Assets/Scenes/QaSingleRoomTraining.README.md#waypoint-navigation) owns launch/preflight details. The [checkpoint index](../SavedModels/qa/local-waypoint-v1/README.md) and [QA changelog](changelog/qa-player.md) own measured results and checkpoint selection.

**Planned local decision behavior:** local objectives such as survival, combat or interaction should take priority over following a navigation instruction. Future threat/objective observations and an expanded policy must let the bot evade, defend, fight or interact locally before resuming the retained navigation intention. An explicit interruption/resumption contract should preserve the goal, avoid treating the interruption as navigation failure and revalidate the route afterward; the current coordinator still counts stalled actions and has no such pause signal. These decisions require policy and scenario changes, with competing-objective evaluations. A learned orchestrator could later replace goal selection under a versioned contract, but no such model or training loop is implemented.

**Planned PCG connection:** once the QA policy is validated and deployed, freeze it while it attempts each generated candidate. Route actual completion and developer-defined QA requirements into the PCG learner's separate clearability reward. The QA player's waypoint rewards train its own behavior; they are not a generated map's PCG score. Generated-dungeon integration, combat decisions and standalone waypoint ONNX deployment remain deferred. The existing generation-training path and planned QA-to-PCG loop are described below.

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

The game owns scenarios and learning rewards; the shared RL framework supplies training and inference. Establish individual gameplay capabilities before combining them into complete QA attempts.

1. Define each stage's scenarios, supported mechanics, observation/action contract, seeds, rewards, episode limits, and measurable promotion criteria. Resume only a compatible checkpoint; contract changes require explicit transfer or a fresh model.
2. Train on the active stage, retaining earlier scenarios as skills expand. Freeze checkpoints for evaluation on separate seeds, including earlier-stage regression checks; evaluation never updates weights.
3. Review task success and failure reasons, then advance manually only after the requirements pass. Save the checkpoint with its stage, sampling/reward settings, contract, and game revision.
4. Export a validated policy to ONNX, verify Python/ONNX/Unity action agreement, and evaluate complete QA attempts before selecting it as the fixed PCG evaluator.

The current curriculum separates local skill from integrated navigation:

| Stage family | Scope and current role |
| --- | --- |
| Historical exit curriculum | QA-17's actor transferred into QA-18 with a fresh critic/optimizer; QA-19 trained in an open 20 x 20 room, followed by 40 x 40 exit continuations through QA-24. These retain their original 30-second limit and results. |
| Isolated instruction following | Empty 40 x 40 room, centre spawn, random fixed waypoint, no exit/coordinator, 60 seconds. The successful branch progresses 3-6 -> 3-7 -> 6-10 -> 8-12 units. QA-32 remains the preserved 8-12-unit baseline; its separate QA-39 child continues on [1-10-unit targets](qa-player-waypoint-reach-1to10.yaml). Neither standalone drill validates moving navigator handoffs. The separate 13-18-unit fork remains archived without promotion. |
| Pre-waypoint restart and timed throughput | QA-17 actor via zero-step QA-18, fresh critic/optimizer, then full continuations through three single-target distance bands (3-6, 6-8, 8-10) and a full-minute continuous exercise, 500 episodes per selected stage. Timed shaping scales each new closest approach/arrival by 0.015 so credit remains available within the combined +5 cap; input/history semantics stay unchanged. [Guide and pinned recipes](../Assets/Scenes/QaSingleRoomTraining.README.md#pre-waypoint-restart-and-timed-arrivals); [run-owned results](../SavedModels/qa/local-waypoint-v1/qa-waypoint-throughput-20261108-233001-train/README.md). |
| Frozen distant-exit integration | QA-32 with the real map/frontier/A* coordinator; unseen actual exit 30-34 units away, 12-unit vision, waypoints within six units, 60 seconds. QA-33/34 compare enabled versus physically disabled navigation on 500 matched cases without training. |
| Deferred capabilities | Obstacles currently have routing/reference verification fixtures, not validated learned traversal. Generated-dungeon integration, local combat/interaction, learned orchestration and standalone waypoint ONNX deployment remain future work. |

The [training guide](README.md#qa-player-training) maps each stage to its runner/config. A config names its exact parent rather than following the latest checkpoint automatically. Actor-only transfer starts a fresh critic, optimizer and model-step counter; full continuation retains them. Keep branch steps distinct from total actor-lineage episodes, and exclude episodes from alternate forks. Neither frozen integration nor a status check advances the curriculum.

### Policy strategy options

Selected direction: the [orchestrator and local policy contract](#overall-navigation-and-action-design), applying the [RL design's developer-owned orchestration pattern](../../../google-docs/reinforcement-learning-framework-system-design.md#orchestrator-and-local-policy-integration).

```text
Sensors -> External map -> Goal selector -> Route planner
                                                |
                                                v
                                       Visible waypoint
                                                |
                                                v
                                     Local movement policy
                                                |
                                                v
                                    Move -> Observe -> Repeat
```

The deterministic orchestrator is already implemented in the game integration; it is not a hierarchical feature supplied or jointly trained by the shared RL framework. Other policy patterns, including specialists and learned memory, remain options for explicit future experiments. Keep their contracts and evaluation separate from the current two-output movement policy.

### Integration sequence

1. Establish reliable movement through the room-size curriculum and separate evaluation.
2. Extend validated capabilities to searching, obstacles, and additional mechanics; use a future policy pattern only when an experiment justifies it.
3. Connect the fixed deployed player's attempt outcome to `PcgRlEnvironment`, holding the PCG action open until evaluation finishes. Validate reward/reset behavior end to end before training for clearability.

## Implemented examples

The examples below use the same shared environment/host and model runner. Their inputs, rewards, ports, scenes, and evaluation outcomes are game-specific choices.

| Example | Implementation and configuration | Contract, results, and deployment |
| --- | --- | --- |
| Room-count generation | `PcgRlEnvironment`, `PcgPolicyMapping`, [pcg.yaml](pcg.yaml) | [Model provenance](../Assets/DemoModels/PCGRoomCount/README.md), [demo scene](../Assets/Scenes/PcgModelDemo.README.md) |
| Room count and size | `PcgRlEnvironment` with room-size training enabled, `PcgLayoutPolicyMapping`, [pcg-layout.yaml](pcg-layout.yaml) | [Model provenance](../Assets/DemoModels/PCGLayout/README.md), [demo scene](../Assets/Scenes/PcgLayoutModelDemo.README.md) |
| Player navigation | `QaPlayerRlEnvironment`, `QaPlayerPolicyMapping`, [qa-player-grid.yaml](qa-player-grid.yaml) | [Training and artifacts](README.md#qa-player-training), [sensor contract](../Assets/PCGDungeon/Scripts/QA/ModelSensorPackage.README.md), [model-test scene](../Assets/Scenes/QaPlayerModelTest.README.md) |
| Local waypoint skill | `QaWaypointReachRlEnvironment`, `QaWaypointPolicyMapping`, [8-12 skill config](qa-player-waypoint-reach-8to12.yaml) | [Skill drill](../Assets/Scenes/QaSingleRoomTraining.README.md#waypoint-reaching-skill-drill), [checkpoint index](../SavedModels/qa/local-waypoint-v1/README.md) |
| Orchestrated exit navigation | `QaSingleRoomRlEnvironment`, `QaNavigationCoordinator`, [frozen integration config](qa-player-waypoint-distant-exit-test.yaml) | [Distant-exit scenario and control](../Assets/Scenes/QaSingleRoomTraining.README.md#frozen-distant-exit-integration-test), [system contract](#overall-navigation-and-action-design) |
| Attempt reporting | `DungeonQaAdapter`, game-owned Python recorder | [QA setup, evidence, and logs](../QA/README.md) |

The generation models reward parameter matching, not completion. The player-training scene omits QA recording components; reporting is enabled explicitly in the QA scenes. Completion-based PCG feedback and curriculum scheduling remain planned integrations.
