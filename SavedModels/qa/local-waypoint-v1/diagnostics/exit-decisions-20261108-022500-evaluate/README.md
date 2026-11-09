# QA-35: exit-waypoint arrival and following actions

**All 151 attempts that reached the final exit-directed waypoint cleared the exit. Zero timed out afterward.** Of the 85 arrivals that had not already triggered completion, every next action pointed toward the exit. All completed within another 0.2 game seconds. These observations place the recorded failures earlier in search/approach; they do not isolate their underlying cause.

This is another 500 frozen Unity evaluations, replaying QA-33's exact placements (seeds 285000-285499) with added read-only tracing. All 500 completion outcomes, step counts and final distances match QA-33. These are repeated cases, not 500 new held-out placements. No policy, reward, navigator, action or observation-contract change was applied. QA-32 stays at 63,246 training steps / 7,000 actor-lineage episodes, with zero optimizer updates. All sessions finished.

## Attempt counts

| Event reached at least once | Attempts | Eventually cleared | Eventually timed out |
| --- | ---: | ---: | ---: |
| Any assigned waypoint | 479 | 347 | 132 |
| A waypoint while the exit was currently visible at arrival | 321 | 306 | 15 |
| An exit-directed waypoint, including intermediate route points | 325 | 306 | 19 |
| The final exit-directed route waypoint | 151 | 151 | **0** |
| Final exit waypoint assigned, whether reached or not | 221 | 217 | 4 |

Totals are **364/500 actual-exit clears (72.8%)**, **136 timeouts**, no deaths. Rows overlap and must not be added. There were 669 distinct exit-directed arrival events across 325 attempts; the table counts attempts once. Visibility and goal type are separate criteria: a visible-exit arrival can still concern an exploration waypoint, while an exit-directed arrival can occur without current exit visibility.

The 136 timeouts partition into:

| Furthest relevant observed event | Timeouts |
| --- | ---: |
| Never discovered the exit | 101 |
| Discovered it, without reaching any exit-directed waypoint | 16 |
| Reached an intermediate exit-directed waypoint, without reaching the final waypoint | 19 |
| Reached the final exit waypoint | **0** |

Many successful attempts enter the exit trigger before reaching the waypoint's 0.5-unit arrival radius, so a clear does not require a recorded final-waypoint arrival.

## What happened after final arrival

- 66/151 final-waypoint arrivals already had actual exit completion on that action.
- The other 85/151 had another action. All 85 commanded movement toward the exit centre; none commanded movement away or perpendicular to it, or zero movement.
- In all 85, the navigator's next status was `exploring`. This is a recorded goal handoff, not a failure: all 85 still cleared immediately afterward. An exploration-labelled waypoint can still point broadly toward the exit.
- From first final arrival to actual completion: mean **0.0576 s**, median **0.10 s**, maximum **0.20 s**, including the 66 immediate completions. Times are game seconds, not wall-clock duration.

The learned policy combines ray channels and a waypoint instruction into two continuous movement outputs; it has no separately recorded choice called "take exit". These results support reliable exit entry once the final waypoint is reached in this scenario. They do not establish that its earlier steering is always correct or that navigation priorities cannot conflict.

There are earlier mixed behaviours: 58 attempts (30 eventual timeouts) contain at least one visible-exit action with a nonpositive movement projection toward the exit while waypoint guidance is within 60 degrees of the exit direction. Forty attempts (four eventual timeouts) contain visible-exit actions aligned with a waypoint that points away/perpendicular to the exit. These categories overlap; single actions are not causal explanations. They motivate inspecting earlier approach/replanning traces rather than inferring refusal from a terminal navigation-status label. No ablation, retraining, counterfactual rollout or behavioural fix was performed here.

## Measurement and unchanged conditions

Arrival means an active assigned waypoint crossed from **greater than 0.5 units to at most 0.5 units** during an action. The trace captures the assignment before movement and measures arrival after reward calculation, **before the coordinator replaces the waypoint**. An exit-directed assignment means the coordinator's goal is an observed exit cell. A final waypoint equals the last point of that route; its position can lie near the exit boundary rather than at the true exit centre. True exit positions are retained only in diagnostic records and never fed into control.

Recorded fields include seed/action, before/after positions, exit position, waypoint position/version, exit-goal/final-waypoint flags, arrival, current exit visibility, exact movement outputs, and episode completion. Exact 134-feature policy inputs are also retained when the exit is visible, the goal is an exit, or the actor is within three units of the exit. All **155,459 action records** reconcile with the 500 episode records. No trace action was added to the evaluation budget.

Unchanged: frozen QA-32 SHA-256 `0ca5d55955bf6ea7489e2495c1a1cbd9c3be773154981483096dd17d4b4479e0`, unmasked `local-waypoint-v1` / 134 inputs and two movement outputs, empty 40 x 40 room, unseen initial exit 30-34 units away, 12-unit vision, visible/clear navigator waypoints within six units, 60-second deadline, +50 actual-exit reward and +5 combined positive waypoint cap. The exit box is 2 x 3 x 2 units; reaching a waypoint never substitutes for the actual trigger. No obstacles, enemies or competing gameplay objectives were tested.

Preflight checked all 500 unseen placements, eight reference compass completions, route/clearance/recovery/reward rules and 10/10 reference exit clears with 157 handoffs. Reference attempts are excluded from these 500 learned-policy evaluations. The runner verified unchanged policy tensors, optimizer update count, checkpoint bytes and source hashes, and restored project settings before exiting successfully.

## Evidence and reproduction

- [Analysis summary](analysis.json), [per-attempt analysis](episode-analysis.json), [waypoint arrival events](waypoint-arrivals.json).
- [All episode records](episodes.jsonl), [all action records, gzip JSONL](exit-decisions.jsonl.gz).
- [Frozen result](evaluation-result.json), [run/source identities](run.json), [config](training-config.yaml), [trace geometry](exit-trace-contract.json), [policy readiness](policy-contract.json).
- [Previous matched integration report](../distant-exit-20261107-220640-evaluate/README.md).
- Local-only run: `Logs/qa-waypoint-exit-decisions/20261108-022500-evaluate/`; preflight: `Logs/qa-waypoint-exit-decisions/20261108-022300-verify/`.

From the game directory, with Unity closed:

```powershell
python -B Training/run_local_movement.py --config Training/qa-player-waypoint-exit-decisions.yaml --mode verify
python -B Training/run_local_movement.py --config Training/qa-player-waypoint-exit-decisions.yaml --mode evaluate --episodes 500
python -B Training/analyze_exit_decisions.py --run Logs/qa-waypoint-exit-decisions/20261108-022500-evaluate --output SavedModels/qa/local-waypoint-v1/diagnostics/exit-decisions-20261108-022500-evaluate --baseline SavedModels/qa/local-waypoint-v1/diagnostics/distant-exit-20261107-220640-evaluate/episodes.jsonl
```

Use the newly printed timestamp directory when repeating the evaluation. Stage advancement remains manual; no model promotion, training or PCG run follows from this diagnostic.
