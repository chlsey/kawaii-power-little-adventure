# QA-43: another 500 waypoint-chain episodes from QA-42

**Complete: 500 new training episodes, followed by 100 frozen ten-target chains. QA-43 completes 42/100 versus QA-42's 41/100.** Four cases are gained and three lost, with 38 common successes and 55 common failures. Completion is effectively flat on this reused benchmark: +1 point, exploratory paired case-bootstrap 95% interval -4 to +6 points, exact paired p=1.0. The shared successful cases are faster: **21.59 -> 17.08 game seconds**. Parent preserved; no deployment or further training.

## Training

| Block | Episodes | Complete chains | Timeouts | Arrivals | Training actions |
| --- | ---: | ---: | ---: | ---: | ---: |
| Two targets | 100 | 94 | 6 | 194 | 7,679 |
| Three targets | 100 | 89 | 11 | 283 | 13,578 |
| Four targets | 100 | 87 | 13 | 370 | 15,797 |
| Random 1-10 targets | 200 | 138 | 62 | 852 | 49,417 |

Exactly **408/500 complete training chains, 92 timeouts, 1,699 waypoint arrivals / 1,996 planned targets**. Fresh training seeds **11500-11599**, **11600-11699**, **11700-11799**, then **11800-11999**; none overlap the preceding 500 training seeds. The stochastic policy changes during training and placements differ from QA-42; frozen matched evaluation provides the performance comparison.

The final block draws a fresh length uniformly from one to ten per episode:

| Random length | Training episodes | Complete chains |
| --- | ---: | ---: |
| 1 | 20 | 20 |
| 2 | 21 | 20 |
| 3 | 20 | 20 |
| 4 | 19 | 17 |
| 5 | 20 | 13 |
| 6 | 21 | 10 |
| 7 | 19 | 9 |
| 8 | 20 | 12 |
| 9 | 21 | 12 |
| 10 | 19 | 5 |

## Frozen matched evaluation

Parent replay before learning reproduces every saved QA-42 outcome, action count, arrival count and full coordinate sequence on all 100 cases. Final child uses those same coordinates and seeds **288000-288099**, deterministic actions, zero reward and no learning updates.

| Measure | QA-42 parent | QA-43 child |
| --- | ---: | ---: |
| Complete ten-target chains | 41/100 | 42/100 |
| Timeouts | 59 | 58 |
| Waypoint arrivals | 681 | 677 |
| Arrival-driven handoffs | 640 | 635 |
| Mean episode duration including 60-second timeouts | 44.59 s | 42.47 s |
| Successful mean, different success subsets | 22.41 s | 18.26 s |
| Mean on the same 38 successful chains | 21.59 s | 17.08 s |

| Waypoint reached | QA-42 parent | QA-43 child |
| --- | ---: | ---: |
| 1 | 100/100 | 100/100 |
| 2 | 98/100 | 97/100 |
| 3 | 91/100 | 89/100 |
| 4 | 75/100 | 74/100 |
| 5 | 69/100 | 66/100 |
| 6 | 61/100 | 62/100 |
| 7 | 55/100 | 54/100 |
| 8 | 48/100 | 48/100 |
| 9 | 43/100 | 45/100 |
| 10 | 41/100 | 42/100 |

The last table is cumulative out of all 100 chains. Child attempts 735 targets, reaches 677 and times out on 58; 265 planned later targets are unattempted. Both models still reach the first target in every case, with substantial later sequence failure. This continuation shows a speed benefit on the recorded common-success subset, not a clear completion-reliability improvement.

Mean capped duration falls **2.121 seconds**, paired case-bootstrap 95% interval **0.382-3.937 seconds saved**. Intervals use 20,000 paired case resamples, seed 42. They describe this single model pair and case set, not variability across independently trained models. The benchmark was previously diagnosed and reused in selecting the training schedule; it is not an untouched test set. Both frozen traces preserve previous-action inputs on every continuing action and handoff. No history-masking intervention or causal diagnosis of the remaining failures was performed.

## Failure rate by assigned distance

Saved-record analysis of this frozen evaluation groups each of the 735 attempted targets by actual distance when assigned. No new training or evaluation was run; the next requested 500 episodes were held before launch.

| Assignment distance | Failed / attempted waypoints | Failure rate |
| --- | ---: | ---: |
| 1 to under 4 units | 14/268 | 5.2% |
| 4 to under 7 units | 18/268 | 6.7% |
| 7 to under 10 units | 26/199 | 13.1% |

The highest one-unit band is **7 to under 8 units: 14/89 failures (15.7%)**. After excluding the always-successful first target, that band's rate is 14/77 (18.2%). Bins describe observed association; positions, turn angles and previous movement differ, so this does not isolate distance as the cause or establish that reducing the cap will fix sequence following. Rates use attempted waypoints; later unattempted targets are excluded.

Of the 58 failed targets, **15 came within one unit** and **37 within two units** (arrival requires 0.5). All 58 had at least 30 seconds on that same target. Longer assignment distance is therefore one observed concentration, while final approach also deserves inspection. [Full bins and chain-position counts](distance-analysis.json).

### Timeout versus physical immobility

