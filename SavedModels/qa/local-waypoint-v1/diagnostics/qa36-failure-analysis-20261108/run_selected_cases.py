from pathlib import Path
import subprocess, sys, json
HERE = Path(__file__).resolve().parent
GAME = HERE.parents[4]
seeds = [285098, 285228, 285304, 285076, 285363, 285455]
for seed in seeds:
    print(f"Tracing {seed}", flush=True)
    with (HERE / f"case-{seed}-stdout.log").open("w") as stdout, (HERE / f"case-{seed}-stderr.log").open("w") as stderr:
        result = subprocess.run([sys.executable, "-u", "-B", str(GAME / "Training/run_local_movement.py"), "--config", str(HERE / f"case-{seed}.yaml"), "--mode", "evaluate"], cwd=GAME, stdout=stdout, stderr=stderr, creationflags=subprocess.CREATE_NO_WINDOW)
    if result.returncode:
        raise RuntimeError(f"Replay {seed} failed; see its stderr log")
    print(f"Completed {seed}", flush=True)
print("All six traces complete", flush=True)
