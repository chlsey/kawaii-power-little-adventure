# Legacy grid movement

37,771 -> 64 -> 64 -> 2; gameplay/rays/grid observations with movement-only outputs.

| Run | Checkpoint and evidence | Role / measured result |
| --- | --- | --- |
| QA-09 | [Weights](qa-player-movement-stage1-20261101-190054/final_model.zip), [file hash](qa-player-movement-stage1-20261101-190054/checkpoints.json) | Fresh 40 x 40 stage 1; 19/100 frozen clears. |
| QA-10 | [Weights](qa-player-movement-stage1-20261102-024341/final_model.zip), [file hash](qa-player-movement-stage1-20261102-024341/checkpoints.json) | Same-stage continuation; 13/100 frozen clears. |
| QA-11 | [Weights](qa-player-no-exploration-20261102-170309-10x10/final_model.zip), [file hash](qa-player-no-exploration-20261102-170309-10x10/checkpoints.json) | Fresh 10 x 10, exploration reward disabled; 13/100 frozen clears. |

Preserve each run separately. Different room sizes within this architecture belong together; changed policy meanings or output contracts belong in another architecture folder. Model/config/result snapshots are historical evidence; their recorded original paths have not been rewritten. See [the full QA history](../../../Training/changelog/qa-player.md) and [checkpoint index](../../README.md).
