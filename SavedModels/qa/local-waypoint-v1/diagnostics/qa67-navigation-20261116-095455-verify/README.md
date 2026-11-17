# QA-67 boundary-recovery pilot

Complete four-case pilot on QA-66 seeds 530000-530003 with frozen QA-50. Reference steering clears 4/4; learned QA-50 clears 0/4. Missing guidance is 1,298/2,400 actions (54.08%); 177 instructions end in 83 coordinator arrivals, 93 interruptions and one episode-ended leg. This source includes actual-position clearance checks and physically swept escape from the initial wall-hit voxel, before the final bounds-jitter tolerance correction.

Final in-memory weights/counters, source/checkpoint hashes verified unchanged and settings restored. [Result](result.json), [audit](navigation-audit.json), [source manifest](campaign.json), [direct synthetic checks](direct-verification/verification.json). The later [full 100-case campaign](../qa67-navigation-20261116-100446-evaluate/README.md) owns the final result.
