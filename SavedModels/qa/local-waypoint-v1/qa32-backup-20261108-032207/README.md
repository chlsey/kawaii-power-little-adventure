# QA-32: 8-12-unit waypoint continuation

**Complete: 492/500 training arrivals (98.4%), then 99/100 frozen deterministic arrivals (99%).** Eight training timeouts and one frozen timeout; no deaths or actual game completions. Increased the distance band from **6-10 to 8-12 units**, raising both bounds by two units. No reward or source changes. All sessions stopped; no subsequent stage started.

| Phase | Arrivals | Timeouts | Mean successful arrival | Median | 90th percentile | Successful range |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Stochastic training | 492/500 | 8 | 3.613 s | 3.10 s | 4.30 s | 1.90-52.10 s |
| Frozen deterministic evaluation | 99/100 | 1 | 2.991 s | 2.80 s | 4.10 s | 2.00-4.80 s |

Times are game seconds at 0.1 seconds per movement action. Arrival-time statistics exclude failed attempts, which all timed out at 60 game seconds / 600 actions. Frozen timeout seed **284011**: initial distance **11.7293**, final distance **6.1208**, target X/Z **(-0.89466, 11.69513)**, no arrival bonus. The complete record is retained in the frozen episode file.

## Continuation and task

Continued full [QA-31 checkpoint](../qa-waypoint-reach-20261107-185943-train/README.md), including actor, critic and optimizer, SHA-256 `5ed4da94b4ab957a61965163b28d0ae1dbfa45e145b9da8ff0712f120bc89062`. Parent checkpoint unchanged; no fresh transfer.

Starting branch counter **40,668**; added **22,578 training steps / 500 episodes**, with **880 optimizer updates**, reaching **63,246 branch model steps / 7,000 lineage training episodes**. This lineage follows QA-27 -> QA-30 -> QA-31 -> QA-32. The separate farther-stage QA-28/QA-29 experiments are excluded and remain preserved.

Same empty 40 x 40 room, fixed centre spawn, random fixed assigned waypoint, 12-unit vision, 0.5-unit arrival radius and 60-second / 600-action cap. No exit or automatic navigator. Arrival ends only the skill drill; all `game_completed` flags remain false. These direct instructions do not change the normal navigator's six-unit assignment limit.

Same `local-waypoint-v1`, 134 -> 64 -> 64 -> 2. PPO learning rate 0.0002, entropy 0.01, gamma 0.99, rollout 256, batch 64 and CPU unchanged. Training seeds **8500-8999**, frozen evaluation seeds **284000-284099**. The scene retains nearby defaults; the selected config applies 8-12 at runtime.

Reward unchanged: **+1 arrival**, **+0.1/unit new closest approach**, **-0.001/action**, no exploration/stuck reward in the isolated drill. Combined positive waypoint budget **+5 per attempt**, below the **+50 local-objective reward floor**. Positive maxima **2.167370 training / 2.172462 evaluation**. No exit-sized payout; competing-objective/combat priority remains untested. The known timeout termination/truncation behavior was not changed in this curriculum experiment.

## Evidence and verification

- [Actual config](training-config.yaml), [run/source provenance](run.json), [preflight](verification.json), [checkpoint manifest](checkpoints.json).
- [Training result](training-result.json), [500 training records](training-episodes.json), [frozen result](evaluation-result.json), [100 frozen records](evaluation-episodes.json).
- [8-12-unit config](../../../../Training/qa-player-waypoint-reach-8to12.yaml), [runner](../../../../Training/run_waypoint_reach.py). Detailed local-only logs: `Logs/qa-waypoint-reach-training/20261107-214655-train/`.

Configured-range/encoding, reference following, no-exit, idle-timeout and combined-reward-cap preflight checks passed. All 600 episode records checked. Frozen evaluation made zero updates; policy tensors, counters and checkpoint bytes were unchanged. Source hashes matched, parent preserved, project settings restored and Unity sessions exited successfully.

The cumulative 50,668-step and 60,668-step snapshots and final ZIP passed byte-count, CRC and SHA-256 checks. Final model SHA-256: `0ca5d55955bf6ea7489e2495c1a1cbd9c3be773154981483096dd17d4b4479e0`. The result field `new_model_steps` is cumulative; `initial_model_steps` and `new_training_steps` separate starting and added steps.

This measures sampled unobstructed 8-12-unit performance. No matched pre-training 8-12 evaluation establishes improvement attributable to the 500 episodes. Other distance bands, obstacles, outside-vision targets and competing objectives were not evaluated in this run. No automatic curriculum advancement.
