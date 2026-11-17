# QA-64: 100 frozen cases with retained waypoint planning

Completed 2026-11-16 UTC. Original QA-50 weights stayed fixed; no training, promotion or deployment. The new planner completes **69/100 cases versus 64/100 previously**, with five gained cases and none lost. Waypoint assignments decrease **19,790 -> 18,026 (8.91%)**, but repeated wall-recovery loops remain the principal diagnosed churn mechanism.

## Matched comparison

Same seeds 510000-510099 and exact spawn/exit coordinates as the [pre-retention baseline](../qa50-exploration-20261115-070559-evaluate/README.md). Normal observed-map frontier navigation in a 50 x 50 empty room; initially unseen exit 30-40 units away, twelve-unit vision, ten-unit waypoint cap and sixty-second deadline. Original QA-50 SHA-256 `acffd0594bb6a03a157e2853688dfc4d7647606d052cb241fe620d9f8eb4db64`, 656,793 model steps / 25,610 optimizer epochs / 4,700 lineage episodes. Policy, rewards, sensor/observation/action meanings and clearance checks unchanged. The only additions since QA-63's ten-case check are optional read-only navigation event tracing and its analysis. The first ten cases exactly reproduce QA-63's actions, assignments and outcomes.

| Measurement | Before retention | New planner |
| --- | --- | --- |
| Exits discovered | 66/100 | 71/100 |
| Exits completed | 64/100 | 69/100 |
| Actions | 35,431 | 34,505 |
| All waypoint assignments | 19,790 | 18,026 |
| Assignments per 100 actions | 55.86 | 52.24 |
| Action-phase wall time | 5,183.48 s | 2,749.96 s |
| Actions per wall-clock second | 6.84 | 12.55 |

Five gained seeds: 510015, 510020, 510041, 510047, 510089; none lost. Twenty-nine timeouts never discover the exit; two discover it without completing. These are reused empty-room cases, not unseen-dungeon, corridor, combat or puzzle evidence. The five-case gain is descriptive, not a claim of general reliability.

Timing runs from each Unity-ready file to its hundredth episode record, excluding startup and preflight verification. Observed full-run throughput is **1.84 times the earlier run**. Sequential local end-to-end timing includes instrumentation and machine conditions; it is not an isolated planner benchmark or production-readiness proof. This full comparison supersedes extrapolating the earlier ten-case 3.33-times timing. The baseline assignment total counts every episode's assignment version; the earlier 19,553 exploration-leg count excludes other assignments and is a different measure.

## Where assignments change

Every assignment version is accounted for: **9,459 normal route instructions and 8,567 escape instructions**. Median instruction lifetime is **0.1 simulated seconds**; 15,887/18,026 last at most one action (within floating-point tolerance).

| End of instruction | Count |
| --- | --- |
| Normal route: current cell enters inflated wall margin | 7,977 |
| Escape: arrives within tolerance in a free cell | 7,960 |
| Escape: arrives within tolerance but current cell remains blocked | 590 |
| Normal route: waypoint reached | 1,139 |
| Goal cell becomes invalid | 205 |
| Newly selected exit supersedes instruction | 51 |
| Known route segment becomes invalid | 4 |
| Episode completes / times out | 69 / 31 |

**84.33% of normal route instructions end because the current cell becomes blocked.** There are no unaccounted replacements, observed clearance-check failures or escape-invalid events. A recovery handoff or arrival is not automatically a bug; exact recurrence analysis below identifies the clearest wasted movement.

Planner counters: **22,753 geometry builds, 9,324 goal selections, 9,328 route searches** over 34,505 actions. New map discovery still frequently rebuilds geometry. Goal and route selection are event-triggered, but escape recovery clears the current goal/route, so repeated recovery can still trigger repeated selection. No action lacks a waypoint. Navigator stall replans remain zero; the QA player's separate stuck detector records 535 windows in 26 cases (windows are not distinct incidents). All actions move at least 0.05 units, so single-action low-displacement counts alone miss the back-and-forth behavior.

## Churn localization

