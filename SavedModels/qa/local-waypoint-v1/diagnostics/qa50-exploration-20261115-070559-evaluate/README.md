# QA-62 frozen QA-50 with navigator exploration escape

Status: completed frozen baseline, before any new learning. Same original 100 seeds 510000-510099 and QA-50 weights as QA-61, same 50 x 50 empty room, initially unseen exit 30-40 units away, twelve-unit vision, sixty-second limit. Only navigator recovery/clearance handling changed; no optimizer updates or training steps.

| Metric | Original navigator | Exploration-escape navigator |
| --- | ---: | ---: |
| Exit discovered | 51/100 | 66/100 |
| Exit completed | 45/100 | 64/100 |
| Never discovered | 49/100 | 34/100 |
| Discovered but not completed | 6/100 | 2/100 |
| Actions without a waypoint | 28,407/41,643 | 0 |
| Issued exploration assignments | 1,064 | 19,553 |
| Exploration assignments reached | 874 (82.14%) | 10,146 (51.89%) |
| Replaced without arrival | 141 | 9,328 |

The missing-waypoint failure is absent and end-to-end completion improved. Assignment churn is substantially higher: the new short escape/handoff behavior changes assignment lifetimes, so assignment arrival percentage should not be treated as a direct measure of worsening motor skill. Preserve this tradeoff for subsequent training/evaluation analysis. No corridor, obstacle, combat or puzzle validation is established by this open-room comparison.

Existing verification passed 100 seeded resets, eight compass completions, map/barrier/unknown-gap/recovery/reward checks and ten of ten reference navigation completions. These reused cases isolate the navigator change at fixed model weights, but are not unseen-geometry generalization evidence.

QA-50 checkpoint SHA-256 `acffd0594bb6a03a157e2853688dfc4d7647606d052cb241fe620d9f8eb4db64` remains unchanged. [Summary](exploration-result.json), [frozen evaluation](evaluation-result.json), [config](training-config.yaml), [per-assignment outcomes](exploration-assignments.json), [per-episode outcomes](exploration-episodes.json). Training budget: exactly 1,000 additional episodes from QA-50, separate from these 100 frozen attempts. The first training launch timed out before learning; recovery is recorded in the QA history.
