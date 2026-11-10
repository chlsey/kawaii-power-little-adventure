# QA-48 behavioral diagnosis

Completed 2026-11-09; frozen analysis and ten diagnostic episodes, zero learning updates. This diagnoses the finite eight-room QA-48 run, not the proposed endless sequence. QA-47 remains the preferred preserved checkpoint.

## Measured failure

Read all 100 matched parent and child cases (seeds 453000-453099), 600 actions each. A guided action has a valid waypoint; away/perpendicular means the action dot target direction is <=0. Low motion means displacement <0.02 world units per 0.1-second action. A strong command would move >=0.2 units without obstruction. Direction alignment alone is not a complete obstacle-avoidance score.

| Measure | QA-47 parent | QA-48 child |
| --- | ---: | ---: |
| New rooms per minute | 1.22 | 0.54 |
| No new room cases | 38/100 | 64/100 |
| Low-motion actions | 12,912/60,000 (21.52%) | 25,062/60,000 (41.77%) |
| Guided away/perpendicular commands | 12,653/47,011 (26.91%) | 42,709/58,344 (73.20%) |
| Strong commands with low motion | 6,110 | 17,570 |
| Missing-waypoint actions | 12,989/60,000 | 1,656/60,000 |

All strong-command/low-motion actions also have a wall ray hit within 0.5 units along the command heading (nearest ray +/- one of 64 rays). This supports pressing into sensed walls, rather than simply choosing no movement. It is sensor evidence, not an exact collision-contact classification. Guidance is present for 97.24% of child actions, so missing waypoints do not explain the main regression.

On 11,671 identical guided observations sampled every fifth child action, QA-47 commands away/perpendicular on 4.88%, QA-48 on 73.22%. This controls the input state: the changed policy weights themselves produce markedly worse target alignment. Clearing QA-48's previous-action inputs reduces that figure only to 62.23%; this is an offline sensitivity probe, not a tested fix. Prior-action history is not established as the sole cause.

## Recorded examples and frozen recovery

- **453043, wall pin:** QA-47 enters one new room; QA-48 enters zero, travels 14.44 units and has 559 low-motion actions (55.9 seconds). A waypoint exists for 599/600 actions. At 12 seconds it is left of the actor, while the actor commands right/up into a sensed wall. Replaying exactly the first 120 child actions then switching to QA-47 yields one new room and 79 low-motion actions total. Switching instead to direct waypoint-direction steering yields three rooms and the same 79 low-motion actions, all already incurred in the replayed prefix. Both escape the same physical state with the same navigator.
- **453093, larger regression:** QA-47 enters five new rooms; QA-48 enters one and spends 37.1 seconds on strong commands with negligible motion against nearby sensed walls.
- **453045, movement/recovery loop:** QA-48 travels 181.51 units with no low-motion actions and no away/perpendicular commands, yet enters zero new rooms. There are 563 waypoint assignments, 280 replans and 282 arrivals; its targets alternate mostly between (119,36) and (119,37). Replaying the first 100 child actions then using direct target steering reaches five rooms, with no low-motion actions. The reference from reset reaches six rooms with zero replans. Thus some failures involve movement interacting with navigator recovery, even when target alignment is positive. This is not evidence that the dungeon is unreachable or that the navigator always fails independently of control.
- **453033, working case:** both policies enter four new rooms.
- **453054, improved case:** parent enters zero, child two; the parent lacks guidance for 501 actions. The regression is aggregate, not universal.

Recovery replay prefixes matched every recorded observation within 1e-5 before the controller switch. Trials preserve pose, history and navigator state, run the full 600 actions, and apply no optimizer updates. Selected cases are counterfactual diagnoses, not a new 100-case benchmark.

## Action-noise check

On seeds 453043/453093/453045, deterministic child outcomes reproduce the original traces exactly. Sampling the frozen policy with one fixed action seed per layout gives 0/1/1 rooms versus deterministic 0/1/0. Wall-pin cases still incur 548/358 low-motion actions; the cycling case gains a room then incurs 333 low-motion actions. Noise alone does not reliably fix these examples. Three cases with one sampled seed each do not establish general stochastic performance.

## Interpretation and next decision

The observed failure is predominantly degraded local steering and wall recovery, with a second movement/navigation cycling failure. Retention regressions on the earlier two-room and empty-waypoint tasks also show loss of prior skill. Reward and task geometry changed together; these data do not isolate reward sparsity, a specific optimizer setting, or history as the training cause.

Keep QA-47 as the baseline. Before further learning, investigate the diagnosed wall states and waypoint arrival/replanning loop, and require both matched room progress and prior-skill retention to pass before replacing that baseline. These are recommendations, not implemented behavior changes or permission for a new run.

## Evidence and limits

- [Full aggregate and identical-input probes](analysis.json), produced by [read-only analyzer](../../../../../Training/analyze_qa48_behavior.py).
- [Frozen action-noise results](noise-comparison.json) and [retained episodes/traces](noise-replay/).
- [Exact-prefix recovery results](recovery-comparison.json) and [retained episodes/traces](recovery-replay/).
- [Replay source data](replay-data.json); interactive comparison in repository-root `work/qa48-analysis/agent-movement-replay.html` uses actual recorded positions/actions/waypoints and nearest sensed wall hits. Floor footprints are not exact collider outlines; this is not a game-camera video.
- [Original finite-run report](../README.md). Parent SHA-256 `2eafa17b18b59271d93ac2d0a4a3f95240788296f5c8132d9f20b33740c7477d`; child SHA-256 `a11cc79beda3b7ef44b462f1bb798c0e8470a77f7b504dfcc2c2a051ee2574df`. Both remain unchanged. Child counters remain 596,793 steps / 23,280 optimizer updates.

Replay preview inspected at desktop and narrow widths; case selection, scrubbing and playback checked, with no runtime exceptions. Diagnostic Unity and preview browser processes stopped. No new training, deployment, commit or sync was performed.
