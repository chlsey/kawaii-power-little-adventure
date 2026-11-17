# QA-61: QA-50 exploration waypoints and exit discovery

Status: complete, reconciled 2026-11-15 06:42 UTC. Exactly 100 frozen attempts, seeds 510000-510099. QA-50 remains selected; no new training, deployment, commit or push.

## Conditions

50 x 50 empty room, exit initially unseen at 30-40 units, twelve-unit vision, sixty-second / 600-action limit. Normal sensor-map frontier selector, observed-space route planner and recovery; local waypoints capped at ten units with 0.5-unit arrival radius. The policy/navigator receives no true hidden exit destination. True exit position is diagnostic evidence only. No internal walls, corridors, enemies or puzzles.

Original frozen deterministic QA-50, SHA-256 `acffd0594bb6a03a157e2853688dfc4d7647606d052cb241fe620d9f8eb4db64`, 656,793 model steps / 25,610 optimizer epochs / 4,700 lineage episodes. Zero weight updates or training steps. [Run config](training-config.yaml), [metadata](run.json), [frozen evaluation](evaluation-result.json).

## Results

| Measure | Result |
| --- | ---: |
| Exploration waypoint arrivals / issued assignments | 874/1,064 (82.14%) |
| Replaced without arrival | 141 |
| Superseded by exit goal | 37 |
| Episode ended before arrival | 12 |
| Mean time for successful exploration assignments | 1.221 seconds |
| Attempts receiving exploration assignments | 100/100 |
| Attempts discovering exit in sensor memory | 51/100 |
| Attempts reaching exit | 45/100 |
| Never discovered exit | 49/100 |
| Discovered exit but timed out | 6/100 |
| Completion conditional on discovery | 45/51 (88.24%) |
| Mean first-discovery time, discovery cases only | 17.337 seconds |
| Replans / abandoned goals | 0 / 101 |

Forty-five successes / 55 timeouts / zero deaths, 41,643 trainer requests. Mean successful exit time 19.207 seconds. There is no learning in these requests.

## Meaning and limits

A frontier is known free space next to unobserved space. The navigator plans through observed free cells and assigns local waypoints toward the selected frontier; a local waypoint need not itself be the frontier goal. A reachable observed exit takes priority. Arrival does not establish frontier completion or level completion.

The arrival denominator counts distinct exploration waypoint versions actually issued to the policy, including replacements and interruptions. A waypoint replaced after exit discovery is not automatically an agent failure. The 141 other unreached replacements also require inspecting route validity and goal changes before attributing failure. Exit discovery uses the final sensor-map evidence; visibility traces provide first-discovery timing.

Most timeouts occurred before exit discovery (49/55), even though many local waypoints were reached. This identifies a weakness in combined exploration and movement; it does not establish whether frontier selection, waypoint validity, goal abandonment or learned steering is responsible. No matched reference-steering run was performed. These 100 empty-room cases do not establish corridor or generated-level generalization.

## Evidence and checks

[Summary](exploration-result.json), [per-episode records](exploration-episodes.json), [per-assignment outcomes](exploration-assignments.json), [original episode evidence](episodes.jsonl), [compressed action traces](exit-decisions.jsonl.gz), [analysis source](evaluate_exploration_waypoints.py), [final verification](final-verification.json). Exact seeds, episode/assignment counts, unchanged checkpoint and source hashes, and restored project settings verified; Python and Unity stopped.

Initial launch failed before completed episodes because the recorder tried reading a sensor map before first reset. Guard record capture until the grid exists; navigation and model behavior remain unchanged. Preserve local failure evidence at `Logs/qa50-exploration-waypoints/20261115-045859-evaluate/`. Completed retry is `Logs/qa50-exploration-waypoints/20261115-045955-evaluate/`; all original 100 seeds evaluated once.

## Stuck behavior follow-up

48/100 attempts recorded at least one stuck event; all 48 were among 55 timeouts (87.27%). None of the 45 successful attempts recorded one. The controller counted 722 low-displacement windows: less than 0.05 units net displacement over each nonoverlapping twenty-action / two-second window. These are repeated detection windows, not 722 independent incidents.

Per-action traces show 16,105/41,643 actions (38.67%) moving less than 0.02 horizontal units. The longest consecutive requested low-motion stretch lasted 529 actions (52.9 simulated seconds); small movements can still accumulate during such a stretch, so this is not a claim of absolute immobility. The environment's sustained requested-low-motion counter was active on 13,327 actions (32.00% of all actions), across 47 cases.

44 of the 49 never-discovered-exit attempts recorded stuck events. No waypoint was supplied on 28,407/41,643 actions (68.22%); this is a separate navigation diagnostic, not proof of the cause of stuck behavior. [Exact counts and detector definitions](stuck-analysis.json). No weights, environment or navigation behavior changed for this analysis.

## Why waypoint assignment disappears

Read-only analysis explains all 28,407 missing-waypoint actions and all 89 transitions into that state across 70 attempts. Every such state is `no_reachable_goal`. Each state has a recorded cardinal wall ray proving that the observed-wall inflation rule removes the agent's current grid cell. The physical position at assignment loss is 0.317-0.498 units from a wall. Fifteen affected attempts eventually recover and complete; all 55 timeouts end without a waypoint.

The scene uses 0.25-unit map cells, actor radius 0.30, skin width 0.03 and navigator safety margin 0.05: planning clearance 0.38. A cell center 0.375 units from the wall is removed in full. This excludes positions in the half-unit strip beside the wall, even though the actual controller can occupy some of that strip. For example, seed 510001 loses its waypoint at step 295 with x=-24.511637, 0.488363 units from the wall, while the previous waypoint is still 3.743 units away. Its current x-cell -99 is excluded by observed wall x-cell -101.

Failure chain: the agent drifts into the conservative wall margin -> its current map cell is excluded -> `QaObservedRoutePlanner.Reachable` immediately returns an empty set because its starting cell is not free -> goal selection finds no reachable frontier/exit -> route attempts fail and the goal is abandoned -> `QaNavigationCoordinator` reports `no_reachable_goal` and supplies no waypoint. `SegmentKnown` also rejects segments beginning in this excluded cell. The missing-waypoint state persists until movement happens to leave the excluded strip. The policy keeps running with zero waypoint features; its recorded low-motion actions all occur during this state.

This is an unhandled navigation recovery state, triggered by near-wall movement. It does not establish that the whole room was explored or that no physical escape exists. The existing stuck-replan path is inside the `HasWaypoint` branch, so it cannot recover a state where no waypoint is assigned.

A prospective fix should establish a collision-safe escape from the current physical position to a nearby valid cell, then resume ordinary routing. The clearance policy and recovery logic must be validated together; simply disabling wall clearance or training for more episodes does not demonstrate a safe repair. No production navigation behavior was changed for this investigation. Exact per-transition wall evidence and method limits are in [the analysis](navigation-loss-analysis.json).
