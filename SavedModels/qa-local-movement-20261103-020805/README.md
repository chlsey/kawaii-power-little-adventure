# Saved 40 x 40 local movement result

QA-17 checkpoint: [final_model.zip](final_model.zip). Preserved separately from the first 40 x 40 run.

| 40 x 40 run | Training | Frozen evaluation |
| --- | --- | --- |
| First 500 episodes (QA-16) | 293/500 (58.6%) | 58/100 (58%) |
| Additional 500 episodes (QA-17, this checkpoint) | 314/500 (62.8%) | 81/100 (81%) |

Total: **1,000 training episodes in 40 x 40**, plus 200 evaluation attempts. The full local-policy lineage has 2,000 training episodes and 198,495 model steps.

- Empty room; exits initially unseen, 14-18 units away; 12-unit vision; 30-second episodes.
- `local-vision-v1`: 134 inputs, two movement outputs, no map input or exploration reward.
- Latest evaluation: 19 timeouts, no deaths; successful exits averaged 7.921 seconds. Same 100 placements as QA-16; zero learning updates.
- Obstacles, full-dungeon navigation, visible-exit regression, and ONNX deployment remain unverified for this checkpoint.

Evidence: [training/evaluation results](result.json), [evaluation](evaluation.json), [config](training-config.yaml), [lineage and source hashes](run.json).

Checkpoint SHA-256: `76226e50dee7f0586b4c6b5b1bb8a3f3358980a5983fa712a7e0a413cc22833d`.
