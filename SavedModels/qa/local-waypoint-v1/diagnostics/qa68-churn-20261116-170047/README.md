# QA-68: retain physically clear instructions

Complete selected 12-case frozen QA-50 pilot. Retain an existing waypoint when the actual path stays observed/non-wall and the full normal-clearance capsule sweep passes, even if a crossed grid-cell centre is blocked. New route selection, collision margin and recovery safety remain unchanged.

| Metric | QA-67 matched subset | QA-68 |
| --- | --- | --- |
| Exits | 6/12 | 8/12 |
| Assignments | 1023 | 879 |
| Assignments per 100 guided actions | 26.96 | 27.30 |
| Interrupted instructions | 508 | 425 |
| Interruptions per 100 guided actions | 13.39 | 13.20 |
| Median instruction seconds | 0.2 | 0.2 |
| Missing guidance | 25.49% | 21.96% |
| Broader recovery returning states | 1259 | 989 |
| Stuck windows | 58 | 53 |

Two former failures complete (530001, 530002); no former success is lost. Raw assignments decrease 14.08%, but normalized assignment churn does not improve. The change improves task completion on this selected sample; it is not a sufficient churn fix. Seed 530000 changes from early missing guidance to repeated recovery, 34 -> 199 assignments without completion.

Existing five direct Unity scenarios pass all 57 assertions. All source hashes and in-memory/file weights are unchanged during evaluation, all canonical layouts match QA-67, settings restored, owned jobs ended. No learning, optimizer updates, checkpoint promotion or deployment. QA-50 remains 656,793 steps / 25,610 optimizer epochs / 4,700 lineage episodes.

The twelve seeds were selected in advance: four prefix failures, four high-churn cases and four successful orientation controls, three per quarter-turn. These are reused diagnostic layouts, not an independent or full 100-case result.

Evidence: [campaign](campaign.json), [result](result.json), [all instruction endings](navigation-audit.json), [matched baseline audit](baseline-subset/navigation-audit.json), [direct checks](direct-verification/verification.json). The retained [runner](run-pilot.py) is the exact script launched from `Logs/qa68-churn/run.py`; copy it back to that location before reproducing it.

Subsequent route-clearance experiment [QA-69](../qa69-churn-20261116-170939/README.md) reduced normalized assignments to 18.75 but regressed exits to 4/12 and missing guidance to 33.58%. It was rolled back; the current source matches this QA-68 snapshot exactly. [Follow-up integrity](followup-integrity.json).
