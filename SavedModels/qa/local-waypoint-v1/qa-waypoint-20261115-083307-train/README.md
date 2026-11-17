# QA-62 initial training launch: preflight timeout

Status: preflight failed, zero completed training episodes and no saved learned checkpoint. Unity did not finish the placement/reference verification within the original 300-second readiness limit. Preserve this launch and failure as part of the QA-62 campaign; do not count it as a separate training budget.

The frozen navigator comparison had already passed: 66/100 exit discoveries, 64/100 exits, zero missing-waypoint actions. Retry the unchanged 1,000-episode budget from original QA-50, seeds 520000-520999, with a run-owned 1,800-second startup timeout. Socket timeouts, environment, rewards, policy and learning settings remain unchanged. [Failure](failure.json), [original config](training-config.yaml), [metadata](run.json). Campaign/recovery source remains local under `Logs/qa-navigation-recovery-campaign/20261115-065621/`.
