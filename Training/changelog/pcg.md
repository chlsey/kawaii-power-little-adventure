# PCG training change log

Updated through 2026-11-02. This log records the two verified generation-control policies. Their success metrics measure matching requested generator controls; they do not establish that generated levels are clearable.

Last reconciled: 2026-11-07. No further PCG run is recorded; QA-to-PCG reward feedback remains planned.

## PCG-01: room-count policy / 2026-10-18

**Change -> result:** connect PPO to the real Unity generator and learn one room-count control -> the training/export/runtime pipeline worked, but exact target matching remained below the goal.

- Starting point: fresh PPO `MlpPolicy`, one normalized target input and one continuous action, decoded to 8-15 requested rooms. Other generator settings remain fixed.
- Config: learning rate 0.0003, 256-step rollouts, batch size 64, seed 0, 10,000 requested steps. One generated dungeon is one episode; generation/validation failure receives -1. Successful generation receives `1 - abs(actual_count - target_count) / 7`.
- Training: **10,240 episodes**, final reported mean reward **0.843**.
- Held-out evaluation: **40/40 generated successfully**, **15/40 exact target matches (37.5%)**, room-count mean absolute error **0.75** versus **2.725** for the seeded uniform random baseline.
- Runtime check: Python, ONNX Runtime, and standalone Unity actions agreed within **1.79e-7**; model-driven generation worked without Python.
- Interpretation: lower error than the random baseline, but below the 80% exact-match goal. Mean training reward is not an exact-match rate.
- Evidence: [config](../pcg.yaml), [model and provenance](../../Assets/DemoModels/PCGRoomCount/README.md). The original checkpoint/run also lives outside the checkout at `%USERPROFILE%/Documents/PCGTrainingRuns/20261018-093558`.

## PCG-02: room count plus room dimensions / 2026-10-19

**Change -> result:** train a separate policy for room count, regular-room width, and regular-room height -> all three requested targets matched on the recorded 200-case held-out evaluation.

- Starting point: **fresh policy**, not a weight-transfer expansion of PCG-01. Three normalized target inputs and three actions control room count 8-15 and regular-room dimensions 8-12 tiles; the generator still determines positions and corridors.
- Config changes from PCG-01: learning rate **0.0003 -> 0.001**, rollout length **256 -> 512**, batch size **64 -> 128**, requested budget **10,000 -> 30,000**. Actor and critic still use two 64-unit hidden layers.
- Reward change: average the normalized count, generated mean width, and generated mean height errors; successful reward is one minus that average. Failure remains -1. Boss-room dimensions are excluded from the regular-room size metric.
- Training: **30,208 episodes**, final reported mean reward **0.967**.
- Held-out evaluation: seeds **1000000-1000199**, covering 200 target combinations; **200/200 successful generations and 200/200 joint exact matches**. All three mean absolute errors were zero. The random baseline jointly matched **2/200** and had mean errors **2.545 / 1.750 / 1.670** for count/width/height.
- Runtime check: standalone Unity regenerated all 200 target combinations successfully; actions agreed with Python within **2.39e-7**. Python/ONNX Runtime agreement was within **3.58e-7**.
- Interpretation: successful control matching within the tested generator settings and target ranges. Several training settings changed together, so their individual effects were not isolated; this is not a controlled before/after comparison with the simpler PCG-01 task.
- Evidence: [config](../pcg-layout.yaml), [model and provenance](../../Assets/DemoModels/PCGLayout/README.md), [evaluation](../../Assets/DemoModels/PCGLayout/evaluation.json), [Unity inference](../../Assets/DemoModels/PCGLayout/unity-inference.json).

## Proposed next objective: not yet tried

Use a fixed, validated QA player to attempt generated levels and return completion evidence to the PCG learning objective. Completion-based feedback, difficulty, pacing, and immersion have no measured PCG training result in these two experiments. The shared QA reporting path does not feed results into PCG rewards automatically.

See the [target QA feedback design](../PCG-system-design.md#target-qa-feedback-loop). Future PCG entries should identify the fixed QA model, task requirements, attempt budget, result-to-reward rule, and held-out evaluation conditions alongside the generator policy's checkpoint.
