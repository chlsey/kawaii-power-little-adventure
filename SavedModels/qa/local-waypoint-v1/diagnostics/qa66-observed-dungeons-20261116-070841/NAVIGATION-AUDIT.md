# Navigator changes and completed dungeon-policy audit

The 100-case policy batch is complete. Matched references stopped at 77/100 completed, zero exits and one exit discovery, before the user-requested implementation. All 77 end with `no_reachable_goal`. These are partial controls, not a completed 100-case comparison. This audit makes no navigation, model or training changes.

## What changed

1. **Recover from an excluded starting cell.** The original observed-map planner could return no route when wall inflation removed the actor's current cell. Add a bounded observed escape with physical sweep and normal destination-clearance checks.
2. **Retain valid instructions and avoid unnecessary planning.** Hold a valid waypoint; consume reached route prefixes; reuse planner geometry until navigation knowledge, grid identity or clearance changes. Preserve a valid exploration goal while unrelated free cells are discovered; reconsider when missing/reached/invalid, exit discovery/connectivity requires it, or recovery abandons the goal.
3. **Preserve intention through recovery.** Keep goal, useful route and progress/replan state. Prefer escape destinations with extra planning room and progress toward that goal. Resume only after reaching adequate clearance, with a tighter normally clear fallback for narrow spaces.
4. **Bound repeated recovery failures.** Keep 32 position/target samples across handoffs, detect repeated returns, avoid the last failed escape, and escalate stalled recovery. Retain a separate goal-progress watchdog across escapes so longer cycles cannot keep resetting their progress budget. Abandon a goal through the existing rules after repeated failed recoveries.

The navigator remains deterministic. QA-50's movement weights, policy inputs/actions, movement speed, ordinary wall-inflation formula, quarter-unit grid and normal capsule-clearance calculation were not changed by these fixes. The extra-clearance preference applies to recovery destinations. The fresh dungeon run uses the game's actual scaled player and generated geometry; the evaluation adapter supplies no privileged map or exit position to the observed navigator.

## What improved in the controlled empty-room comparison

Same 100 seeds/placements and frozen QA-50, QA-64 versus QA-65:

| Metric | Before final recovery fixes | After |
| --- | --- | --- |
| Exit completion | 69/100 | 100/100 |
| Assignments | 18,026 | 3,104 |
| Exact two-action return cycles | 5,293 | 0 |
| Broader recovery-related returning states | 11,847 | 118 |
| Missing-waypoint actions | 0 | 0 |

Earlier matched empty-room stages improved exits 45 -> 64 after the initial escape, then 64 -> 69 after instruction/route retention, then 69 -> 100 after retained recovery/progress changes. These diagnosed/reused cases demonstrate the changes together; they do not establish generic-level performance or independent effects of each change. [QA-65 evidence](../qa65-churn-20261116-062333/README.md).

## Fresh generated dungeons: complete policy results

100 fresh seeds, two generated 20 x 20 rooms with real corridors, 25 layouts per quarter-turn orientation. This is a different task, so it is not a before/after regression measurement of the recovery fixes.

| Measurement | Result |
| --- | --- |
| Exits completed / discovered | 0/100 / 5/100 |
| Actions | 60,000 |
| Actions with / without a waypoint | 10,752 / 49,248 |
| Missing-waypoint fraction | 82.08% |
| Assignment versions, all accounted for | 1,628 |
| Normal / recovery arrival events | 585 / 625 |
| Instructions interrupted without a navigation-arrival event | 418 (25.68%) |
| Median instruction lifetime | 0.3 simulated seconds |
| Median / maximum assignments per case | 16 / 32 |
| Exact two-action return cycles | 0 |
| Broader recovery-related returning states | 60 in 15 cases |
| Low-displacement actions, under 0.02 units | 32,593 (54.32%) |
| QA stuck-detector windows | 1,400 in 99 cases |
| Final phase | 75 corridor / 25 spawn room |
| Entered exit room / ended below floor | 0 / 0 |
| Final navigation status | `exploration_blocked` in all 100 |

Arrival events are coordinator state transitions, not merely a geometric radius crossing. Of the 418 interrupted instructions, 347 end when the starting cell becomes excluded, 27 when a goal cell becomes invalid, 17 on an invalid escape, thirteen on an invalid route segment, ten on detected oscillation and four on retained-goal progress failure. All 1,628 versions have exactly one end classification. No unaccounted replacement is hidden by the analysis.

The missing-guidance time matches **46,585 `exploration_blocked` events plus 2,663 `no_reachable_goal` events**. Lower assignment counts here accompany absent instructions. They are not evidence of stable successful navigation. Exact short loops are absent and the broader recurrence count is low, but those metrics do not capture standing or drifting without a target.

Only 32/10,752 guided actions (0.30%) point away from or perpendicular to the waypoint. A positive dot product does not prove precise steering or passage clearance, but this result supports investigating the loss of guidance first.

## Current diagnosis and open checks

The four initial matched reference cases also fail with `no_reachable_goal`. They reach all thirty assigned waypoints, with no escape assignment, oscillation, stall-recovery or recovery-exhaustion event; they then produce 2,050 no-goal events. Those failures therefore occur without invoking the new escape/recovery behavior. This narrows the investigation to ordinary planning connectivity and availability of the next goal, while not constituting a full before/after dungeon test of every navigator change.

An approximate reconstruction from their recorded rays finds no eligible frontier at current nominal clearance; removing map inflation exposes 26/34/44/35. Scaling radius alone exposes frontiers in just one of the four. This supports a clearance/voxel-connectivity problem at narrow passages; float reconstruction is not an authoritative runtime grid dump and zero inflation is not a tested fix. Recorded controller radius is 0.3, transform X/Z scale approximately 0.6 and skin width 0.08; current planning uses radius + skin + 0.05 directly.

One performance gap also remains: when selection finds no reachable goal, `!hasGoal` triggers the full reachable-cell search again each action, even if map knowledge and position have not changed. Geometry caching does not cache that failed selection. This explains part of the reference replay's cost; it has not been optimized during this experiment.

The next correctness investigation is narrow-passage representation and consistency between the planner's inflated grid and the game's actual capsule checks. That is separate from the recovery-state fixes already measured. The partial controls do not classify every failure. Further implementation and validation belong to QA-67.

[Policy audit data](navigation-audit.json), [each instruction](navigation-assignment-audit.json), [audit script](analyze-navigation-audit.py), [exact-cycle result](two-action-loops.json), [clearance probe summary](clearance-probe-summary.json), [campaign status](campaign.json). Policy weights remain frozen and training remains stopped.
