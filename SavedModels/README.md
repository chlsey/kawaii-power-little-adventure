# Saved checkpoints

Latest pending evaluation: [QA-58](qa/local-waypoint-v1/qa-mixed-exit-20261114-072247-train/README.md) completed another 1,000 episodes (997 exits), final checkpoint retained. Frozen mixed completion stays 120/150; original two-room and empty retention are pending. No deployment.

The current QA workflow is [local waypoint control under a deterministic navigator](qa/local-waypoint-v1/README.md). **[QA-57](qa/local-waypoint-v1/qa-mixed-exit-20261111-084731-train/README.md)** is the latest saved experiment: another 3,000 unique completed episodes from QA-56 at unchanged LR 0.000002, with 2,993 exits / seven timeouts. Frozen mixed exits recover **101 -> 120/150**, while original two-room exits regress **56 -> 45/100**; both paired changes are supported on reused cases. Empty waypoint mean changes 45.49 -> 45.58. QA-55 remains slightly ahead on both counts (121/150 and 50/100). Unity-disconnect recovery and lost progress are disclosed; no completed seeds were repeated. All 106 archives verified and prior models preserved. This branch has 10,000 mixed-task / 14,700 completed lineage episodes; QA-51 is an excluded alternate fork. Geometry overlap disclosed. No additional learning or deployment.

**[QA-50](qa/local-waypoint-v1/qa-waypoint-recovery-20261109-224625-train/README.md)** remains preferred over QA-51/52/53/54: recovery 52.29 arrivals/minute, empty 45.41, mixed exits 117/150 and original two-room exits 57/100. It continued QA-47 for 100 refresh + 300 wall/corner episodes at LR 0.00002; its two-room gain over QA-47's 54/100 is modest. QA-55 now has a higher mixed-exit count (121/150) but remains below QA-50 on original two-room exits (50/100); preserve both task-specific alternatives. QA-47 and all earlier baselines remain preserved. No automatic deployment follows from the newest run, and the separate endless-sequence setup has not trained.

[QA-44](qa/local-waypoint-v1/qa-waypoint-throughput-20261108-233001-train/README.md) is the completed pre-waypoint restart parent of this branch. QA-32 remains the preserved historical integrated benchmark reference: its same frozen weights cleared 364/500 distant-exit cases with navigation and 236/500 with navigation disabled. The [architecture index](qa/local-waypoint-v1/README.md) distinguishes these task-specific results, earlier baselines and alternate branches; newest does not imply best on every task.

| Architecture / policy meaning | Inputs -> hidden layers -> outputs | Retained runs |
| --- | --- | --- |
| [Local waypoint control](qa/local-waypoint-v1/README.md) | 134 -> 64 -> 64 -> 2; current rays and an assigned waypoint | QA-18 transfer; QA-19/21-24 exit runs; QA-27-32/39 skill branches; QA-42/43 chains; QA-44 restart; QA-46/47 rooms; QA-48 failed throughput; QA-50 recovery; QA-51 winding and QA-52/53/54/55/56/57 mixed-exit alternatives; snapshots and frozen diagnostics |
| [Current-vision exit movement](qa/local-vision-v1/README.md) | 134 -> 64 -> 64 -> 2; currently visible exit summary | QA-14 through QA-17, plus the QA-17 continuation start copy |
| [Legacy grid movement](qa/grid-movement-v1/README.md) | 37,771 -> 64 -> 64 -> 2; grid observations, movement only | QA-09, QA-10, QA-11 |
| [Legacy grid actions](qa/grid-actions-v1/README.md) | 37,771 -> 64 -> 64 -> 5; grid observations, gameplay actions | QA-06, QA-08 |

Identical tensor dimensions do not imply compatible policy meanings. Keep `local-waypoint-v1` and `local-vision-v1` separate. The legacy grid folder labels identify archived architecture families; they do not add semantic-version metadata to old checkpoints.

## Folder convention

```text
SavedModels/
  README.md
  qa/
    local-waypoint-v1/
      README.md
      qa-waypoint-transfer-start/
      qa-waypoint-reach-transfer-start/
      qa-waypoint-<UTC timestamp>-train/
        final_model.zip
        checkpoints.json
        checkpoints/
        training-config.yaml
        run.json
        training-result.json
        evaluation-result.json
        training-episodes.jsonl
        evaluation-episodes.jsonl
      qa-waypoint-reach-<UTC timestamp>-train/
        final_model.zip
        checkpoints.json
        checkpoints/
        training-config.yaml
        run.json
        training-result.json
        evaluation-result.json
        training-episodes.json
        evaluation-episodes.json
      qa-waypoint-chain-<UTC timestamp>-train/
        final_model.zip
        training-<block>-model.zip
        checkpoints.json
        checkpoints/
        training-config.yaml
        run.json
        training-result.json
        training-episodes.json
        <phase>/
          episodes.jsonl
          legs.jsonl
          chains.jsonl
      qa-waypoint-throughput-<UTC timestamp>-train/
        final_model.zip
        checkpoints.json
        checkpoints/
        training-config.yaml
        run.json
        completion.json
        <phase>/
          episodes.jsonl
          legs.jsonl
          chains.jsonl
      diagnostics/
        <task>-<UTC timestamp>-<mode>/
          README.md
          <run-owned results, records, config and verification evidence>
    local-vision-v1/
    grid-movement-v1/
    grid-actions-v1/
```

- Give each run its own immutable name; retain its starting checkpoint and parent lineage. Do not overwrite a previous run or select a model solely because its timestamp is newest.
- Keep periodic snapshots in that run's `checkpoints/` directory. The maintained exit and waypoint-skill runners create a run directory under the architecture folder and save final weights before frozen evaluation. Config/provenance are retained when training starts; results are added as each phase finishes. Exit runs use JSONL episode records; standalone skill runs use JSON arrays. Chain runs retain a combined training array plus phase-owned JSONL coordinates, legs and outcomes, block-end models and compressed frozen action traces.
- Each checkpoint-owning run has a `checkpoints.json` manifest with relative paths, byte sizes and SHA-256 hashes. These describe model files after relocation; original run-owned configs and JSON provenance remain unchanged historical evidence and may contain their original paths. Frozen diagnostics reference the evaluated checkpoint/hash and retain their own evidence without copying or retraining its weights.
- Runnable configs in [Training/](../Training/README.md) point to current paths. Historical config snapshots beside models describe what ran; use the maintained templates to launch a new session.
- `Logs/` keeps local runtime/session diagnostics and historical rolling snapshots. The current waypoint workflow's periodic weights and essential episode evidence are retained under `SavedModels/`, so its model record does not depend on ignored logs. Historical log files have not been deleted or broadly migrated.
- [Assets/DemoModels/](../Assets/DemoModels) contains deployed ONNX examples and Unity references; those assets stay in place. Saved Python checkpoints are organized here. [QA training history](../Training/changelog/qa-player.md) owns measured results and promotion decisions.

This index and the saved files are local repository content. No commit, push, or external backup is implied by saving a checkpoint.
