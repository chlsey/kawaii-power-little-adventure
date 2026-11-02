# Training change logs

Record what changed, why it was tried, and what happened for each trained agent. Keep the history with the game-owned training configs and model artifacts.

The branch's [training-changelog skill](../../../../skills/training-changelog/SKILL.md) maintains these histories during training changes, run launches, and result/status checks.

| Agent | History |
| --- | --- |
| QA player | [QA player change log](qa-player.md): interfaces, sensors, rewards, task difficulty, training settings, and measured outcomes. |
| PCG | [PCG change log](pcg.md): generation-control policies and their evaluations. |

## Recording an iteration

Use one entry per meaningful experiment or implementation change. Extend an existing entry when its evaluation finishes. Give a new run a separate entry when its settings, starting checkpoint, task, or training budget change.

```text
ID / date:
Change -> result:
Starting point: fresh policy or exact parent checkpoint/run.
Settings: environment, input/output contract, reward, learning rate,
          entropy, episode limit, training seeds, and training budget.
Training result: completed episodes, successes, timeouts, and steps.
Evaluation result: frozen policy, held-out seeds, outcomes, and limits.
Interpretation / next decision: measured finding versus hypothesis.
Evidence: config, result summary, checkpoint, and relevant source/guide.
```

- Label entries as implemented, completed, halted, verification-only, or proposed. A proposed change has no measured result until it is run.
- Separate training success from frozen-policy evaluation. Do not treat mean reward or pipeline verification as task success.
- Record checkpoint lineage. A fresh model with a new contract starts a new lineage; separate continuations from restarts at an older checkpoint.
- Compare runs with the same evaluation conditions where possible. Changing several settings at once does not establish which change caused an outcome.
- Record an interrupted campaign once, with its recovery segments beneath it; do not add recovered episodes twice.
- Preserve results and settings in these Markdown entries. Evidence under `Logs/` is local and ignored by Git; saved-model summaries and model guides provide additional durable provenance.

Use [the training guide](../README.md) for commands and [the player/PCG design](../PCG-system-design.md) for architecture and planned workflows.
