"""Evaluate frozen QA-24 on 1,000 random fixed waypoints beyond its vision range."""

from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import shutil
import socket
import subprocess
import sys
import time

import numpy as np
import torch
from stable_baselines3 import PPO

GAME = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(GAME.parent / 'unity-reinforcement-learning/PythonEnv'))
from unity_env import UnityGymEnv
from test_waypoint_reach import CHECKPOINT, UNITY, save


def summary(records):
    times = [row['elapsed_seconds'] for row in records if row['reached']]
    return dict(completed_episodes=len(records), successes=len(times), timeouts=len(records) - len(times),
                success_rate=len(times) / len(records),
                successful_arrival_seconds=dict(mean=float(np.mean(times)), median=float(np.median(times)),
                    p90=float(np.percentile(times, 90)), minimum=min(times), maximum=max(times)) if times else None)


# empty room + frozen QA-24 -> 1,000 distant random targets -> record arrivals, times and unchanged weights
def run():
    directory = GAME / 'Logs/qa-waypoint-reach' / (datetime.now(timezone.utc).strftime('%Y%m%d-%H%M%S') + '-1000')
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
    source_names = ['Assets/Editor/QaWaypointReachBatchSession.cs', 'Training/test_waypoint_reach_batch.py',
                    'Training/test_waypoint_reach.py', 'Assets/Scenes/QaWaypoint40Training.unity',
                    'Assets/PCGDungeon/Scripts/Policy/QaLocalMovementPolicyMapping.cs',
                    'Assets/PCGDungeon/Scripts/Policy/QaWaypointPolicyMapping.cs',
                    'Assets/PCGDungeon/Scripts/QA/QaPlayerController.cs', 'Assets/PCGDungeon/Scripts/QA/ModelSensorPackage.cs']
    save(directory / 'run.json', dict(checkpoint=str(CHECKPOINT), checkpoint_sha256=digest,
         mode='frozen deterministic evaluation', episodes=1000, seed_range=[270000, 270999],
         room_size=[40, 40], spawn_xz=[0, 0], distance_range=[13, 18], vision_range=12,
         sampling='System.Random per seed; uniform angle [0,2pi), uniform radius [13,18)',
         episode_seconds=60, max_actions=600, action_seconds=.1, acceptance_radius=.5,
         exit_disabled=True, automatic_navigation=False, assigned_waypoint_signal_always_present=True,
         waypoint_distance_encoding='clamp01(distance / 12)', time_scale=100,
         initial_model_timesteps=timesteps, initial_optimizer_updates=updates,
         source_sha256={name: hashlib.sha256((GAME / name).read_bytes()).hexdigest() for name in source_names}))
    process = subprocess.Popen([str(UNITY), '-batchmode', '-nographics', '-projectPath', str(GAME),
        '-executeMethod', 'QaWaypointReachBatchSession.Run', '-trainingRunDirectory', str(directory),
        '-logFile', str(directory / 'unity.log')], cwd=GAME, creationflags=subprocess.CREATE_NO_WINDOW)
    save(directory / 'process.json', dict(unity_pid=process.pid))
    environment = None
    records = []
    started = time.monotonic()
    try:
        deadline = time.monotonic() + 300
        ready = directory / 'unity-ready.txt'
        while not ready.exists():
            failure = directory / 'unity-failure.txt'
            if failure.exists(): raise RuntimeError(failure.read_text())
            if process.poll() is not None: raise RuntimeError(f'Unity exited {process.returncode}; see unity.log')
            if time.monotonic() > deadline: raise TimeoutError('Unity startup')
            time.sleep(.1)
        environment = UnityGymEnv(port=int(ready.read_text()), timeout_seconds=120)
        environment._socket.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
        assert environment.observation_size == 134 and environment.action_size == 2
        for episode in range(1000):
            observation, _ = environment.reset(seed=270000 + episode)
            assert observation[130] == 1 and observation[133] == 1
            done = False
            steps = 0
            while not done:
                assert np.all(observation[3:130:2] == 0) and np.all(np.abs(observation) <= 1.00001)
                action, _ = model.predict(observation, deterministic=True)
                observation, reward, terminated, truncated, _ = environment.step(action)
                steps += 1
                assert reward == 0 and steps <= 600
                done = terminated or truncated
            row = json.loads((directory / 'episodes.jsonl').read_text().splitlines()[-1])
            assert row['seed'] == 270000 + episode and row['steps'] == steps
            assert 13 <= row['initial_distance'] <= 18 and row['elapsed_seconds'] <= 60.0001
            assert row['reached'] == (row['final_distance'] <= .5)
            if not row['reached']: assert steps == 600
            records.append(row)
            if len(records) % 10 == 0:
                progress = dict(status='running', target_episodes=1000, **summary(records))
                save(directory / 'progress.json', progress)
                print(f'{len(records)}/1000: {progress["successes"]} arrivals; {progress["timeouts"]} timeouts', flush=True)
        assert model._n_updates == updates and model.num_timesteps == timesteps
        assert all(torch.equal(model.policy.state_dict()[key], value) for key, value in weights.items())
        assert hashlib.sha256(CHECKPOINT.read_bytes()).hexdigest() == digest
        environment.close()
        environment = None
        (directory / 'session-finished.txt').write_text('1000 episodes complete\n')
        process.wait(timeout=45)
        assert process.returncode == 0 and (directory / 'unity-finished.txt').exists()
        result = dict(status='complete', **summary(records), weight_updates=0,
                      policy_tensors_unchanged=True, checkpoint_unchanged=True,
                      checkpoint_sha256=digest, wall_seconds=time.monotonic() - started)
        save(directory / 'result.json', result)
        save(directory / 'progress.json', result)
        destination = GAME / 'SavedModels/qa/local-waypoint-v1/diagnostics' / ('waypoint-reach-' + directory.name)
        destination.mkdir(parents=True)
        for name in ['run.json', 'result.json', 'episodes.jsonl']:
            shutil.copy2(directory / name, destination / name)
        print(json.dumps(result, indent=2), flush=True)
    except BaseException as error:
        save(directory / 'failure.json', dict(error=str(error), completed_episodes=len(records)))
        raise
    finally:
        if environment is not None: environment.close()
        if process.poll() is None:
            process.terminate()
            process.wait(timeout=30)
        for path, original in settings.items():
            if path.read_bytes() != original: path.write_bytes(original)


if __name__ == '__main__':
    run()
