# QA-69: route-clearance preference rejected

Complete frozen QA-50 evaluation; **rejected and rolled back to the exact tested QA-68 source**. Add a one-cell A* cost to physically refined wall-rim cells and prefer route waypoints with a half-unit neighbourhood of observed planning clearance, with ordinary-clearance fallback. These two changes were tested together; their individual effects are not isolated.

Same twelve preselected diagnostic dungeons, three per orientation, as QA-68. Same actor, model, collision margin, goal selector, sensor range, waypoint cap, generator, deadlines and no training rewards. No new training.

| Measurement | QA-67 subset | QA-68 retained | QA-69 rejected |
| --- | --- | --- | --- |
| Exits | 6/12 | 8/12 | 4/12 |
| Assignments | 1023 | 879 | 682 |
| Assignments per 100 guided actions | 26.96 | 27.30 | 18.75 |
| Interrupted instructions | 508 | 425 | 316 |
| Missing guidance | 25.49% | 21.96% | 33.58% |
| Broader recovery returning states | 1259 | 989 | 653 |
| Stuck windows | 58 | 53 | 65 |
| Actions | 5092 | 4126 | 5476 |

Lower assignment churn did not mean better navigation: QA-69 loses four of QA-68's eight successes (530001, 530014, 530021, 530024), with none gained over QA-68. Compared with QA-67, it loses three successes and gains one. More time without guidance and more stuck windows make this an unsuitable tradeoff. Do not promote it or describe churn as solved. QA-68 improves completion but its normalized assignment rate remains essentially unchanged.

Both pilots pass the existing five direct Unity scenarios / 57 assertions each. File and in-memory checkpoint weights/counters stayed fixed; source snapshots and matched layouts verified, all instruction versions accounted for, compressed traces checked, settings restored and owned jobs ended. QA-50 remains selected, 656,793 steps / 25,610 optimizer epochs / 4,700 lineage episodes. All work remains local; no commit, push, deployment or curriculum advancement.

These selected, reused cases are not a full 100-case or independent generalization result. The retained source is QA-68; its next full matched evaluation remains unrun.

Evidence: [result](result.json), [campaign and rollback](campaign.json), [instruction audit](navigation-audit.json), [direct checks](direct-verification/verification.json), [integrity and current source](followup-integrity.json), [retained QA-68 report](../qa68-churn-20261116-170047/README.md). The [runner](run-pilot.py) was launched from `Logs/qa68-churn/run-clearance.py`; restore it to that path before reproducing.
