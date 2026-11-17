# QA-70: 100 two-room tests of the QA-68 navigator

Complete frozen QA-50 evaluation on the same 100 generated layouts as QA-67: seeds 530000-530099, two 20 x 20 rooms and real corridors, 25 cases per quarter-turn, sixty seconds/600 actions per case, twelve-unit vision and ten-unit waypoint cap. Navigation uses observed sensor memory only.

| Metric | QA-67 | QA-70 |
| --- | --- | --- |
| Exits | 49 | 44 |
| Exit discoveries | 51 | 47 |
| Assignments | 5601 | 4885 |
| Assignments per 100 guided actions | 24.83 | 23.14 |
| Actions without waypoint (%) | 44.73 | 49.61 |
| Interrupted instructions | 2784 | 2320 |
| Broader recovery return states | 3462 | 2013 |
| Stuck windows | 504 | 581 |
| Actions | 40819 | 41900 |

Gained exits: [530001, 530002, 530016, 530042, 530043]. Lost exits: [530012, 530020, 530034, 530058, 530059, 530062, 530071, 530074, 530086, 530098].

All 57 existing direct assertions pass. Model file and in-memory weights/counters stayed unchanged; run-owned source hashes and canonical layout matches verified. Project settings restored. No training, deployment, commit or push. QA-50 remains selected at 656,793 steps / 25,610 optimizer epochs / 4,700 lineage episodes.

These reused diagnostic layouts do not establish independent generic-level performance. Recovery return states and stuck windows are diagnostic counts, not distinct failures. No new reference-controller replay is included in this campaign.

Evidence: [campaign](campaign.json), [results](result.json), [instruction audit](navigation-audit.json), [direct verification](direct-verification/verification.json). The retained [runner](run-pilot.py) is launched from `Logs/qa70-two-room/run.py`.
