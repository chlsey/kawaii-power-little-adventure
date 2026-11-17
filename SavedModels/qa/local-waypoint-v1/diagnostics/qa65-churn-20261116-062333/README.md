# QA-65: preserve navigation progress through recovery

Completed 2026-11-16 UTC: **100/100 exits versus QA-64's 69/100**, with original QA-50 weights frozen. All 31 former timeouts complete; no previously successful case is lost. Waypoint assignments decrease **18,026 -> 3,104 (82.78%)**. Exact two-action return loops decrease **5,293 -> zero**. A broader repeated-state check also decreases sharply, so the improvement is not limited to changing the period of the old short loop.

## Implemented behavior

Recovery retains the original goal, useful route and normal stall/replan state. Observed escape candidates remain within two units and the waypoint cap, with physical capsule sweep and normal destination clearance. Prefer a half-unit neighbourhood of extra observed planning clearance, then score travel distance and progress toward the retained goal. A preferred escape releases within the normal 0.5-unit tolerance only once the actor itself has that extra clearance. A normally clear narrow-space fallback uses a 0.2-unit arrival tolerance. Resume and validate the retained route, rerouting when necessary; preserve exit discoveries made during recovery.

Keep 32 position/target samples across waypoint changes. Three recurring returns involving recovery trigger a different escape; twenty escape actions without 0.05 units of new closest approach also escalate. A retained-goal closest-approach counter independently escalates after sixty actions without 0.05 units of new goal approach after recovery begins. Escape handoffs do not reset that goal-progress budget. Reaching a normal route waypoint ends its recovery window; a genuinely new goal establishes a new baseline. Avoid the last failed escape by 0.75 units; three recovery failures abandon the goal through the existing frontier-exclusion/exit-deferral rules.

Policy inputs/actions, ordinary waypoint clearance, collision checks, movement speed, rewards and weights are unchanged. Recovery waypoints use the same learned movement policy. The [current design](../../../../../Training/PCG-system-design.md#overall-navigation-and-action-design) owns the full contract.

## Matched frozen comparison

Same seeds **510000-510099**, exact spawn/exit coordinates and configuration as [QA-64](../qa64-churn-20261116-035023/README.md). Normal observed-map frontier navigation in a **50 x 50 empty room**, initially unseen exit 30-40 units away, twelve-unit vision, ten-unit waypoint cap and sixty-second limit. Original QA-50 checkpoint SHA-256 `acffd0594bb6a03a157e2853688dfc4d7647606d052cb241fe620d9f8eb4db64`, 656,793 model steps / 25,610 optimizer epochs / 4,700 lineage episodes. Zero weight updates or new training steps.

| Measurement | QA-64 | QA-65 |
| --- | --- | --- |
| Exit discovery | 71/100 | 100/100 |
| Exit completion | 69/100 | 100/100 |
| Actions | 34,505 | 24,443 |
| All waypoint assignments | 18,026 | 3,104 |
| Assignments per 100 actions | 52.24 | 12.70 |
| Normal route / escape assignments | 9,459 / 8,567 | 2,502 / 602 |
| Blocked-start replacements | 7,977 | 583 |
| Goal selections / route searches | 9,324 / 9,328 | 1,944 / 1,947 |
| Planner geometry builds | 22,753 | 23,734 |
| Median instruction lifetime | 0.1 s | 0.5 s |
| Exact two-action return cycles | 5,293 in 30 cases | 0 |
| Broader recovery-related returning states | 11,847 in 38 cases | 118 in 6 cases |
| QA stuck-detector windows | 535 in 26 cases | 0 |
| Actions without a waypoint | 0 | 0 |
| Action-phase wall time | 2,749.96 s | 1,576.83 s |
| Actions per wall-clock second | 12.55 | 15.50 |

Actions decrease 29.16%; full action-phase wall time decreases 42.66%. Timing excludes Unity startup and preflight, using the ready file and hundredth episode record. It is sequential local end-to-end timing, not an isolated planner or production benchmark. Geometry builds increase, consistent with more genuine exploration and new map knowledge; planner snapshot rebuilding remains a separate cost. The 87 new low-displacement actions are different from repeated back-and-forth movement; all episodes complete and the separate stuck-window detector records none.

Every assignment version is accounted for: 1,361 normal arrivals, 370 goal-cell invalidations, 84 exit preemptions, 100 episode completions, 583 blocked-start replacements, 584 recovery arrivals, fourteen oscillation escalations, four stalled escapes, one retained-goal progress escalation and three segment invalidations. Four recovery budgets are exhausted and abandon their goals; these are recovery transitions, not additional assignment endings.

The broader check finds a before-action position within 0.1 units and waypoint within 0.25 units of a state 2-100 actions earlier, with recovery in between and at least 0.05 units of intervening immediate movement. Count each returning action once. This is a descriptive recurrence measure, not a count or duration of distinct failures; legitimate revisits can qualify. **118 returns remain across six successful cases**, so this result does not claim all oscillation or inefficient movement is eliminated.

Worst old seed 510009 improves from **600 actions / 594 assignments / timeout** to **135 actions / 29 assignments / completion**. Seed 510025, used to diagnose the longer-cycle weakness during implementation, completes in 548 actions / 47 assignments. The slowest final case takes 553 actions (55.3 simulated seconds), so some movement remains inefficient.

## Verification, refinement and limits

Existing fixtures pass: 100 seeded resets, eight compass completions, map-barrier/unknown-gap/recovery/reward checks, and all ten reference completions with ten exit discoveries and 248 handoffs. Parent checkpoint/source hashes, source snapshots and restored settings verified; evaluation and Unity processes exited. QA-50 remains selected and QA-62 remains stopped. No training, deployment, commit or push.

The implementation was refined using this reused benchmark, especially seed 510025. An eight-sample history and then a 32-sample history each completed 38 partial cases with 37 exits before interruption: the first missed a 16-17-action cycle, and the second exposed a later roughly 67-action cycle. The retained-goal watchdog addresses the longer recurrence without continually enlarging history. These partial runs are preserved and **not combined with the final 100-case result**. Two earlier preflight-only launches completed zero model episodes while arrival/goal bookkeeping was finalized. Retained campaigns: [first preflight](../qa-65-churn-20261116-055835/campaign.json), [second preflight](../qa65-churn-20261116-055948/campaign.json), [eight-sample partial](../qa65-churn-20261116-060101/campaign.json), [32-sample partial](../qa65-churn-20261116-061135/campaign.json).

This demonstrates the combined recovery changes on a diagnosed, reused empty-room benchmark. Individual changes were not ablated. It does not establish unseen-dungeon, corridor, narrow-space fallback, combat, puzzle or production reliability. No curriculum advancement follows automatically.

[Measured result](churn-result.json), [per-case results](churn-episodes.json), [every assignment](churn-assignments.json), [exact return cycles](two-action-loops.json), [broader return analysis](recovery-return-states.json), [episode records](episodes.jsonl), [navigation events](navigation-events.jsonl.gz), [action trace](exit-decisions.jsonl.gz), [evaluation accounting](evaluation-result.json), [verification](verification.json), [campaign](campaign.json), [source hashes](run.json), [config](training-config.yaml), [source snapshot](source/), [runner](evaluate_waypoint_churn.py), [short-cycle analysis](analyze-two-action-loops.py), [broader-return analysis script](analyze-recovery-returns.py).

Local runtime `Logs/qa50-exploration-waypoints/20261116-062408-evaluate/`; preflight `Logs/qa50-exploration-waypoints/20261116-062334-verify/`.
