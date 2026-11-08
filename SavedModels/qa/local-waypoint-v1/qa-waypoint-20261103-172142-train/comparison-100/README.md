# QA-21: 100 matched frozen cases per condition

Evaluation follow-up only. No new training, optimizer updates or stage advancement. The room remains open 40 x 40, exits start unseen 30-34 units away, vision is 12 units and episodes last at most 30 seconds.

"Before" means QA-19, already trained on the 20 x 20 waypoint task, evaluated here in the larger room. "After" means QA-21 following its 500 additional 40 x 40 training episodes. Both checkpoints were trained with waypoint inputs and rewards. Masking zeros only inputs 130-133 during deterministic prediction; rays and previous movement remain. It does not represent a separately trained policy without waypoints.

## Results

| Checkpoint | Waypoints enabled | Waypoints masked |
| --- | --- | --- |
| Before larger-room training (QA-19) | **63/100** | **25/100** |
| After 500 larger-room episodes (QA-21) | **61/100** | **56/100** |

All failures were timeouts; no deaths occurred. The same 100 seeds, **250000-250099**, have exactly matching spawn and exit coordinates across all four conditions.

| Paired comparison (A minus B) | Clear-rate difference | Paired 95% interval | A-only / B-only clears | Exact paired p |
| --- | --- | --- | --- | --- |
| After vs before, waypoint inputs enabled | -2 points | -14 to +10 points | 18 / 20 | 0.871 |
| Enabled vs masked, after training | +5 points | -8 to +18 points | 26 / 21 | 0.56 |
| Enabled vs masked, before training | +38 points | +25 to +51 points | 48 / 10 | 4.52e-07 |
| After vs before, waypoint inputs masked | +31 points | +21 to +41 points | 33 / 2 | 3.67e-08 |

The larger-room training changed the enabled score from **63/100 to 61/100**. Waypoint inputs changed the trained model's score from **56/100 masked to 61/100 enabled**. Both paired intervals include zero: these cases establish neither a training improvement/decline nor a clear waypoint benefit/harm for the trained model. They do not establish waypoint dependence.

Before the larger-room continuation, waypoint inputs raised QA-19's score from **25/100 masked to 63/100 enabled**, a +38-point difference (paired 95% interval +25 to +51). The continuation improved the masked score by +31 points (+21 to +41) while leaving enabled performance similar. That measures a change in behavior under input masking; trajectories would be needed to explain the policy's decision mechanism.

The original ten-case prefix showed **4 -> 7** enabled clears before/after training, and **7 enabled vs 9 masked** after training. Both apparent directions change in the full set. Keep that prefix as historical evidence; use these 100-case results for the current assessment. Increasing the room dimensions requires several handoffs for the reference controller but does not make assigned-waypoint inputs physically necessary for this learned policy.

Intervals are percentile bootstraps of the 100 paired binary outcome differences (20,000 resamples, RNG seed 250000). The exact two-sided test uses only discordant pairs. These are case-sampling intervals, without multiple-comparison adjustment; they do not cover training-seed variability or other room geometries. Deterministic actions remove action-sampling variability. Successful-exit times describe different successful subsets and are secondary to the clear-rate comparison.

## Evidence and preservation

[Full comparison and verification](comparison.json), [evaluation plan](evaluation-plan.json), [case configurations](configs/), [original and extension metadata](segments/).

| Condition | Summary | 100 per-seed records |
| --- | --- | --- |
| after enabled | [JSON](after_enabled.json) | [JSONL](after_enabled.jsonl) |
| after masked | [JSON](after_masked.json) | [JSONL](after_masked.jsonl) |
| before enabled | [JSON](before_enabled.json) | [JSONL](before_enabled.jsonl) |
| before masked | [JSON](before_masked.json) | [JSONL](before_masked.jsonl) |

Reused the verified 100 after-enabled episodes and the ten-case prefix in each other condition: **130 existing rollouts plus 270 new rollouts**. Each extension covers only seeds 250010-250099, so each condition contains 100 unique cases without duplicates. Canonical records retain source-segment and original episode identifiers; archived segment records, summaries, configs and source/model provenance remain unchanged snapshots. Absolute paths in historical snapshots describe the original local run; the canonical records and segment copies here keep the evidence with this workflow.

Before checkpoint SHA-256: `67c50b7f30f38eb7194979f4e63ec836c4ee1e45fecc7f2a89961f84631a03cd`.
After checkpoint SHA-256: `de32a90630b506d97610a918cadea89039518720d0ae2e96c1af58a231e1fe6b`.

Same scene/runner/navigation/policy source hashes across all segments. All evaluations reported zero weight updates; the maintained runner asserted unchanged policy tensors and checkpoint files. Final checks verified both checkpoint hashes, matching placements, hidden initial exits, episode limits, waypoint bonus caps, clean extension shutdowns and restored project settings. Training and evaluation are stopped. No checkpoint was overwritten and no subsequent stage started.
