# Local vision and exit movement

134 -> 64 -> 64 -> 2; previous movement, current rays and visible-exit summary.

| Run | Checkpoint and evidence | Role / measured result |
| --- | --- | --- |
| QA-14 | [Weights](qa-local-movement-20261102-225827/final_model.zip), [file hash](qa-local-movement-20261102-225827/checkpoints.json) | Fresh 10 x 10 visible-exit policy; 100/100 frozen clears. |
| QA-15 | [Weights](qa-local-movement-20261102-232854/final_model.zip), [file hash](qa-local-movement-20261102-232854/checkpoints.json) | 20 x 20 visible-exit continuation; 100/100 frozen clears. |
| QA-16 | [Weights](qa-local-movement-20261103-010610/final_model.zip), [file hash](qa-local-movement-20261103-010610/checkpoints.json) | 40 x 40 hidden-exit continuation; 58/100 frozen clears. |
| QA-17 start copy | [Weights](qa-local-movement-20261103-010610-continuation-start/final_model.zip), [file hash](qa-local-movement-20261103-010610-continuation-start/checkpoints.json) | Preserved QA-16 copy; same checkpoint hash, no additional training. |
| QA-17 | [Weights](qa-local-movement-20261103-020805/final_model.zip), [file hash](qa-local-movement-20261103-020805/checkpoints.json) | Latest saved 40 x 40 checkpoint; 81/100 frozen clears. Actor parent for waypoint transfer. |

Preserve each run separately. Different room sizes within this architecture belong together; changed policy meanings or output contracts belong in another architecture folder. Model/config/result snapshots are historical evidence; their recorded original paths have not been rewritten. See [the full QA history](../../../Training/changelog/qa-player.md) and [checkpoint index](../../README.md).
