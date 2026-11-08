# Legacy grid gameplay actions

37,771 -> 64 -> 64 -> 5; grid observations with movement, attack, interact and ability outputs.

| Run | Checkpoint and evidence | Role / measured result |
| --- | --- | --- |
| QA-06 | [Weights](qa-player-single-room-view-entropy-20261026/final_model.zip), [file hash](qa-player-single-room-view-entropy-20261026/checkpoints.json) | Fresh viewed-space/entropy campaign; 114/1,000 training clears; no frozen evaluation recorded. |
| QA-08 | [Weights](qa-player-single-room-stuck-penalty-20261031-221905/final_model.zip), [file hash](qa-player-single-room-stuck-penalty-20261031-221905/checkpoints.json) | Stuck-penalty restart from QA-06; 92/1,000 training clears; no frozen evaluation recorded. |

Preserve each run separately. Different room sizes within this architecture belong together; changed policy meanings or output contracts belong in another architecture folder. Model/config/result snapshots are historical evidence; their recorded original paths have not been rewritten. See [the full QA history](../../../Training/changelog/qa-player.md) and [checkpoint index](../../README.md).
