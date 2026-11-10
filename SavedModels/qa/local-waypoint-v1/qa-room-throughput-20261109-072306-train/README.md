# QA-48: finite eight-room throughput experiment

**300 full-minute training episodes completed, but the child regressed. Keep QA-47 as the preferred parent.** This experiment uses eight rooms; it does not implement the subsequently selected endless sequence.

| Frozen measure | QA-47 parent | QA-48 child |
| --- | --- | --- |
| New rooms/minute, excluding spawn | 1.22 | 0.54 |
| Median new rooms | 1 | 0 |
| Best new-room count | 5 | 4 |
| Episodes reaching no new room | 38/100 | 64/100 |
| Two-room exit retention | 54/100 | 30/100 |
| Empty-room waypoints/minute | 44.85 | 34.61 |
| Empty-room cases reaching ten targets | 100/100 | 90/100 |

Counts above are additional distinct rooms beyond the starting room. Each generated layout has eight empty 20 x 20 rooms in a 160 x 160 placement area, so at most seven new rooms exist. All episodes run the full 600 actions / sixty simulated seconds. The actual exit is disabled and does not end or reward the exercise. There are no enemies, loot, puzzles or decorative obstacles.

The real dungeon manager generates and initializes the scene. A test-only oracle selects the nearest unvisited room by generated-floor A* route length, then supplies visible, capsule-clear waypoints up to ten units away. A room counts once when the actor is at least one unit inside its footprint. Spawn and revisits do not pay. Movement, previous actions and sensor state persist across goals. The policy receives the unchanged 134-input/two-output local-waypoint-v1 contract, not room coordinates or the oracle map.

## Training

Full actor/critic/optimizer continuation from [QA-47](../qa-two-room-20261109-070143-train/README.md), SHA-256 `2eafa17b18b59271d93ac2d0a4a3f95240788296f5c8132d9f20b33740c7477d`. Exactly 300 seeds 16300-16599 / 180,000 actions. Reward changes to +10 per distinct new room, existing positive waypoint shaping capped at +5 over the entire episode, -0.001/action. Evaluation reward is zero. Same PPO learning rate 0.0002 / gamma 0.99 / entropy 0.01 / rollout 256 / minibatch 64 / CPU.

Exploratory training records **510 new rooms**, mean 1.70/minute / median two / range 0-5, 75 zero-progress episodes, no case reaches all seven. These sampled-action training outcomes do not establish frozen performance. All training visits occurred at ground height (~0.18); no below-floor visit credit was observed.

Final **596,793 branch steps / 23,280 optimizer updates / 4,600 actor-lineage episodes**, increments +180,000 / +7,030 / +300. Exact stopping discards 32 transitions in the final partial PPO rollout. Eighteen periodic full archives plus the final model are retained. [Final model](final_model.zip) SHA-256 `a11cc79beda3b7ef44b462f1bb798c0e8470a77f7b504dfcc2c2a051ee2574df`.

## Evaluation and interpretation

Matched parent/child seeds 453000-453099 use identical generated layouts and full-minute budgets. Mean new rooms declines by 0.68; [paired case analysis](paired-analysis.json) records uncertainty and case changes. Child near-immobile actions (<0.02 units) increase 12,912 -> 25,062 out of 60,000. Missing-waypoint actions fall 12,989 -> 1,656; many poor actions therefore occur while guidance remains available. These are observed behavior differences, not a causal isolation of any policy input, reward term or recovery mechanism.

Two-room retention uses the original 100 seeds 451000-451099, actual exits and the prior heuristic; empty-room retention uses all original 100 full-minute target sequences 288000-288099. Both skills regress. Preserve this experiment as evidence without promotion or deployment; subsequent endless-sequence work should start from QA-47, not this child.

Preflight: three direct-steering controls each reach five new rooms in sixty seconds, with return about 54.4 (+50 room rewards +5 shaping -0.6 time cost); idle reaches zero, earns no shaping and returns -0.6. Visit IDs are unique per episode and exclude spawn. All generated room visits are at floor height. The raw `phase` field is inherited from the two-room diagnostic; use room IDs, room centres and `room-visits.jsonl` for eight-room identities.

## Evidence

- [Config](training-config.yaml), [metadata and source hashes](run.json), [completion and all summaries](completion.json), [checkpoint hashes](checkpoints.json).
- [All 300 training episodes](training/episodes.jsonl), [training visits](training/room-visits.jsonl), [training actions](training/actions.jsonl.gz).
- [Parent evaluation](parent-evaluation/episodes.jsonl), [child evaluation](child-evaluation/episodes.jsonl), [child visits](child-evaluation/room-visits.jsonl).
- [Two-room retention](two-room-regression/episodes.jsonl), [empty-room retention](empty-room-regression/episodes.jsonl).
- [Runner snapshot](source/Training/train_room_throughput.py), [Unity adapter snapshot](source/Assets/Editor/QaTwoRoomNavigationSession.cs).

Local runtime/logs: `Logs/qa-room-throughput/20261109-072306-train/`. Reproduce this finite experiment with `python -u -B Training/train_room_throughput.py --config Training/qa-player-room-throughput-300.yaml --mode train`.
