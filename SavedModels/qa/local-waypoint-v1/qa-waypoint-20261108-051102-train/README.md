# QA-36: completed exit-navigation continuation and recovery

**Complete: 403/500 unique training clears (80.6%), followed by 332/500 frozen benchmark clears (66.4%).** QA-32 cleared 364/500 (72.8%) on the same placements. The child loses 32 clears / 6.4 percentage points. Preserve QA-32 as the preferred checkpoint for this benchmark; retain this child as an experiment without promotion. All sessions are stopped.

## Frozen comparison

| Measure | Parent QA-32 | Trained QA-36 |
| --- | ---: | ---: |
| Actual exit clears | 364/500 (72.8%) | 332/500 (66.4%) |
| Timeouts | 136 | 168 |
| Deaths | 0 | 0 |
| Mean episode time including timeouts | 31.09 s | 33.00 s |
| Mean successful clear time | 20.29 s | 19.33 s |
| Timeouts without discovering exit | 101 | 116 |
| Discovered-exit timeouts | 35 | 52 |

All times are game seconds. Successful-only means involve different subsets. Among the 274 cases both cleared, parent mean was 19.38 s and child mean 18.59 s; that does not offset fewer completed cases. Paired outcomes: **274 both clear, 58 child-only clears, 90 parent-only clears, 78 both timeout**. Completion change -6.4 points; paired case-bootstrap 95% interval **-11.2 to -1.6**, exact paired binomial p=0.01058. Bootstrap 20,000 resamples, RNG seed 36. This is exploratory uncertainty across reused cases from one trained continuation, not variability across independent training runs. Causes of the poorer completion rate, earlier-checkpoint performance and retention of the isolated waypoint skill were not tested here.

Evaluation uses deterministic frozen weights, seeds 285000-285499, with exact parent spawn/exit positions verified for all 500 cases. No optimizer updates during evaluation. The final checkpoint remains at **214,687 model steps / 8,370 optimizer updates / 7,500 completed actor-lineage episodes**, SHA-256 `7ca386b36041489e06e99e8f1d42612571adbe33f27191e4705e45c7cdd0e3e9`. Evaluation's 164,981 unique trainer requests are not training steps.

## Training and rollback accounting

The original [QA-36 segment](../qa-waypoint-20261108-032328-train/README.md) crashed after recording 110 completed attempts. Its last periodic snapshot retained 93,246 cumulative model steps / 3,630 optimizer updates, SHA-256 `30b40cb8b227de79ba3a90f201381dc29ebd1d46f8ddadb6cb7855ab59583142`.

- Retained prefix: 104 completed attempts, seeds 9000-9103, 87 clears and 17 timeouts, 29,899 completed-episode steps.
- Snapshot additionally counts 101 actions of the next incomplete attempt. Unity episode state and PPO's partial rollout buffer were not restored; recovery restarted seed 9104.
- Recovery: 396 attempts, seeds 9104-9499, 316 clears and 80 timeouts, 121,441 new model steps and 4,740 optimizer updates.
- Unique campaign: **104 + 396 = 500 attempts**, **403 clears, 97 timeouts, no deaths**. Original six completed suffix attempts 9104-9109 (four clears, two timeouts) are rolled-back history and excluded, rather than double-counted. Across physical execution there were 506 completed training attempts; only the defined 500 unique campaign attempts enter the campaign score.
- Completed-episode steps total 151,340. Model counters increased by 151,441 from QA-32's 63,246, including the retained 101 partial-attempt actions. Counters and episode sums are deliberately distinct; they do not establish bit-exact restoration of in-flight PPO experience.

The full QA-32 actor/critic/optimizer was continued; no fresh actor or optimizer. The [original QA-32](../qa-waypoint-reach-20261107-214655-train/README.md) and [separate backup](../qa32-backup-20261108-032207/BACKUP.md) remain byte-identical across all 12 original files. Recovery snapshots every 5,000 actions reduced potential crash loss. Twenty-four periodic child snapshots plus final model are retained and verified; no checkpoint was selected by evaluating all snapshots.

## Runtime interruption and targeted workaround

A second native Unity/Mono crash interrupted frozen evaluation after **321 cases, 210 clears and 111 timeouts**. The final trained model was already saved and remained unchanged. Both native crashes stopped in `GenericEqualityComparer<Vector2Int>.Equals` / `HashSet.Contains` inside the observed route planner.

