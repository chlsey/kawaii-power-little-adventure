# Saved checkpoints

The current QA workflow is [local waypoint control under a deterministic navigator](qa/local-waypoint-v1/README.md). **QA-32** is the latest trained skill checkpoint: 99/100 frozen assigned-waypoint arrivals at 8-12 units. The same frozen weights cleared 364/500 distant-exit cases with navigation and 236/500 with navigation disabled. The [architecture index](qa/local-waypoint-v1/README.md) distinguishes these task-specific results, earlier baselines and alternate branches; newest does not imply best on every task.

| Architecture / policy meaning | Inputs -> hidden layers -> outputs | Retained runs |
| --- | --- | --- |
| [Local waypoint control](qa/local-waypoint-v1/README.md) | 134 -> 64 -> 64 -> 2; current rays and an assigned waypoint | QA-18 transfer; QA-19/21-24 exit runs; QA-27-32 skill branches; snapshots; QA-25/26/33/34 frozen diagnostics |
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
      diagnostics/
        <task>-<UTC timestamp>-<mode>/
          README.md
          <run-owned results, records, config and verification evidence>
    local-vision-v1/
    grid-movement-v1/
    grid-actions-v1/
```

- Give each run its own immutable name; retain its starting checkpoint and parent lineage. Do not overwrite a previous run or select a model solely because its timestamp is newest.
- Keep periodic snapshots in that run's `checkpoints/` directory. The maintained exit and waypoint-skill runners create a run directory under the architecture folder and save final weights before frozen evaluation. Config/provenance are retained when training starts; results are added as each phase finishes. Exit runs use JSONL episode records; skill runs use JSON arrays.
- Each checkpoint-owning run has a `checkpoints.json` manifest with relative paths, byte sizes and SHA-256 hashes. These describe model files after relocation; original run-owned configs and JSON provenance remain unchanged historical evidence and may contain their original paths. Frozen diagnostics reference the evaluated checkpoint/hash and retain their own evidence without copying or retraining its weights.
- Runnable configs in [Training/](../Training/README.md) point to current paths. Historical config snapshots beside models describe what ran; use the maintained templates to launch a new session.
- `Logs/` keeps local runtime/session diagnostics and historical rolling snapshots. The current waypoint workflow's periodic weights and essential episode evidence are retained under `SavedModels/`, so its model record does not depend on ignored logs. Historical log files have not been deleted or broadly migrated.
- [Assets/DemoModels/](../Assets/DemoModels) contains deployed ONNX examples and Unity references; those assets stay in place. Saved Python checkpoints are organized here. [QA training history](../Training/changelog/qa-player.md) owns measured results and promotion decisions.

This index and the saved files are local repository content. No commit, push, or external backup is implied by saving a checkpoint.
