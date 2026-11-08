# Nearby-waypoint drill: zero-step actor transfer

Copies QA-24's actor, action head and log standard deviation into the same 134 -> 64 -> 64 -> 2 architecture with a fresh critic and optimizer. Input semantics remain `local-waypoint-v1`. This starting checkpoint has zero new steps, episodes or updates; its parent's 5,000 episodes / 613,364 model steps remain historical lineage.

See [transfer provenance](transfer.json), [checkpoint hash](checkpoints.json), and [the completed first 500-episode nearby stage](../qa-waypoint-reach-20261107-034515-train/README.md). The parent's bytes are preserved. This checkpoint is the reproducible starting point, not the trained result.
