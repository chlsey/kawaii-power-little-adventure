# QA-38: 100 frozen tests with a 10-unit navigator waypoint maximum

**Complete: 75/100 actual-exit clears (75%), 25 timeouts, no deaths.** QA-32 with the historical six-unit cap cleared 74/100 on the exact same placements. The one-point increase does not establish improved completion, and mean attempt time increased from 29.97 to 32.75 game seconds. All sessions stopped; no weights or optimizer updates.

## Matched results

| Metric | Maximum 6 units | Maximum 10 units |
| --- | ---: | ---: |
| Actual-exit clears | 74/100 | 75/100 |
| Timeouts | 26 | 25 |
| Mean episode time including timeouts | 29.97 s | 32.75 s |
| Mean successful clear time | 19.41 s | 23.67 s |
| Mean assignments per attempt | 14.79 | 14.86 |
| Mean replans per attempt | 3.73 | 3.26 |

Paired cases: **69 both clear, six 10-unit-only clears, five 6-unit-only clears, 20 both timeout**. Completion change +1 percentage point; exploratory paired case-bootstrap 95% interval **-5 to +8 points**, exact paired p=1.0. Bootstrap 20,000 resamples, seed 38. This is a reused-case, historical matched comparison with one frozen policy, not independent-training variability or a fresh parallel baseline run. Successful-only times concern different subsets; capped all-episode time captures failures as 60 seconds.

The new 25 failures comprise 17 that never discovered the exit and eight that discovered it without completing. These episode-level records do not establish individual failure causes.

## Distances actually assigned

The cap is **10 units when assigning a waypoint**, not a requirement that every assignment be 8-10 units away. The coordinator still chooses the furthest known-free, currently visible, actor-clear route point within the cap; short turns, nearby frontiers and final approach remain valid.

All **1,486 observed assignments** respected the cap:

- **956/1,486 (64.3%)** were in the 8-10-unit band.
- **1,063/1,486 (71.5%)** exceeded six units.
- Mean initial assignment distance **7.67 units**, median **9.77 units**, observed maximum **9.99990 units**, minimum **0.57 units**.
- Recorded 387 waypoint arrivals across the 100 attempts. Assignment replacement counts are not arrival counts.

Assignment distances are measured on the first active action for each seed/version. A bot can subsequently move farther away from a retained instruction; the cap constrains selection, not every later actor-to-waypoint distance.

## Change and unchanged conditions

`QaNavigationCoordinator.DefaultMaxWaypointDistance` is now 10 units; its maximum is configurable. The batch runner explicitly writes/records/applies `max_waypoint_distance`, and readiness/verification checks the actual value. Legacy batch recipes omitting the field retain six units for reproducibility. This experiment's [maintained config](../../../../../Training/qa-player-waypoint-max10-evaluate.yaml) explicitly selects 10. Current game-system and scene guides reflect this distinction.

Frozen model is **QA-32**, SHA-256 `0ca5d55955bf6ea7489e2495c1a1cbd9c3be773154981483096dd17d4b4479e0`, 63,246 training steps / 7,000 actor-lineage episodes. QA-36 was not used. Same 134-input/two-output local-waypoint-v1 contract, prior movement features, ray sensing, visibility/clearance tests, routing and recovery rules, 0.5-unit arrival radius, +50 actual-exit reward / combined +5 positive waypoint cap, -0.001/action, empty 40 x 40 room, unseen exit 30-34 units away, 60-second deadline and deterministic inference. Seeds 285000-285099, exact baseline spawn/exit coordinates checked. No training, no new model, no automatic stage advancement.

This uses the existing **exit-navigation benchmark** to isolate the waypoint-cap experiment against old cases. It is not an arbitrary-target drill or a direct 15-30-unit waypoint test. The existing Mono comparer workaround and read-only telemetry remain in place; the old baseline is historical source/evidence, not rerun as another 100-case arm here.

Preflight passed all 100 placements, eight compass clears, map/barrier/unknown-gap/clearance/recovery/reward checks and **10/10 reference exit clears with 142 handoffs**, minimum four before exit discovery. The handoff bound now uses the configured cap and exit fixture footprint instead of a hardcoded six-unit assumption. Reference attempts are excluded from the 100 policy tests. Actual normalized distance ceiling for new assignments is 10/12, while the observation encoding and 12-unit vision remain unchanged.

## Evidence

[Analysis](analysis.json), [frozen result](evaluation-result.json), [100 episode records](episodes.jsonl), [100 baseline records](baseline-episodes.jsonl), [action traces, gzip JSONL](exit-decisions.jsonl.gz), [run-owned config](training-config.yaml), [runtime/source hashes](run.json), [source snapshot](source/Assets/PCGDungeon/Scripts/QA/QaNavigationCoordinator.cs), [reference preflight](reference-verification.json), [policy readiness](policy-contract.json), [integrity checks](verification.json).

Local-only runtime: `Logs/qa-waypoint-max10/20261108-143217-evaluate/`; preflight: `Logs/qa-waypoint-max10/20261108-143024-verify/`. The original QA-32 and its separate backup remain unchanged. No fix to prior looping behaviour is established by this result.
