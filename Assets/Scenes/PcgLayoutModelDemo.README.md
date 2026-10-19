# PCG Room Count and Size Demo

Open `PcgLayoutModelDemo.unity` beside this README and press Play. This second demo uses a trained ONNX model to choose **room count, regular-room width, and regular-room height**. The original `PcgModelDemo.unity` still runs the first, room-count-only model.

## Setup and run

1. Open `kawaii-power-little-adventure` in **Unity 6000.3.2f1**. Keep `unity-reinforcement-learning` in a sibling folder; the game references that shared package locally.
2. Wait for Unity to resolve packages, import assets, and compile scripts.
3. Open **Assets > Scenes > PcgLayoutModelDemo**.
4. Press **Play** and select the **Game** view. The model and components are already connected. Python is not needed.

## Controls and expected behavior

The scene generates a dungeon immediately, viewed from above. The upper-left panel compares the requested values with the model's chosen values and the generated result. The initial request is **12 rooms, 10 tiles wide, 10 tiles tall**; the saved model chooses those same values. It matched all three controls for all 200 target combinations in evaluation.

| Control | Result |
| --- | --- |
| Fewer rooms / More rooms | Change the requested count within 8-15 and regenerate. |
| Narrower / Wider | Change regular-room width within 8-12 tiles and regenerate. |
| Shorter / Taller | Change regular-room height within 8-12 tiles and regenerate. |
| Generate dungeon (R) / R key | Regenerate with a new seed and the same three targets. Click the Game view first for keyboard input. |

For fixed targets, the model's choice is deterministic; the seed changes the room positions and corridors. All regular rooms use the model's chosen width and height. The boss room retains its separate Settings dimensions, so it can have a different size. Visuals are placeholders, and any QA capsule uses the existing placeholder behavior.

```text
Requested count + width + height
                |
                v
PcgPolicyRunner -> RlModelRunner -> pcg_layout.onnx (CPU)
                |
                v
PcgLayoutPolicyMapping -> generation parameters + seed
                |
                v
PcgDungeonManager -> generator + validator -> displayed dungeon
```

The model supplies parameters to the existing procedural generator. The demo does not train a model or run a trained QA bot, and it does not establish map clearability.

For the tensor contract, reward, saved checkpoint, measured results, and concrete training/export steps, see the [model README](../DemoModels/PCGLayout/README.md). When swapping models, assign the asset before Play and keep `PcgPolicyRunner` in **Trained / Room Count And Size** mode.

To make a Windows player, use **File > Build Profiles**, select Windows, and enable this scene as the only scene for the build. Windows Build Support is required. Share the whole output folder. `Builds/` is ignored by Git, so a checkout does not include the local executable.
