# QA-66 observed-dungeon baseline

Complete frozen QA-50 policy evaluation, seeds 530000-530099, real generated two-room geometry, 25 cases per quarter-turn, observed-map navigator only. Zero exits, five exit discoveries, 60,000 actions, 49,248 missing-waypoint actions (82.08%). No policy updates.

Matched direction-following references: 77/100 complete, zero exits and one exit discovery, all ending with `no_reachable_goal`. Remaining replays stopped when the user requested implementing and testing navigation fixes. The interrupted attempt is excluded from metrics; raw partial traces are retained separately in `failure-reference`. The four initial controls are in `reference-preflight`. See [partial control result](partial-control-result.json).

Source snapshots and checkpoint file hashes verified at stop, settings restored, owned processes ended. Final in-memory weight equality assertions were not reached because the campaign was superseded. The frozen driver has no learning calls. Do not describe the partial controls as 100 completed cases.

[Navigator audit](NAVIGATION-AUDIT.md), [campaign](campaign.json), [complete policy traces](evaluation/episodes.jsonl). No training or model promotion. QA-67 owns subsequent changes and matched testing.
