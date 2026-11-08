"""Run one deterministic frozen-policy waypoint attempt in Unity, capped at 60 game seconds."""

from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import subprocess
import time

import numpy as np
import torch
from stable_baselines3 import PPO


GAME = Path(__file__).resolve().parents[1]
CHECKPOINT = GAME / 'SavedModels/qa/local-waypoint-v1/qa-waypoint-20261104-170721-train/final_model.zip'
UNITY = Path('C:/Program Files/Unity/Hub/Editor/6000.3.2f1/Editor/Unity.exe')


def save(path, value):
    temporary = path.with_suffix('.tmp')
    temporary.write_text(json.dumps(value, indent=2, allow_nan=False), encoding='utf-8')
    temporary.replace(path)


# empty room + frozen QA-24 -> follow one fixed waypoint -> check arrival and unchanged weights
def run():
    directory = GAME / 'Logs/qa-waypoint-reach' / datetime.now(timezone.utc).strftime('%Y%m%d-%H%M%S')
    directory.mkdir(parents=True)
    print(f'Run directory: {directory}', flush=True)
    digest = hashlib.sha256(CHECKPOINT.read_bytes()).hexdigest()
    assert digest == '78548d2ca50e90f61b737e9f74a6bf983c7682ea96c2e431b94cf71810076c36'
    torch.set_num_threads(1)
    model = PPO.load(CHECKPOINT, device='cpu')
    model.policy.set_training_mode(False)
    assert model.observation_contract == 'local-waypoint-v1'
    weights = {key: value.clone() for key, value in model.policy.state_dict().items()}
    updates, timesteps = model._n_updates, model.num_timesteps
    settings = {path: path.read_bytes() for path in (GAME / 'ProjectSettings').iterdir() if path.is_file()}
    sources = ['Assets/Editor/QaWaypointReachSession.cs', 'Training/test_waypoint_reach.py',
               'Assets/Scenes/QaWaypoint40Training.unity', 'Assets/PCGDungeon/Scripts/Policy/QaWaypointPolicyMapping.cs',
               'Assets/PCGDungeon/Scripts/Policy/QaLocalMovementPolicyMapping.cs',
               'Assets/PCGDungeon/Scripts/QA/QaPlayerController.cs', 'Assets/PCGDungeon/Scripts/QA/ModelSensorPackage.cs']
    save(directory / 'run.json', dict(checkpoint=str(CHECKPOINT), checkpoint_sha256=digest,
         mode='frozen deterministic evaluation', episodes=1, reset_seed=260000,
         episode_seconds=60, action_seconds=.1, max_actions=600, acceptance_radius=.5,
         waypoint_offset=[float(4 / np.sqrt(2)), float(4 / np.sqrt(2))],
         exit_disabled=True, automatic_navigation=False,
         initial_model_timesteps=timesteps, initial_optimizer_updates=updates,
         source_sha256={name: hashlib.sha256((GAME / name).read_bytes()).hexdigest() for name in sources}))
    process = subprocess.Popen([str(UNITY), '-batchmode', '-nographics', '-projectPath', str(GAME),
        '-executeMethod', 'QaWaypointReachSession.Run', '-trainingRunDirectory', str(directory),
        '-logFile', str(directory / 'unity.log')], cwd=GAME, creationflags=subprocess.CREATE_NO_WINDOW)
    save(directory / 'process.json', dict(unity_pid=process.pid))
    trace = []
    try:
        for step in range(601):
            path = directory / f'observation-{step}.json'
            deadline = time.monotonic() + (300 if step == 0 else 60)
            while not path.exists():
                failure = directory / 'unity-failure.txt'
                if failure.exists():
                    raise RuntimeError(failure.read_text())
                if process.poll() is not None:
                    raise RuntimeError(f'Unity exited {process.returncode}; see {directory / "unity.log"}')
                if time.monotonic() > deadline:
                    raise TimeoutError(f'Waiting for {path}')
                time.sleep(.01)
            frame = json.loads(path.read_text())
            assert frame['step'] == step and not frame['exit_visible']
            observation = np.asarray(frame['observation'], dtype=np.float32)
            assert observation.shape == (134,) and np.all(np.abs(observation) <= 1.00001)
            # Exit channels remain empty: only the assigned waypoint can identify the target.
            assert np.all(observation[3:130:2] == 0)
            trace.append(frame)
            if frame['done']:
                break
            action, _ = model.predict(observation, deterministic=True)
            frame['action'] = action.tolist()
            save(directory / f'action-{step}.json', dict(action=action.tolist()))
            if step % 100 == 0:
                print(f'{step / 10:.1f}s: {frame["distance"]:.3f} units from waypoint', flush=True)
        process.wait(timeout=45)
        assert process.returncode == 0
        assert model._n_updates == updates and model.num_timesteps == timesteps
        assert all(torch.equal(model.policy.state_dict()[key], value) for key, value in weights.items())
        assert hashlib.sha256(CHECKPOINT.read_bytes()).hexdigest() == digest
        save(directory / 'trace.json', trace)
        result = dict(status='complete', episodes=1, successes=int(frame['reached']),
                      elapsed_seconds=frame['elapsed_seconds'], steps=frame['step'],
                      initial_distance=trace[0]['distance'], final_distance=frame['distance'],
                      minimum_distance=min(item['distance'] for item in trace),
                      timeout=not frame['reached'], weight_updates=0, policy_tensors_unchanged=True,
                      checkpoint_unchanged=True, checkpoint_sha256=digest)
        save(directory / 'result.json', result)
        print(json.dumps(result, indent=2), flush=True)
    except BaseException as error:
        save(directory / 'failure.json', dict(error=str(error)))
        raise
    finally:
        if process.poll() is None:
            process.terminate()
            process.wait(timeout=30)
        for path, original in settings.items():
            if path.read_bytes() != original:
                path.write_bytes(original)


if __name__ == '__main__':
    run()
