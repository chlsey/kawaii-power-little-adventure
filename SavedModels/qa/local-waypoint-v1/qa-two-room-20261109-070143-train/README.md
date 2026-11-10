# QA-47: 200 more two-room episodes

**Completed exactly 200 additional training episodes from QA-46. Frozen actual-exit completion improves 31/100 -> 54/100 on identical layouts.** This brings total two-room training to 300 episodes. QA-44 and QA-46 remain preserved; this result does not deploy the model or advance the curriculum.

| Measure | QA-46 parent | QA-47 child |
| --- | --- | --- |
| Actual exits, matched frozen two-room benchmark | 31/100 | 54/100 |
| Timeouts | 69 | 46 |
| Mean duration, all attempts capped at 60 s | 45.302 s | 34.884 s |
| Mean duration among each policy's successes | 12.587 s | 13.489 s |
| Empty-room targets/minute, matched original sequences | 44.17 | 44.85 |
| Empty-room cases reaching ten targets | 100/100 | 100/100 |
| Minimum empty-room arrivals | 11 | 37 |

## Continuation and settings

User requested 200 more episodes. Full actor/critic/optimizer continuation from [QA-46](../qa-two-room-20261109-063812-train/README.md), SHA-256 `494027e711eeece9a87a41fdb7d0f81ab32eaeb9c150dbf6727b667a5ba3ce80`, 360,928 branch steps / 14,070 optimizer updates / 4,100 actor-lineage episodes. Exactly **200 fresh training seeds 16100-16299**, disjoint from QA-46's 16000-16099. The final auto-reset is suppressed, so no 201st training layout is generated.

Everything affecting gameplay/policy/rewards stays the same as QA-46: real `PcgDungeonManager` generation/validation/spawn/exit lifecycle; two empty 20 x 20 rooms with generated corridors/walls/open doors inside a 100 x 100 placement area; same scaled game player prefab and four-unit base speed; no enemies/puzzles/loot/props; 0.1-second actions, actual exit or 600-action/60-second termination. `local-waypoint-v1` remains 134 -> 64 -> 64 -> 2. The fixed oracle uses generated floor/known exit A* and clear instructions up to ten units away; 0.5 arrival tolerance, twenty-action no-progress replan. It retains the same recovery limitation after actor drift. This tests guided traversal, not unknown-exit discovery or combat/interaction.

Training reward stays +50 actual exit, +0.1/new closest waypoint unit and +1/new arrival under one shared **+5 positive attempt cap**, -0.001/action. Evaluation reward is zero. Same PPO LR 0.0002 / gamma 0.99 / entropy 0.01 / rollout 256 / minibatch 64 / CPU. The runner change reads budget/seeds from the explicit config and compares the immediate parent's retained benchmark; no environment/heuristic/hyperparameter change.

**Training:** 145/200 exits, 55 timeouts, 55,865 actions, mean return 40.576; successful mean 15.769 s / capped all-case mean 27.933 s. Maximum positive shaping five. These exploratory sampled-action training outcomes are separate from deterministic frozen evaluation.

**Final:** 416,793 branch steps / 16,250 updates / 4,300 actor-lineage episodes; increments +55,865 steps / +2,180 updates / +200 episodes. Exact stopping discards 57 transitions from the final incomplete PPO rollout while retaining them in the collected-action counter. Eleven periodic full archives plus the final checkpoint are retained.

## Frozen comparison and remaining failures

Before learning, all 100 QA-46 replay outcomes and step counts reproduce its retained result exactly: 31 exits / 69 timeouts / 45,302 actions. The child then receives the same seeds 451000-451099; all floor lists, initial routes, spawn/exit positions and room counts match. This is the **reused diagnosed benchmark** from QA-46, not an untouched test set. QA-44 had 29/100 on these cases; QA-45's earlier 48/100 was a different set.

Paired outcomes: **26 common successes, 28 gained, five lost, 41 common failures**. The +23-point completion gain has an exploratory paired case-bootstrap 95% interval +13 to +33 points (20,000 draws, seed 47); exact paired p=0.0000662. Capped duration saves 10.418 s on average, interval 5.784-15.058 s. On the same 26 common successes, time improves 11.908 -> 11.600 s. Each policy's overall success-only mean uses a different group of cases; its increase does not contradict the matched completion/duration improvement. These summaries measure this benchmark and one training trajectory, not independent-training variability or broad game readiness.

All 100 child cases now reach at least one waypoint; parent had nine failures with no waypoint arrivals. The 46 remaining timeouts end in starting room 27 / corridor 17 / exit room two. Near-immobile actions (<0.02 units) decrease 7,509 -> 5,671; missing-waypoint actions decrease 10,651 -> 6,439, while total actions decrease 45,302 -> 34,884. The fixed planner can still lose usable guidance after drift. The two exit-room failures (451017 / 451033) end 10.435 / 10.590 units from the exit with 498 / 516 no-waypoint actions; they do not reach the final exit trigger. End-location labels alone do not prove an actor never left that region earlier. No single observation channel or recovery mechanism is causally isolated.

**Empty-room retention:** all 100 original full-minute target sequences 288000-288099 match exactly. Mean rises 44.17 -> 44.85 arrivals/minute, every case reaches ten, range improves 11-53 -> 37-53. QA-44 historical mean/range were 44.43 / 37-53. This preserves the measured empty-room skill alongside the improved two-room benchmark; it does not validate larger dungeons or unknown-exit navigation.

## Integrity and evidence

All seven parent archives remain unchanged and pass SHA/ZIP CRC checks. Twelve child archives pass the same checks; final saved model reloads with the correct counters, contract and lineage. Frozen exit/retention phases change no weights or counters. Training episode seeds/counts/rewards are checked, source snapshots match, settings restored and all Unity sessions stopped. Three direct-steering reward controls clear and idle earns zero positive shaping before learning. No further learning stage or deployment was started.

Final [model](final_model.zip) SHA-256: `2eafa17b18b59271d93ac2d0a4a3f95240788296f5c8132d9f20b33740c7477d`.

- [Config snapshot](training-config.yaml), [run/source metadata](run.json), [completion](completion.json), [checkpoint hashes](checkpoints.json).
- [Training summary](training-result.json), [all 200 training episodes](training/episodes.jsonl), [training action traces](training/actions.jsonl.gz).
- [Exit comparison](exit-comparison.json), [paired analysis](paired-analysis.json), [parent replay](parent-evaluation/episodes.jsonl), [child evaluation](child-evaluation/episodes.jsonl).
- [Retention comparison](empty-room-comparison.json), [retention episodes](empty-room-regression/episodes.jsonl), [reward checks](reward-verification/result.json).
- [Exact runner](source/Training/train_two_room_navigation.py), [Unity adapter/heuristic](source/Assets/Editor/QaTwoRoomNavigationSession.cs).

Reproduce from the game directory: `python -u -B Training/train_two_room_navigation.py --config Training/qa-player-two-room-200.yaml --mode train`. Local runtime/logs/original settings: `Logs/qa-two-room-training/20261109-070143-train/`.