Follow-up analysis of the same saved traces; no additional episodes or behavior changes. **16,527/18,026 instruction endings (91.68%)** are blocked-start replacements or escape arrivals (free or still blocked). This category includes legitimate recovery handoffs; exact loops below identify confirmed wasted movement. The 31 timeout cases produce **14,376/18,026 assignments (79.75%)**; the 69 successful cases produce 3,650. **7,868/7,977 blocked-start replacements (98.63%) occur during exploration**, versus 109 while pursuing an exit.

Blocked-start replacements are concentrated at the outer perimeter (maximum absolute actor X/Z is 24.5002-24.6856 in this 50-unit room). Assign the nearest outer wall using world X/Z, independent of actor facing:

| Nearest outer wall | Replacements | Share | Cases touching this margin |
| --- | --- | --- | --- |
| East (+X) | 6,361 | 79.74% | 35 |
| West (-X) | 863 | 10.82% | 37 |
| South (-Z) | 742 | 9.30% | 20 |
| North (+Z) | 11 | 0.14% | 2 |

Cases may touch multiple walls. East-wall contact is not more widespread than west-wall contact in this sample, but produces many more repeated replacements. Repeated events are not independent trials of directional reliability. Of 6,361 east-margin entries, **1,809 have an inward-pointing target X component but an outward movement command**; another 4,552 have both target and command pointing outward. In 5,161 events, the target's X coordinate is less than 0.1 units from the actor's pre-action X coordinate. These traces implicate wall-adjacent steering as well as the recovery handoff; they do not isolate a universal policy or planner direction bias. [Localization counts](churn-localization.json).

## Confirmed return loops

A loop is a normal route assignment, an escape assignment, then the same route target and actor X/Z position within 0.001 units exactly two actions later. **5,293 such cycles occur in 30 cases, including 29/31 timeouts and one successful case.** Their 10,586 actions consume **30.68% of all actions**. In 23 cases these exact cycles consume at least half the episode's actions. This is a conservative exact-return measure; it does not count other wandering or approximate oscillation.

Seed 510009 is the clearest example: **594 assignments / 600 actions**, 294 consecutive two-action cycles (58.8 simulated seconds). The actor alternates between X/Z positions `(24.31897, -18.03392)` and `(24.60182, -17.75108)`. The normal target is `(24.375, -11.625)`; the escape target is `(24.125, -18.125)`. Recorded movement commands alternate `(1, 1)` and `(-1, -1)`, cancelling each other.

The trace establishes this sequence: a valid normal instruction leads to learned movement into the inflated wall margin; recovery assigns a short escape; after returning to a free cell, normal navigation resumes and the same movement repeats. `AssignExplorationEscape` clears goal/route/replan state and resets stall tracking. That interaction lets the cycle persist despite retaining valid normal waypoints. Breaking this recovery oscillation remains a code/behavior task; no fix was applied during this measurement run.

## Verification and retained evidence

Existing fixtures passed: 100 seeded resets, eight compass completions, map-barrier/unknown-gap/recovery/reward checks, and ten reference completions with ten exit discoveries and 248 handoffs. Evaluation records zero weight updates and zero new training steps. Parent/source hashes and restored settings verified; Python and Unity exited. QA-50 remains selected; QA-62 remains stopped.

[Summary](churn-result.json), [per-case results](churn-episodes.json), [every assignment](churn-assignments.json), [exact return loops and examples](two-action-loops.json), [episode records](episodes.jsonl), [navigation event trace](navigation-events.jsonl.gz), [action trace](exit-decisions.jsonl.gz), [evaluation accounting](evaluation-result.json), [verification](verification.json), [campaign](campaign.json), [source hashes](run.json), [config](training-config.yaml), [source snapshot](source/), [runner/analysis](evaluate_waypoint_churn.py), [return-loop analysis](analyze-two-action-loops.py).

Local runtime: `Logs/qa50-exploration-waypoints/20261116-035058-evaluate/`; preflight: `Logs/qa50-exploration-waypoints/20261116-035024-verify/`. No further training, curriculum advancement, commit or push.
