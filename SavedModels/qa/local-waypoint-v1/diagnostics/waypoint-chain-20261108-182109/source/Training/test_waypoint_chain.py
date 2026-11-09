"""Frozen QA-39: predefined ten-waypoint chains, without exploration or exits."""

from datetime import datetime, timezone
import gzip
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
from test_waypoint_reach import UNITY, save

CHECKPOINT = GAME / 'SavedModels/qa/local-waypoint-v1/qa-waypoint-reach-20261108-145257-train/final_model.zip'
DIGEST = 'a90f3a238b4d6e6b236db357467edafd97b89c5b2dc57bccba50263c72102d2f'


def rows(path):
    return [json.loads(line) for line in path.read_text().splitlines()]


def run():
    directory = GAME / 'Logs/qa-waypoint-chain' / datetime.now(timezone.utc).strftime('%Y%m%d-%H%M%S')
    directory.mkdir(parents=True)
    print(f'Run directory: {directory}', flush=True)
    assert hashlib.sha256(CHECKPOINT.read_bytes()).hexdigest() == DIGEST
    torch.set_num_threads(1)
    model = PPO.load(CHECKPOINT, device='cpu')
    model.policy.set_training_mode(False)
    assert model.observation_contract == 'local-waypoint-v1'
    weights = {key: value.clone() for key, value in model.policy.state_dict().items()}
    updates, timesteps = model._n_updates, model.num_timesteps
    settings = {path: path.read_bytes() for path in (GAME / 'ProjectSettings').iterdir() if path.is_file()}
    sources = ['Assets/Editor/QaWaypointChainSession.cs', 'Assets/Editor/QaWaypointChainSession.cs.meta',
               'Training/test_waypoint_chain.py', 'Assets/Scenes/QaWaypoint40Training.unity',
               'Assets/PCGDungeon/Scripts/Policy/QaWaypointPolicyMapping.cs',
               'Assets/PCGDungeon/Scripts/Policy/QaLocalMovementPolicyMapping.cs',
               'Assets/PCGDungeon/Scripts/QA/QaPlayerController.cs',
               'Assets/PCGDungeon/Scripts/QA/ModelSensorPackage.cs']
    save(directory / 'run.json', dict(checkpoint=str(CHECKPOINT), checkpoint_sha256=DIGEST,
         model='QA-39', mode='frozen deterministic evaluation', episodes=100, reference_episodes=100,
         seed_range=[288000, 288099], waypoints_per_chain=10, room_size=[40, 40],
         nominal_leg_range=[1.5, 9.5], assigned_distance_range=[1, 10], target_xz_bounds=[-15, 15],
         sampling='Predefined bounded random walk: System.Random seed, uniform angle and nominal radius; reject points outside bounds',
         spawn_xz=[0, 0], episode_seconds=60, max_actions=600, action_seconds=.1,
         acceptance_radius=.5, vision_range=12, exit_disabled=True, automatic_navigation=False,
         reset_between_waypoints=False, previous_action_preserved=True, reward=0,
         time_scale=100, initial_model_timesteps=timesteps, initial_optimizer_updates=updates,
         source_sha256={name: hashlib.sha256((GAME / name).read_bytes()).hexdigest() for name in sources}))
    for path, content in settings.items():
        target = directory / 'original-settings' / path.name
        target.parent.mkdir(exist_ok=True)
        target.write_bytes(content)
    process = subprocess.Popen([str(UNITY), '-batchmode', '-nographics', '-projectPath', str(GAME),
        '-executeMethod', 'QaWaypointChainSession.Run', '-trainingRunDirectory', str(directory),
        '-logFile', str(directory / 'unity.log')], cwd=GAME, creationflags=subprocess.CREATE_NO_WINDOW)
    save(directory / 'process.json', dict(unity_pid=process.pid))
    environment = None
    started = time.monotonic()
    try:
        deadline = time.monotonic() + 300
        while not (directory / 'unity-ready.txt').exists():
            if (directory / 'unity-failure.txt').exists():
                raise RuntimeError((directory / 'unity-failure.txt').read_text())
            if process.poll() is not None:
                raise RuntimeError(f'Unity exited {process.returncode}; see unity.log')
            if time.monotonic() > deadline:
                raise TimeoutError('Unity startup')
            time.sleep(.1)
        environment = UnityGymEnv(port=int((directory / 'unity-ready.txt').read_text()), timeout_seconds=300)
        environment._socket.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
        assert environment.observation_size == 134 and environment.action_size == 2

        # Reference controls verify every exact chain is physically achievable within the same deadline.
        for phase in ['reference', 'policy']:
            for episode in range(100):
                observation, _ = environment.reset(seed=288000 + episode)
                assert observation[0] == observation[1] == 0 and observation[130] == 1
                done, steps = False, 0
                while not done:
                    assert np.all(np.isfinite(observation)) and np.all(np.abs(observation) <= 1.00001)
                    assert np.all(observation[3:130:2] == 0)
                    if phase == 'reference':
                        action = observation[131:133]
                    else:
                        action, _ = model.predict(observation, deterministic=True)
                    observation, reward, terminated, truncated, _ = environment.step(action)
                    steps += 1
                    assert reward == 0 and steps <= 600
                    done = terminated or truncated
                records = rows(directory / 'episodes.jsonl')
                record = records[-1]
                assert record['seed'] == 288000 + episode and record['steps'] == steps
                if phase == 'reference':
                    assert record['reached'] and record['reached_waypoints'] == 10
                if (episode + 1) % 10 == 0:
                    current = records[-(episode + 1):]
                    progress = dict(phase=phase, completed=episode + 1, target=100,
                                    complete_chains=sum(r['reached'] for r in current),
                                    waypoint_arrivals=sum(r['reached_waypoints'] for r in current))
                    save(directory / 'progress.json', progress)
                    print(json.dumps(progress), flush=True)
            # Keep reference evidence separate from learned-policy outcomes and traces.
            if phase == 'reference':
                for name in ['episodes', 'legs', 'chains', 'actions']:
                    (directory / f'{name}.jsonl').rename(directory / f'reference-{name}.jsonl')

        assert model._n_updates == updates and model.num_timesteps == timesteps
        assert all(torch.equal(model.policy.state_dict()[key], value) for key, value in weights.items())
        assert hashlib.sha256(CHECKPOINT.read_bytes()).hexdigest() == DIGEST
        assert rows(directory / 'chains.jsonl') == rows(directory / 'reference-chains.jsonl')
        legs = rows(directory / 'legs.jsonl')
        assert all(.9999 <= row['initial_distance'] <= 10.0001 for row in legs)
        completed = sum(row['reached'] for row in records)
        times = [row['elapsed_seconds'] for row in records if row['reached']]
        result = dict(status='complete', completed_episodes=100, complete_chains=completed,
                      timeouts=100 - completed, waypoint_arrivals=sum(r['reached_waypoints'] for r in records),
                      attempted_waypoints=len(legs), planned_waypoints=1000,
                      chain_completion_rate=completed / 100,
                      successful_seconds_mean=float(np.mean(times)) if times else None,
                      successful_seconds_median=float(np.median(times)) if times else None,
                      mean_episode_seconds=float(np.mean([r['elapsed_seconds'] for r in records])),
                      reference_chains_completed=100, weight_updates=0, policy_tensors_unchanged=True,
                      checkpoint_unchanged=True, model_timesteps=timesteps, optimizer_updates=updates,
                      checkpoint_sha256=DIGEST, wall_seconds=time.monotonic() - started)
        environment.close()
        environment = None
        (directory / 'session-finished.txt').write_text('Reference 100 and policy 100 chains complete\n')
        process.wait(timeout=45)
        assert process.returncode == 0 and (directory / 'unity-finished.txt').exists()
        save(directory / 'result.json', result)
        save(directory / 'progress.json', result)
        destination = GAME / 'SavedModels/qa/local-waypoint-v1/diagnostics' / ('waypoint-chain-' + directory.name)
        destination.mkdir(parents=True)
        for path in directory.glob('*.json*'):
            if path.name.endswith('actions.jsonl'):
                with path.open('rb') as src, gzip.open(destination / (path.name + '.gz'), 'wb') as dst:
                    shutil.copyfileobj(src, dst)
            else:
                shutil.copy2(path, destination / path.name)
        for name in sources:
            target = destination / 'source' / name
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(GAME / name, target)
        print(json.dumps(result, indent=2), flush=True)
        print(f'Retained report data: {destination}', flush=True)
    except BaseException as error:
        save(directory / 'failure.json', dict(error=str(error)))
        raise
    finally:
        if environment is not None:
            environment.close()
        if process.poll() is None:
            process.terminate()
            process.wait(timeout=30)
        for path, original in settings.items():
            if path.read_bytes() != original:
                path.write_bytes(original)


if __name__ == '__main__':
    run()
