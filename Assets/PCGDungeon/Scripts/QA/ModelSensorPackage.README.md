# Model sensor package

- `ModelSensorPackage` owns ray perception and a persistent discovered map for one player attempt.
- `PcgDungeonManager` attaches it to prefab and placeholder players and calls `BeginAttempt()` after player initialization. Another game can attach the component to an actor root and use the same lifecycle from its own adapter.
- Mapping uses player position and sensed traces. It does not read generator layouts, room identities, or dungeon dimensions.

```text
+------------------------------------------------------------------+
| ATTEMPT START                                                    |
| BeginAttempt -> clear map and capture spawn view.                |
+------------------------------------------------------------------+
             |
             v
+------------------------------------------------------------------+
| MODEL TICK                                                       |
| Capture rays and traces -> update persistent grid.               |
| ReadSnapshot -> rays and nearby map window.                      |
| Shared observation encoder -> Python policy or local ONNX.       |
+------------------------------------------------------------------+
             |
             v
+------------------------------------------------------------------+
| ACTION RESULT                                                    |
| Apply player action -> capture completed action before reward.   |
| Training: calculate reward. Inference: read snapshot.            |
+------------------------------------------------------------------+
             |
             v
        Attempt ended?
             |
             +-- No ---> Return to the next model tick.
             |
             +-- Yes --> EndAttempt: capture final view and freeze snapshot.
```

Gameplay callbacks can end an attempt during an action. Reward and terminal-observation collection then read the frozen sensor snapshot. Sensor memory and model control work independently of the optional QA reporting components.

## Configure vision

| Setting | Value |
| --- | --- |
| Ray count | 64, fixed by the model contract |
| Ray range | 12 world units by default; editable in the Inspector |
| Ray height | 0.5 world units above the actor's position by default; editable in the Inspector |
| Directions | Horizontal world-space XZ ring, independent of actor rotation |
| Ordering | Ray 0 points along +Z; each next ray rotates +5.625 degrees around world Y, so ray 16 points along +X |

- Add `ModelSensorTarget` to an object's collider or ancestor and select its **Hit Id**. Wall and corner prefab roots are marked Wall; the generated exit is marked LevelEnd.
- Each ray selects the nearest marked collider, including trigger colliders. A nearer wall hides an exit behind it.
- Self colliders and unmarked objects are ignored. Floors and room triggers do not intercept vision; enemies and loot have no sensor classification in this version.
- The latest traces retain world-space origins, endpoints, and hit identifiers for map updates.
- Choose an origin height that intersects the relevant wall and exit colliders for the actor and geometry.

## Persistent grid and attempt lifecycle

- The sensor stores a `ModelMapCell[,]` with four flags per cell: **Observed, Wall, Exit, Visited**. All flags unset means Unknown.
- Cells cover 0.25 x 0.25 world units on the X/Z plane. World cell coordinates are `floor(position / 0.25)`, including negative coordinates.
- Storage starts at 128 x 128 cells around the spawn cell and doubles along each axis as needed. Expansion preserves existing cells and discovered-wall counts.
- `Capture()` traces each ray through grid cells. Cells before the endpoint become Observed; a Wall or LevelEnd endpoint sets its corresponding flag. A miss marks the trace through maximum range Observed.
- Hit endpoints are assigned just inside the sensed surface for stable cell assignment. Gaps between rays and space behind hits remain Unknown.
- Wall and Exit flags persist for the attempt; clear-space traces cannot erase them. This version assumes static boundaries and exits within an attempt.
- Each capture marks the player's current cell Observed and Visited. Seeing open space and visiting it remain separate facts.
- `DiscoveredWallCellCount` counts unique wall cells for diagnostics. `ViewedOpenCellCount` counts previously unknown open cells revealed by ray traces; marking a position Visited or detecting a wall/exit endpoint does not increment it.
- Capture at reset and before/after completed model actions. There is no sensor `Update` or `FixedUpdate` polling; rendering frame rate does not drive exploration.
- `ReadSnapshot()` reads captured data without consuming reward. `EndAttempt()` captures the final view and freezes it for the terminal observation before regeneration or player cleanup.
- `BeginAttempt()` explicitly resets map/history even when a player is reused. Disabling/re-enabling the component does not clear exploration memory.

## Observation contract

| Hit identifier | Meaning |
| --- | --- |
| `0` | None: no marked target within range |
| `1` | Wall: boundary or marked obstacle |
| `2` | LevelEnd: the exit target |

- Each `QaRayObservation` contains `HitId` and `Distance01`: collider distance divided by ray range, clamped to `0..1`. A miss uses identifier `0` and distance `1`.
- Inputs `0..6` retain normalized health, raw powerup count, previous movement X/Y, previous attack/interact flags, and previous ability.
- Inputs `7..134` contain 64 `(hit identifier, normalized distance)` pairs.
- Inputs `135..37770` contain a world-aligned **97 x 97** map window centered on the player's current cell, covering 24.25 x 24.25 world units.
- Each cell contributes four binary values in order **Observed, Wall, Exit, Visited**. Rows run from negative to positive world Z, with X increasing inside each row. Unallocated cells read as Unknown.
- Total input size is **37,771 values**; action outputs remain five values. The full expanding map remains in the sensor while the model receives this fixed-size nearby view.
- [QaPlayerRlEnvironment.CollectObservation()](../RL/QaPlayerRlEnvironment.cs) and local inference use the same snapshot encoder in [QaPlayerPolicyMapping](../Policy/QaPlayerPolicyMapping.cs).
- Snapshot arrays are borrowed read-only buffers. Encode them before the next capture; reading a snapshot does not scan or consume reward.

## Exploration and exit rewards

`QaPlayerRlEnvironment` and `QaSingleRoomRlEnvironment` use captured exit visibility and `ViewedOpenCellCount` to calculate rewards. The sensor supplies data; it does not award or consume rewards. The environments baseline the spawn scan and consume post-action discoveries once, including suppressed or capped discoveries. Only newly ray-revealed open cells earn exploration bonuses; wall sightings, visited positions, and repeated views do not.

See [the training reward rules](../../../../Training/README.md#qa-player-training) for magnitudes, caps, and exit priority. Viewed open cells measure new visual coverage; the map does not recognize distinct rooms or plan routes.

## Train and deploy

- Start a fresh policy using [qa-player-grid.yaml](../../../../Training/qa-player-grid.yaml). It saves runs under the game repository's `Logs/qa-player-grid-runs/` and does not resume an earlier model.
- The shared Python bridge negotiates the fixed input size from Unity; no new wire schema is required. See [the training workflow](../../../../Training/README.md).
- Retained baseline and million-step models expect 119 inputs and cannot consume the grid observation.
- Export and assign a compatible 37,771-input/five-output ONNX before using the [model-test scene](../../../Scenes/QaPlayerModelTest.README.md). The runner rejects incompatible static shapes without padding or a random-policy fallback.
- The [training guide](../../../../Training/README.md#deploy-and-evaluate) links the current grid smoke result and retained model evaluations. Pipeline checks do not establish learned level completion.

## Check an integration

Verify ray ordering/filtering, grid traversal and expansion, unknown gaps, sticky flags, window alignment, reset clearing, and terminal freezing. Compare training/local encodings and confirm discoveries are rewarded once with the configured caps and exit priority.

Local [grid verification](../../../../Logs/qa-grid/20261024-165659/verification.json) and [map-window capture](../../../../Logs/qa-grid/20261024-165659/map-window.png) provide detailed integration evidence.
