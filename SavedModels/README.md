# Saved checkpoints

Latest full evaluation: [QA-70](qa/local-waypoint-v1/diagnostics/qa70-two-room-20261116-183302/README.md), 100 matched two-room tests of the retained QA-68 navigator with frozen QA-50. Exits **49 -> 44/100**, discoveries **51 -> 47/100**, normalized assignments **24.83 -> 23.14**, and missing guidance **44.73% -> 49.61%**. Five exits gained / ten lost; lower churn does not establish better completion. All 57 direct checks and model/source invariance checks pass. No learning or model deployment.

Latest churn follow-up: [QA-68](qa/local-waypoint-v1/diagnostics/qa68-churn-20261116-170047/README.md) retains physically clear active instructions. On twelve selected matched cases, exits **6 -> 8/12** and interruptions **508 -> 425**, but normalized assignments remain **26.96 -> 27.30 per 100 guided actions**. [QA-69](qa/local-waypoint-v1/diagnostics/qa69-churn-20261116-170939/README.md) lowers the rate to 18.75 while regressing exits to 4/12; its route-clearance changes were rejected and rolled back. Current source is QA-68, QA-50 weights remain frozen, and the subsequent full 100-case result is recorded above.

Previous full 100-case frozen diagnostic: [QA-67](qa/local-waypoint-v1/diagnostics/qa67-navigation-20261116-100446-evaluate/README.md), shared world capsule, physical observed-cell refinement, live blocked maps and failed-goal caching. On 100 matched generated dungeons, exits **0 -> 49/100**, discoveries **5 -> 51/100**, missing guidance **82.08% -> 44.73%**. Normalized assignments increase **15.14 -> 24.83 per 100 guided actions**, and observed throughput falls **24.48 -> 7.77 actions/second**; recovery churn and performance remain open. Reference steering clears 42/51 policy failures. Zero learning; QA-50 stays selected. [QA-65](qa/local-waypoint-v1/diagnostics/qa65-churn-20261116-062333/README.md) is the historical empty-room result under its earlier source; [QA-62](qa/local-waypoint-v1/qa-waypoint-20261115-090609-train/README.md) remains stopped.

**Selected baseline for future QA work: [QA-50](qa/local-waypoint-v1/qa-waypoint-recovery-20261109-224625-train/README.md), by user choice.** Highest recorded original two-room count: 57/100; mixed reference 117/150. Full checkpoint verified and preserved; 4,700 lineage episodes, stored LR 0.00002. Selection does not start training or change a deployed model. See `Training/qa-player-selected-checkpoint.json` at the game root; future runners still require an explicit run config. QA-59/60 remain historical alternatives.

Prior corridor experiment: [QA-59](qa/local-waypoint-v1/qa-two-room-20261114-215855-train/README.md), exactly 1,000 corridor-focused two-room episodes (all training exits). Frozen two-room exits 38 -> 46/100, an uncertain observed gain; empty waypoint mean 45.59 -> 45.66. Final lineage 16,700 episodes. Mixed retention untested, prior alternatives preserved, no deployment.

Preceding completed experiment: [QA-58](qa/local-waypoint-v1/qa-mixed-exit-20261114-072247-train/README.md), another 1,000 episodes (997 training exits). Frozen mixed completion stays 120/150; original two-room declines 45 -> 38/100; empty waypoint mean stays 45.58 -> 45.59. All 36 archives verified, settings restored, processes stopped. Final lineage 15,700 episodes (11,000 mixed). Prior alternatives retained; no deployment.

The current QA workflow is [local waypoint control under a deterministic navigator](qa/local-waypoint-v1/README.md). **[QA-57](qa/local-waypoint-v1/qa-mixed-exit-20261111-084731-train/README.md)** is the preceding completed experiment: another 3,000 unique completed episodes from QA-56 at unchanged LR 0.000002, with 2,993 exits / seven timeouts. Frozen mixed exits recover **101 -> 120/150**, while original two-room exits regress **56 -> 45/100**; both paired changes are supported on reused cases. Empty waypoint mean changes 45.49 -> 45.58. QA-55 remains slightly ahead on both counts (121/150 and 50/100). Unity-disconnect recovery and lost progress are disclosed; no completed seeds were repeated. All 106 archives verified and prior models preserved. At QA-57 this lineage had 10,000 mixed-task / 14,700 completed episodes; QA-51 is an excluded alternate fork. Geometry overlap disclosed. No additional learning or deployment.

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
