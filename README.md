# Kawaii Power Little Adventure

Unity game and game-owned QA/PCG training workflows for CSC495. Start with the [design](Training/PCG-system-design.md), [training guide](Training/README.md), or [saved checkpoint index](SavedModels/README.md).

The current QA player combines a deterministic observed-map/navigation orchestrator with one learned `local-waypoint-v1` movement policy. The [system design](Training/PCG-system-design.md#overall-navigation-and-action-design) defines their state, interfaces, recovery and reward priority. Reaching an instruction waypoint does not complete the gameplay objective.

The latest skill checkpoint, **QA-32**, reached 99/100 assigned 8-12-unit waypoints in frozen evaluation. In the separate 500-case distant-exit test, that same frozen policy cleared **364/500 with the navigator versus 236/500 with it disabled**. These are empty-room results; generated-dungeon and combat integration remain planned. [Checkpoint roles and evidence](SavedModels/qa/local-waypoint-v1/README.md) preserve earlier baselines and the unsuccessful farther-distance branch alongside the current skill branch.

| Folder | Contents |
| --- | --- |
| [Assets/](Assets) | Game code, Unity scenes and deployed model assets. |
| [Training/](Training/README.md) | Maintained YAML launch configs, Python runners, design guide and `changelog/` histories. |
| [SavedModels/](SavedModels/README.md) | Retained Python checkpoints grouped by architecture/run, plus frozen-test `diagnostics/`; manifests record checkpoint hashes. |
| [QA/](QA/README.md) | Optional QA recording and reporting integration. |
| `Logs/` | Ignored local runtime diagnostics and historical session artifacts. Retained current-workflow weights/evidence also live in `SavedModels/`. |

Use the matching config and policy contract for a checkpoint. Training, evaluation, and manual stage advancement are separate actions. Commands are maintained in the [scene guide](Assets/Scenes/QaSingleRoomTraining.README.md#waypoint-navigation). Shared package responsibilities are documented in the [RL](../unity-reinforcement-learning/README.md) and [QA framework](../rein-powered-QA-framework/README.md) repositories.