The planner's three hash sets now use an explicit coordinate comparer, retaining Unity's hash function and coordinate equality. This avoids that generic comparer path; it is a runtime workaround hypothesis, not proof of the engine crash's root cause. No movement, reward, observation, goal-selection or route-order change was intended. Training used the earlier source; only resumed evaluation uses this workaround.

Validation and remaining evaluation:

1. The new evaluation-slice preflight passed 189 placements and map/clearance/recovery/reward checks but missed its 10/10 reference-completion requirement: **9/10 within the verifier's 300-action loop**. This [failed check](new-slice-preflight-failure.txt) is retained, not treated as a pass.
2. The established original preflight passed with the workaround: **1,000 planned placements, eight compass clears, route/recovery/reward checks and 10/10 reference clears with the same 157 handoffs**.
3. Replayed cases 285311-285320: all ten learned-policy outcomes, action counts, placements and final distances matched exactly.
4. Completed remaining seeds 285321-285499 without another crash. Combine the original 321 records with the 179 new records; exclude the ten overlapping verification cases. The recovery session physically ran 189 cases (128 clears, 61 timeouts), and adds 122 clears / 57 timeouts to the unique aggregate.

Thus the 500-case evaluation spans two source builds under the same policy and navigation contracts, with an exact ten-case overlap check. A full 500-case source-build equivalence experiment was not performed. Both interruption histories remain preserved separately. No automatic curriculum advancement, later training or PCG run followed.

## Task and unchanged learning settings

Isolated QA-32 waypoint training was continued into the integrated actual-exit task using the existing observed-map/frontier/A* navigator. Empty 40 x 40 room, initially unseen exit 30-34 units away, 12-unit rays, visible clear waypoints within six units, 60-second / 600-action deadline, playback 100. Actual exit +50, waypoint arrival +1 and new closest approach +0.1/unit sharing a +5 positive attempt cap, -0.001/action, existing stuck rule, no exploration reward. Same local-waypoint-v1 / 134 -> 64 -> 64 -> 2, PPO LR 0.0002, entropy 0.01, gamma 0.99, rollout 256, batch 64, CPU. No obstacles, enemies or competing mechanics. The documented timeout/termination/bootstrapping limitation remains unchanged.

## Evidence

- [Final checkpoint](final_model.zip), [all checkpoint hashes](checkpoints.json), [completion integrity checks](completion-verification.json).
- [Campaign training summary](campaign-training-result.json), [500 unique campaign training records](campaign-training-episodes.jsonl).
- [Recovery training result](training-result.json), [396 recovery training records](training-episodes.jsonl), [config](training-config.yaml), [run/source identities](run.json).
- [500-case frozen result](evaluation-result.json), [all unique frozen records](evaluation-episodes.jsonl), [paired comparison](paired-comparison.json).
- [First 321 frozen records](evaluation-prefix-episodes.jsonl), [evaluation interruption](evaluation-interruption.json), [native crash stack](evaluation-crash-excerpt.txt), [189-case recovery result](evaluation-recovery-result.json), [recovery records](evaluation-recovery-episodes.jsonl), [recovery source identities](evaluation-recovery-run.json), [recovery config](evaluation-recovery-config.yaml).
- [Ten-case overlap proof](evaluation-overlap-verification.json), [established preflight with workaround](runtime-workaround-verification.json), [workaround source snapshot](QaMapNavigation-workaround.cs.txt).
- [Parent benchmark](../diagnostics/distant-exit-20261107-220640-evaluate/README.md), [campaign history](../../../../Training/changelog/qa-player.md). Startup progress records are historical.

Local-only logs: training recovery `Logs/qa-waypoint-exit-qa36-recovery/20261108-051102-train/`; frozen recovery `Logs/qa-waypoint-exit-qa36-evaluation-recovery/20261108-062959-evaluate/`; failed new-slice preflight `Logs/qa-waypoint-exit-qa36-evaluation-recovery/20261108-062758-verify/`; successful established preflight `Logs/qa-waypoint-exit-from-qa32/20261108-062911-verify/`. Source-configured sessions have finished; these are provenance paths, not instructions to launch another run.
