# QA-44 restart: stage 2, single targets 6-8 units away

Complete: **500/500 training arrivals and 100/100 frozen arrivals**, mean frozen 1.745 game seconds. This is one fixed random target per episode, centre spawn in an empty 40 x 40 room, 0.5-unit arrival tolerance and at most 60 seconds / 600 actions. Arrival ends the exercise; it does not mark game completion. Inputs/actions and PPO settings retain local-waypoint-v1.

QA-17 is the last actor trained before assigned waypoints/navigation; QA-18 is its zero-step waypoint-compatible transfer with a fresh critic and optimizer. This independent branch starts from that transfer, excludes QA-19 through QA-43 training, and keeps the old QA-43 [full backup](../qa43-backup-20261108-231031/BACKUP.md). Later stages continue the preceding stage's complete optimizer state.

Cumulative branch steps **16,286**, completed actor-lineage episodes **3,000**. This stage adds 9,578 collected actions / 370 optimizer updates. Frozen evaluation adds zero updates. Reward +1 arrival / +0.1 new closest unit / -0.001 per action, combined positive limit five below objective floor fifty. Movement history resets only at episode start. A static-target result does not validate moving handoffs or resolve the previous-action hypothesis.

[Final checkpoint](final_model.zip), [checkpoint identities](checkpoints.json), [training result](training-result.json), [500 training cases](training-episodes.json), [frozen result](evaluation-result.json), [100 frozen cases](evaluation-episodes.json), [run-owned config](training-config.yaml), [parent/source identities](run.json), [preflight](verification.json), [campaign history](../../../../Training/changelog/qa-player.md). All checkpoint CRC/hash and source identities verified.
