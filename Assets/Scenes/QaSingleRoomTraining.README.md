# Single-room QA player training

Choose a scene and press Play. Start the external Python trainer from the game repository with that scene's matching config; for example, the small room:

| Scene | Floor size | Config | TCP port |
| --- | --- | --- | --- |
| [QaSingleRoomTraining](QaSingleRoomTraining.unity) | 10 x 10 | [qa-player-single-room.yaml](../../Training/qa-player-single-room.yaml) | 7781 |
| [QaSingleRoomLargeTraining](QaSingleRoomLargeTraining.unity) | 40 x 40 | [qa-player-single-room-large.yaml](../../Training/qa-player-single-room-large.yaml) | 7782 |

```powershell
python ../unity-reinforcement-learning/PythonEnv/train.py --config Training/qa-player-single-room.yaml
```

The shared RL training dependencies must be installed in the chosen Python environment. Each scene listens on its configured localhost port and waits for the trainer; neither contains a QA recorder or report adapter.

## Task and reset

- One fixed room with four solid, sensor-marked walls and no internal obstacles. The large variant has four times the side length and 16 times the floor area.
- Each reset uses its seed to sample both the player spawn and exit position. Both stay at least 1.5 units inside the room boundary and at least 3 units apart.
- The green 2 x 2 exit marker has a trigger matching the dungeon's exit dimensions. Its sensor classification is LevelEnd; walls are Wall.
- Reaching the exit, death, or the 100-second action-time limit ends an episode. The trainer controls the next reset; actors and room geometry are reused.

```text
+----------------------------------------------------------+
| Python resets with seed                                  |
+----------------------------------------------------------+
                |
                v
+----------------------------------------------------------+
| Sample player/exit positions; clear sensor map           |
+----------------------------------------------------------+
                |
                v
+----------------------------------------------------------+
| Observe -> policy action -> physics -> reward / done     |
+----------------------------------------------------------+
                |
                +-- Continue --> Next policy action
                |
                +-- Ended ----> Trainer resets next episode
```

## Model contract and rewards

Single-room training also subtracts 0.01 reward per action after 20 consecutive movement actions (2 simulated seconds) within 0.05 horizontal units of an anchor position. The penalty continues while blocked and resets when the player moves at least 0.05 units from the anchor or stops requesting movement. It uses attempted movement and actual displacement, without requiring hidden obstacle geometry. Successful completion and death do not receive this penalty. This reward is separate from the controller's periodic `StuckEvents` diagnostic counter; full-dungeon training retains its existing rewards.

`QaSingleRoomRlEnvironment` uses the existing `QaPlayerController`, `ModelSensorPackage`, and `QaPlayerPolicyMapping`: 37,771 inputs and five actions, with 0.1 simulated seconds per action. Exit position is not supplied directly to the model; perception comes from its rays and discovered grid.

Rewards match dungeon training: +50 completion, +0.1 per world unit of new closest approach toward a ray-visible exit, -0.001 per action, and +0.001 per newly viewed open cell capped at +0.02 per action and +2 per attempt. Spawn views, wall/exit hit cells, visits, and repeated views earn no exploration bonus; visible exits take priority. The sensor range stays at 12 units in both variants. The small room keeps the exit within range throughout the valid spawn area; the large room can begin with the exit out of sight and requires searching the room. See [the sensor contract](../PCGDungeon/Scripts/QA/ModelSensorPackage.README.md).

The game-owned configs start fresh PPO policies and save checkpoints in separate `Logs/qa-player-single-room-runs/` and `Logs/qa-player-single-room-view-runs/` directories. Set `resume_from` to a compatible checkpoint when continuing a policy. The small-room config requests 500,000 timesteps; the large-room config requests 1,000,000 with `ent_coef: 0.01`. These budgets are not episode-count guarantees: early completions produce more episodes, and PPO completes its final rollout. Exact episode-limited sessions use an episode-count stopping callback and suppress the vector environment's unused reset after the final episode. Keep session configuration, runner snapshots, per-episode metrics, and results in game-owned `Logs/` session directories.

The [full-dungeon training scene](QaPlayerTraining.unity) remains a separate task with its own config and run artifacts.
