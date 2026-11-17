# QA-63: retain valid navigation instructions

Completed frozen verification on 2026-11-16 UTC. No training or model updates. The user stopped the QA-62 campaign before this change; its final checkpoint and partial evaluation remain [preserved separately](../../qa-waypoint-20261115-090609-train/README.md). QA-50 remains selected.

The coordinator retains a valid waypoint and route, consumes the reached route prefix, and searches again when invalidation or recovery requires it. Goal selection runs when a goal is missing, completed or invalid, recovery abandons it, or discovery can make an exit reachable. Unrelated free-cell discovery preserves the current exploration intention. Planner geometry is cached until observed navigation knowledge, grid identity or clearance changes; visited flags alone do not invalidate it. Policy inputs, weights, rewards and clearance checks are unchanged.

## Frozen comparison

Original QA-50 checkpoint SHA-256 `acffd0594bb6a03a157e2853688dfc4d7647606d052cb241fe620d9f8eb4db64`, 656,793 model steps / 25,610 optimizer epochs / 4,700 lineage episodes. Ten reused seeds 510000-510009 match the first ten cases of the [pre-retention navigator baseline](../qa50-exploration-20261115-070559-evaluate/README.md), including exact spawn and exit coordinates. Normal frontier navigation in a 50 x 50 empty room, initially unseen exit 30-40 units away, twelve-unit vision, ten-unit waypoint cap, sixty-second deadline. This is a small regression/performance check, not a new reliability estimate or generated-dungeon test.

| Measurement | Before retention | After retention |
| --- | --- | --- |
| Exit completions | 9/10 | 9/10 |
| Actions | 2,933 | 2,944 |
| Waypoint assignments | 1,304 | 1,304 |
| Action-phase wall time | 490.67 s | 147.83 s |
| Actions per wall-clock second | 5.98 | 19.92 |

Action-phase time is measured from each run's Unity-ready file to the tenth episode record, excluding Unity startup and verification. Observed throughput is 3.33 times the earlier run; these sequential local measurements are not isolated planner benchmarks. The new counters record **2,250 planner builds, 695 goal selections and 695 route searches** over 2,944 actions. Newly observed cells still invalidate planner geometry often. Waypoint churn remains: the timeout seed 510009 alone has 594 assignments over 600 actions, despite only eleven planner builds. This change does not establish production readiness or resolve that remaining navigation behavior.

Existing verification passed: 100 seeded resets, eight compass completions, map-barrier/unknown-gap/recovery/reward checks, and ten reference navigation completions with ten exit discoveries and 248 handoffs. Frozen evaluation recorded zero weight updates and zero new training steps; the checkpoint and source hashes stayed unchanged. Project settings were restored and the Python/Unity processes exited. An earlier check was interrupted before completion to finish remembered-exit refresh handling; its local evidence is not combined with this result.

[Measured result](result.json), [episode records](episodes.jsonl), [evaluation accounting](evaluation-result.json), [existing verification](verification.json), [run/source hashes](evaluation-run.json), [config](evaluation-config.yaml), [source snapshot](source/). Local runtime: `Logs/qa50-exploration-waypoints/20261116-024643-evaluate/`; verification: `Logs/qa50-exploration-waypoints/20261116-024408-verify/`. No training restart, deployment, commit or push.
