# QA-41: predefined waypoint-chain evaluation

**Frozen QA-39 completed 33/100 ten-waypoint chains; 67 timed out. Every first waypoint was reached (100/100).** This removes exploration, goal selection, route planning and exit discovery. A fixed sequence alone exposes a continuing-control failure that the single-waypoint drill misses. It does not isolate previous-action feedback from changed positions, sensor observations or turn geometry.

## Results

| Waypoint | Attempted | Reached | Timed out here |
| --- | ---: | ---: | ---: |
| 1 | 100 | 100 | 0 |
| 2 | 100 | 97 | 3 |
| 3 | 97 | 89 | 8 |
| 4 | 89 | 69 | 20 |
| 5 | 69 | 61 | 8 |
| 6 | 61 | 53 | 8 |
| 7 | 53 | 45 | 8 |
| 8 | 45 | 39 | 6 |
| 9 | 39 | 34 | 5 |
| 10 | 34 | 33 | 1 |

Counts are conditional: a later waypoint is attempted only after all predecessors are reached. Total **620 arrivals / 687 attempted targets**, with 313 planned later targets unattempted, not additional failures. After the first target, 520/587 attempted targets were reached (88.6%). Recorded 587 arrival handoffs; every handoff preserved player position and the previous action exactly, and all 587 next actions received nonzero movement history.

Successful chains averaged **15.55 game seconds** (median 15.70; p90 18.78). Mean attempt duration including 60-second timeouts was **45.33 seconds**. Reference controls completed **100/100 identical chains / 1,000 targets**, mean **12.27 seconds**, maximum **16.20 seconds**. Reference cases are verification, excluded from policy outcomes.

The 67 failed legs each had **40.3-58.1 seconds** available on the same fixed target, mean **52.42 seconds**. This is not merely a tight combined deadline. Across those failed legs, **16,974/35,122 commands (48.3%)** pointed away from or perpendicular to the assigned direction (zero commands count here). Mean per-failed-leg command alignment was 0.011, where +1 means directly toward, zero perpendicular and -1 away. The measure concerns commands, not collision-free displacement.

For example, seed 288000 reached the first three targets in 2.4 seconds, then spent 57.6 seconds on target four: initial distance 5.95, closest 2.99, final 5.88 units; 302/576 commands were away/perpendicular. This demonstrates sustained poor tracking after earlier successful arrivals, without any navigator replacement or hidden goal.

## Protocol and limits

- **Model:** QA-39, SHA-256 `a90f3a238b4d6e6b236db357467edafd97b89c5b2dc57bccba50263c72102d2f`, 72,054 model steps / 2,800 optimizer updates / 7,500 actor-lineage episodes. No training, reward-driven updates or checkpoint promotion. Tensors, counters and checkpoint remained unchanged.
- **Cases:** seeds 288000-288099, 100 chains of ten targets, deterministic CPU inference, existing empty 40 x 40 room, centre spawn at chain start, 12-unit vision and 0.5-unit arrival tolerance.
- **Targets:** precomputed bounded random walks independent of policy actions. Uniform angles and nominal radii 1.5-9.5 units, rejecting points outside +/-15 X/Z. The half-unit arrival tolerance guarantees actual next assignments in 1-10 units. Observed assignment range **1.16694-9.52252 units**. The same complete coordinate sequences were verified for reference and policy.
- **Handoff:** arrival immediately selects the next target. No player, orientation, previous-action, sensor or metric reset between targets; no target replacement, abandonment or recovery. Only the initial chain reset clears movement history. Targets remain fixed until arrival or timeout.
- **Budget:** 60 game seconds / 600 actions per chain, 0.1 second per action, time scale 100. Zero diagnostic reward; no learning. Exit disabled and never sensed. The source scene, player controller and observation mapping are unchanged.

The first-leg 100/100 versus full-chain 33/100 shows why isolated arrivals overstate sequence reliability. This establishes difficulty under continued waypoint following; it does **not** prove that the act of switching targets or previous-action inputs alone causes failures. Later targets also have different positions, ray observations and approaches. No matched reset-history or isolated-position control was run. Generated dungeons, barriers and combat remain outside this test.

## Evidence and reproduction

[Summary](result.json), [analysis and all 67 failed-leg summaries](analysis.json), [100 chain outcomes](episodes.jsonl), [per-leg records](legs.jsonl), [predefined target coordinates](chains.jsonl), [complete policy observations/actions](actions.jsonl.gz), [reference outcomes](reference-episodes.jsonl), [reference legs](reference-legs.jsonl), [reference coordinates](reference-chains.jsonl), [reference observations/actions](reference-actions.jsonl.gz), [run settings/source hashes](run.json), [completion checks](verification.json), [analysis script](analyze.py), [source snapshot](source/Assets/Editor/QaWaypointChainSession.cs).

[QA-39 provenance](../../qa-waypoint-reach-20261108-145257-train/README.md), [maintained runner](../../../../../Training/test_waypoint_chain.py), [change history](../../../../../Training/changelog/qa-player.md).

From the game directory, with other Unity sessions closed:

```powershell
python -B Training/test_waypoint_chain.py
```

That command launches a fresh frozen reference/policy evaluation, not training. To reanalyze retained data only:

```powershell
python -B SavedModels/qa/local-waypoint-v1/diagnostics/waypoint-chain-20261108-182109/analyze.py
```

Local runtime: `Logs/qa-waypoint-chain/20261108-182109/`. Unity exited normally; settings restored and all experiment sessions stopped.
