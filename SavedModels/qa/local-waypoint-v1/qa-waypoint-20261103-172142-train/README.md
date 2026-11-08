# QA-21: 40 x 40 waypoint continuation

The 500-episode larger-room continuation and its 100 frozen evaluations completed. The task is an open **40 x 40** room with initially unseen exits **30-34 units** away, **12-unit vision** and waypoints within **six units**. No obstacles or combat were added. [Config snapshot](training-config.yaml), [run/source provenance](run.json), [file hashes](checkpoints.json).

| Phase | Seeds | Clears | Timeouts / deaths | Requests / new steps | Mean successful exit |
| --- | --- | --- | --- | --- | --- |
| Training | 2500-2999 | 248/500 (49.6%) | 252 / 0 | 124,076 | 19.547 s |
| Frozen deterministic evaluation | 250000-250099 | 61/100 (61%) | 39 / 0 | 23,201 | 18.854 s |

[Training summary](training-result.json), [training episodes](training-episodes.jsonl), [evaluation summary](evaluation-result.json), [evaluation episodes](evaluation-episodes.jsonl). Training clears per hundred: **54, 52, 52, 46, 44**. Training earned 1,712 waypoint bonuses; evaluation earned 349. Exploration reward and sustained-stuck penalty were zero in both phases. Episode waypoint bonuses stayed within +5. Evaluation performed zero weight updates and preserved every policy tensor and the checkpoint file. Thirty-nine frozen timeouts remain; this does not establish reliable completion of the larger-room task.

## Checkpoints and lineage

Continue the full [QA-19 model, critic and optimizer](../qa-waypoint-20261103-163554-train/README.md), whose checkpoint SHA-256 is `67c50b7f30f38eb7194979f4e63ec836c4ee1e45fecc7f2a89961f84631a03cd`. Its 36,286 model steps and 2,500 prior lineage training episodes remain separate from these 124,076 new steps / 500 new episodes. This checkpoint has **160,362 cumulative waypoint-model steps** and **3,000 lineage training episodes**. It does not repeat the actor-only transfer or reset the critic/optimizer. The older QA-17 actor history remains in the parent lineage records.

Final SHA-256: `de32a90630b506d97610a918cadea89039518720d0ae2e96c1af58a231e1fe6b`. Contract stays **local-waypoint-v1**, **134 -> 64 -> 64 -> 2**, movement X/Y. Keep completion +50, unique waypoint arrival +1 capped at +5/episode, best approach +0.1/unit, time -0.001/action, existing stuck penalty and zero exploration reward. No extra retreat penalty. PPO stays learning rate 0.0002, entropy 0.01, gamma 0.99, rollout 256, batch 64, CPU, 30-second episodes.

| Checkpoint | Cumulative model steps | New steps in this stage |
| --- | --- | --- |
| [Snapshot](checkpoints/rl_model_46286_steps.zip) | 46,286 | 10,000 |
| [Snapshot](checkpoints/rl_model_56286_steps.zip) | 56,286 | 20,000 |
| [Snapshot](checkpoints/rl_model_66286_steps.zip) | 66,286 | 30,000 |
| [Snapshot](checkpoints/rl_model_76286_steps.zip) | 76,286 | 40,000 |
| [Snapshot](checkpoints/rl_model_86286_steps.zip) | 86,286 | 50,000 |
| [Snapshot](checkpoints/rl_model_96286_steps.zip) | 96,286 | 60,000 |
| [Snapshot](checkpoints/rl_model_106286_steps.zip) | 106,286 | 70,000 |
| [Snapshot](checkpoints/rl_model_116286_steps.zip) | 116,286 | 80,000 |
| [Snapshot](checkpoints/rl_model_126286_steps.zip) | 126,286 | 90,000 |
| [Snapshot](checkpoints/rl_model_136286_steps.zip) | 136,286 | 100,000 |
| [Snapshot](checkpoints/rl_model_146286_steps.zip) | 146,286 | 110,000 |
| [Snapshot](checkpoints/rl_model_156286_steps.zip) | 156,286 | 120,000 |
| [Final model](final_model.zip) | 160,362 | 124,076 |

All twelve intermediate snapshots and final weights are retained here, with hashes in `checkpoints.json`. Snapshots have not each received independent frozen evaluation; keep them as alternatives without labelling the latest or any intermediate checkpoint best by default.

## Navigation and waypoint dependence

[Reference verification](verification.json) passed all 600 configured reset placements and eight compass completions, plus observed-map gaps/corner blocking, fixed-barrier clearance, recovery, reset and reward checks. The observation-only reference controller cleared **10/10** episodes, made **185 handoffs**, and needed at least **five handoffs before exit discovery in every attempt**. Reference results are distinct from learned-policy performance.

The [QA-20 baseline comparison](../qa-waypoint-40x40-validation-20261103/README.md), before this training, measured **4/10 clears with waypoint inputs versus 2/10 with them masked**, on matched seeds/placements. The newly trained checkpoint cleared **7/10 with waypoints versus 9/10 with those inputs masked**, on the same seeds 250000-250009, exact spawn/exit placements, source revision and checkpoint. [Masked summary](waypoint-masked-result.json), [episodes](waypoint-masked-episodes.jsonl), [source/model provenance](waypoint-masked-run.json), [paired comparison](waypoint-comparison.json), [control config](frozen-control-config.yaml). Successful masked exits averaged 10.711 seconds; the enabled subset averaged 16.871 seconds. Both controls made zero weight updates. Six seeds cleared in both; one cleared only with waypoints, three only when masked.

**Primary assessment: [100 matched cases per condition](comparison-100/README.md).** Before larger-room training, QA-19 cleared **63/100 with waypoints**; QA-21 cleared **61/100** afterward, a -2-point difference (paired 95% interval -14 to +10). QA-21 cleared **56/100 with waypoint inputs masked**, giving the enabled signal a +5-point difference (-8 to +18). Both intervals include zero, so neither training improvement/decline nor waypoint benefit/harm is established. The ten-case prefix above is historical; its apparent directions change in the full set. Only inputs 130-133 are masked. Vision/previous movement remain available, and this is a frozen input ablation of models trained with waypoints. Waypoint dependence remains unestablished.

The [scene guide](../../../../Assets/Scenes/QaSingleRoomTraining.README.md#larger-room-waypoint-continuation) describes setup and the [QA changelog](../../../../Training/changelog/qa-player.md) owns experiment history. Generated-dungeon, obstacle-training, combat and standalone ONNX deployment remain deferred. A 20 x 20 regression evaluation of this child has not been run; keep QA-19 as the measured 20 x 20 baseline. At completion of this run, training and evaluation were stopped and no further run had started. The later QA-22 continuation is linked below; reliable waypoint dependence remains unresolved.

Subsequent authorized continuation: [QA-22](../qa-waypoint-20261104-011557-train/README.md) completed 500 further episodes, then 64/100 enabled versus 55/100 masked frozen clears. The original checkpoint and this run's results remain preserved.
