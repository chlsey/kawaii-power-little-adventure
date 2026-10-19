# PCG Model Demo

Open `PcgModelDemo.unity` beside this README and press Play. The scene already has the trained ONNX model, inference component, dungeon manager, generator, and camera connected. It runs inference locally on the CPU; Python and a running trainer are not required.

## Setup and run

1. Install **Unity 6000.3.2f1** through Unity Hub.
2. Keep the game and its shared RL package in sibling folders, using the versions supplied with this demo:

   ```text
   source-code/
   +-- kawaii-power-little-adventure/   <- Unity project
   +-- unity-reinforcement-learning/   <- shared Unity package
   ```

   The game's `Packages/manifest.json` references the package as `file:../../unity-reinforcement-learning`. Both repositories must include the demo implementation. Unity resolves the inference dependencies automatically when importing the project.
3. In Unity Hub, add and open `kawaii-power-little-adventure`. Wait for package resolution, asset import, and script compilation to finish.
4. In the Project window, open **Assets > Scenes > PcgModelDemo**.
5. Press **Play** and select the **Game** view. No Inspector setup or model assignment is needed.

## What to expect

The scene generates a dungeon immediately, viewed from above. Rooms and corridors use simple placeholder visuals. The panel in the upper-left shows the requested room count, the model's choice, the actual generated room count, and the layout seed.

The default request is **12 rooms**. This saved model chooses **13**, so the panel should initially show:

```text
Requested rooms: 12
Model chose: 13
Generated rooms: 13
```

The model is an early proof of concept and does not always match the request. The 12-to-13 result is expected for this model.

| Control | Result |
| --- | --- |
| Fewer rooms / More rooms | Change the requested count within 8-15 and immediately regenerate. |
| Generate dungeon (R) / R key | Generate another layout with a new seed and the same request. Click the Game view first for keyboard input. |
| Stop Play | End the demo. Changes made during Play Mode are temporary. |

For a fixed request, inference is deterministic: regenerating changes the layout seed while keeping the model's chosen room count.

## What it is doing

```text
Requested room count
        |
        v
PcgPolicyRunner: normalize the request
        |
        v
RlModelRunner: run pcg_room_count.onnx on the CPU
        |
        v
PcgPolicyRunner: decode the count and apply seed/settings
        |
        v
PcgDungeonManager: request generation
        |
        v
Dungeon generator + validator -> displayed dungeon
```

The model chooses the room-count parameter. The existing procedural generator places the rooms and corridors using that count, a seed, and the fixed generation settings. The model does not directly draw the layout.

This demo demonstrates the deployed policy driving the real generator. It does not run training or a trained QA bot, and it does not establish that the generated maps are clearable. Any QA capsule uses the existing placeholder behavior.

## Model and build

The bundled model is [pcg_room_count.onnx](../DemoModels/PCGRoomCount/pcg_room_count.onnx). To try another compatible model, select **PcgDungeonManager** and assign it to **RlModelRunner > Model Asset** before pressing Play. Keep **PcgPolicyRunner > Mode** set to **Trained**. The adapter expects one normalized float observation and one float action; see the [model README](../DemoModels/PCGRoomCount/README.md) for its contract and measured results.

For a Windows player, use **File > Build Profiles**, select Windows, and build with `Assets/Scenes/PcgModelDemo.unity` enabled. It is already enabled in the project. Windows Build Support must be installed for the editor. A local build can be placed at `Builds/PcgModelDemo/PcgModelDemo.exe`; `Builds/` is ignored by Git, so a fresh checkout needs a build or a supplied copy. Share the entire build folder, including its data folder and libraries.

For the separate training scene, Python setup, reward definition, and export steps, see [Training/README.md](../../Training/README.md).