All 58 failed chains end at the 60-second / 600-action deadline. Each spends 35.1-58.0 seconds on its final, unreached waypoint (mean 51.59 s). Mean path length on that one target is 206.22 units, range 140.29-232.00; whole failed chains cover 237.52-240.00 units. Every one of the 29,924 failed-leg actions moves at least 0.05 horizontal units. Commands point away/perpendicular on 13,535/29,924 actions (45.2%), mean command alignment 0.026. This is sustained target-tracking failure while moving, consistent with looping/oscillation, rather than stationary blockage.

The controller also records 23 `stuck_events` in two failed chains. This counter checks net displacement over a multi-action window, so returning near the starting point can trigger it even when each action moves. It is not an episode termination condition in the chain adapter. Example seed 288000 reaches three targets in 2.7 s, then spends 57.3 s on target four: starts 5.82 units away, closest 3.81, final 4.76. The target is never replaced; the deadline ends the chain. [Motion evidence](timeout-motion-analysis.json). No new run, training or change to recovery rules.

## Same task and learning rules

Full actor/critic/optimizer continuation from [QA-42](../qa-waypoint-chain-20261108-183246-train/README.md). Same 134 -> 64 -> 64 -> 2 local-waypoint-v1 interface; PPO LR 0.0002, gamma 0.99, entropy 0.01, rollout 256, batch 64, ten optimization epochs, CPU. Same 100/100/100/200 four-block recipe, 60-second / 600-action whole-chain deadline and 0.1-second actions.

Empty 40 x 40 room, centre reset only at chain start, no exit/navigator/exploration or intermediate resets. Precomputed seeded random walks use uniform nominal angles/radii 1.5-9.5, rejecting points outside +/-15 X/Z. The 0.5-unit arrival tolerance keeps actual assignments 1-10 units away; observed bounds across all phases **1.05108-9.90670**. Arrival immediately switches the instruction while preserving player state, prior action, sensors and metrics; targets remain fixed until arrival or timeout.

Training reward stays `(0.1 * new closest approach + 1 * arrival) / planned chain length - 0.001/action`, with combined positive cap five below the +50 objective floor. Each leg retains feedback; there is no exit payout. Frozen diagnostic reward remains zero. Reference verification completed 100/100 random-length chains / 548 arrivals covering lengths 1-10, with reward accounting checked.

Only runner bookkeeping changed: lineage and seed ranges now derive from config, and replay references the configured parent's saved evaluation. The Unity environment, observation/action semantics, rewards and optimization rules are unchanged. Block-end partial rollouts discard **455 collected transitions** rather than start extra episodes: 86,471 actions collected, 86,016 entered optimized rollouts. Existing timeout/termination handling is unchanged.

## Lineage and completion checks

- Parent QA-42: **174,332 steps / 6,780 updates / 8,000 lineage episodes**; SHA-256 `0bd60198d0904f0099c1e19691a1dc553c220b512542576d1c56eff0b2b0025d`. All 24 parent archives unchanged.
- Child QA-43: **260,803 steps / 10,140 updates / 8,500 lineage episodes**, adding **86,471 steps / 3,360 updates / 500 episodes**. Final SHA-256 **`a635f6607dc8fb713051cd3a7e6b40ba800cbc4fbf6d7e2689e6805fbe48b788`**.
- All **20** child archives passed manifest/hash/CRC checks. Final evaluation adds zero steps/updates; 43 sampled saved-model predictions match its traces. Source files match retained snapshots. All seven Unity phases finished normally, project settings restored, no experiment processes remain.

The 100 reference controls, 100 parent replays and 100 frozen child cases do not add actor-training lineage. No actual-exit navigation, obstacles, combat, generated-dungeon integration, retention outside 1-10 units or deployment was evaluated here.

## Evidence and reproduction

[Final checkpoint](final_model.zip), [block/periodic checkpoint manifest](checkpoints.json), [500 training records](training-episodes.json), [training summary](training-result.json), [comparison](comparison.json), [analysis](analyze.py), [completion checks](completion-verification.json), [run-owned config](training-config.yaml), [parent/source identities](run.json).

[Two-target block](training-1-result.json), [three-target block](training-2-result.json), [four-target block](training-3-result.json), [mixed block](training-4-result.json), [parent frozen summary](parent-evaluation-result.json), [child frozen summary](evaluation-result.json), [reference checks](verification/result.json), [child actions](evaluation/actions.jsonl.gz), [parent actions](parent-evaluation/actions.jsonl.gz), [child leg records](evaluation/legs.jsonl), [child cases](evaluation/episodes.jsonl). Phase folders retain exact target coordinates and session settings.

From the game directory with other Unity sessions closed:

```powershell
python -B Training/run_waypoint_chain.py --config Training/qa-player-waypoint-chain-continuation.yaml --mode verify
python -B Training/run_waypoint_chain.py --config Training/qa-player-waypoint-chain-continuation.yaml --mode train
```

The recipe pins QA-42 and starts a new run; it does not resume QA-43 or auto-select a model. Reanalyze saved data only:

```powershell
python -B SavedModels/qa/local-waypoint-v1/qa-waypoint-chain-20261108-194516-train/analyze.py
```

[Maintained config](../../../../Training/qa-player-waypoint-chain-continuation.yaml), [runner](../../../../Training/run_waypoint_chain.py), [session guide](../../../../Assets/Scenes/QaSingleRoomTraining.README.md#random-waypoint-chain-curriculum), [training history](../../../../Training/changelog/qa-player.md). Local runtime: `Logs/qa-waypoint-chain-continuation/20261108-194516-train/`. All authorized work complete and sessions stopped.
