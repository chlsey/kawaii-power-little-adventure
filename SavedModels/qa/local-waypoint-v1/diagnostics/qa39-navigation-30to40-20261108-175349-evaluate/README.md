# QA-40: frozen QA-39 navigation to goals 30-40 units away

**Complete: 67/100 goals reached (67%), 33 timeouts, no deaths.** This tests the newly trained QA-39 policy with the observed-map navigator and multiple local waypoint assignments. The goal is an initially unseen exit marker 30-40 units from spawn; the policy still receives only local instructions up to ten units away. No training or optimizer updates, and all sessions stopped.

## Outcomes

| Measure | Result |
| --- | ---: |
| Goals reached | 67/100 |
| Timeouts before discovering goal | 30 |
| Timeouts after discovering goal | 3 |
| Deaths | 0 |
| Successful navigation mean time | 27.87 s |
| Successful median | 24.50 s |
| Successful p90 | 51.30 s |
| Mean episode time including 60-second timeouts | 38.47 s |

Times are game seconds. Goal discovery is the main measured failure stage (30 of 33 failures); this does not isolate exploration selection from local steering, since movement determines what can be sensed. No per-failure causal replay or intervention was run here.

Goal-distance groups are descriptive, not controlled difficulty comparisons:

| Initial distance | Cases | Reached | Timeouts |
| --- | ---: | ---: | ---: |
| 30-35 units | 51 | 31 | 20 |
| 35-40 units | 49 | 36 | 13 |

Actual initial distances span **30.0262-39.9074 units**. Every spawn and goal is at least 1.5 units inside the floor. Every exit begins unseen. These are fresh seeds 287000-287099.

## Navigation and handoffs

The **1,752 observed assignments** stayed within ten units when selected: mean **7.50**, maximum **9.99981**, minimum **0.52**. **1,074 (61.3%)** were 8-10 units away. Shorter points are valid for turns, nearby frontiers and final approach. The cap constrains assignment distance; moving away from an existing instruction can increase its later distance.

Recorded **525 waypoint arrivals and 505 arrival-driven handoffs** across all attempts. A handoff here requires a recorded arrival followed by an active action on a new waypoint version; ordinary replans/replacements are not counted as arrival-driven handoffs. **55/67 successes had at least two such handoffs**; median successful case had four. Some successes had zero recorded arrival-driven handoffs, so goal completion alone is not proof of accurate waypoint following. Mean assignment count was 17.52 and mean replans 3.65 per attempt.

An actual exit trigger marks task completion. Merely reaching an intermediate waypoint does not. The global goal coordinates are retained in diagnostic traces only; the action policy gets the existing 134-input local-waypoint-v1 observation, not a privileged distant-goal vector.

## Scenario and comparison limits

A separate [50 x 50 scene](../../../../../Assets/Scenes/QaWaypoint50Training.README.md) is needed because a 40-unit separation cannot safely fit in every direction inside the old 40 x 40 room with 1.5-unit margins. The new usable width is 47 units. The floor and four enclosing walls are resized; the original 40 x 40 scene is verified unchanged. There are no internal barriers, doors, enemies or generated rooms.

Frozen model is **QA-39**, SHA-256 `a90f3a238b4d6e6b236db357467edafd97b89c5b2dc57bccba50263c72102d2f`, model counter **72,054 steps / 2,800 optimizer updates / 7,500 actor-lineage episodes**. That child was trained from original QA-32 on standalone 1-10-unit targets. This evaluation updates no weights/counters and creates no new policy checkpoint. QA-32 and QA-36 are not the evaluated models.

Same 12-unit vision, ten-unit local cap, 0.5-unit local arrival radius, 60-second / 600-action episode deadline, +50 actual-exit reward, combined +5 positive waypoint cap, -0.001/action and existing stuck/recovery rules. Deterministic inference, CPU, playback 100. Existing Mono hash-set comparer workaround retained. Input semantics, previous movement and rewards are unchanged. Timeout/termination handling remains as previously documented.

**Do not directly compare 67/100 with the earlier 75/100.** The older test used QA-32, a 40 x 40 room, a 30-34-unit goal band and different seeds. This run changes model, geometry and goal range together; no matched QA-32 control was run on this new setup. It establishes performance for this scenario, not the causal benefit of QA-39 training or reliable PCG-dungeon traversal.

Preflight passed 100 placements, eight compass clears, map/barrier/unknown-gap/clearance/recovery/reward checks and **10/10 observation-driven reference clears**. Reference navigation made 284 assignment changes, with minimum seven before discovery. The verifier's reference action limit now uses the actual configured deadline (600 here), replacing an older fixed 300-action cutoff; policy evaluation behavior is unchanged. Reference controls are excluded from the 100 learned-policy cases. No promotion, subsequent training, PCG run or automatic curriculum advance followed.

## Evidence

[Analysis](analysis.json), [per-case discovery and handoff counts](case-analysis.json), [frozen result](evaluation-result.json), [all 100 episode records](episodes.jsonl), [complete action traces, gzip JSONL](exit-decisions.jsonl.gz), [run-owned config](training-config.yaml), [source identities](run.json), [source scene snapshot](source/Assets/Scenes/QaWaypoint50Training.unity), [preflight](reference-verification.json), [readiness](policy-contract.json), [integrity checks](verification.json).

[Maintained recipe](../../../../../Training/qa-player-waypoint-qa39-navigation-30to40.yaml), [QA-39 training provenance](../../qa-waypoint-reach-20261108-145257-train/README.md), [training history](../../../../../Training/changelog/qa-player.md).

Local-only runtime: `Logs/qa39-navigation-30to40/20261108-175349-evaluate/`; preflight: `Logs/qa39-navigation-30to40/20261108-175159-verify/`. No process remains active.
