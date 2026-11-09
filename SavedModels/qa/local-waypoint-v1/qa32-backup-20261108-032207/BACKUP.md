# Preserved QA-32 before exit-navigation training

Created for QA-36 at the user's request on 2026-11-08 UTC. All 12 files from [the original QA-32 run](../qa-waypoint-reach-20261107-214655-train/README.md) were copied and verified byte-identical. This includes the complete final policy/critic/optimizer checkpoint, both periodic checkpoints, run/config metadata and recorded training/evaluation results. The copied README describes the original QA-32 drill results.

Final checkpoint SHA-256: `0ca5d55955bf6ea7489e2495c1a1cbd9c3be773154981483096dd17d4b4479e0`. Starting model counter: 63,246 steps; optimizer counter: 2,460 updates; total actor lineage: 7,000 training episodes. Contract: `local-waypoint-v1`, 134 inputs, two movement outputs.

[Backup provenance and every copied file hash](backup.json). QA-36 reads this backup as its parent and writes all new checkpoints to [a separate run](../qa-waypoint-20261108-032328-train/README.md). Neither the original QA-32 nor this backup is an output target.
