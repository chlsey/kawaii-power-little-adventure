"""Dedicated waypoint skill training; defaults to verification, never advances stages."""

import argparse
from datetime import datetime, timezone
import json
from pathlib import Path
import shutil
import socket
import subprocess
import time

import numpy as np
import torch
from stable_baselines3 import PPO
from stable_baselines3.common.callbacks import CallbackList, CheckpointCallback, StopTrainingOnMaxEpisodes
from stable_baselines3.common.logger import configure
from stable_baselines3.common.monitor import Monitor
from stable_baselines3.common.vec_env import DummyVecEnv

from run_local_movement import GAME, checkpoint_hash, load_config, prepare_waypoint_transfer, save, save_checkpoint_manifest, wait_record
from test_waypoint_reach import UNITY
from test_waypoint_reach_batch import summary
from unity_env import UnityGymEnv


def launch(directory, episodes, mode, distance_range, verify=False):
    directory.mkdir(parents=True)
    save(directory / 'session.json', dict(mode=mode, episodes=episodes, verify=verify,
        min_distance=distance_range[0], max_distance=distance_range[1]))
    process = subprocess.Popen([str(UNITY), '-batchmode', '-nographics', '-projectPath', str(GAME),
        '-executeMethod', 'QaWaypointReachTrainingSession.Run', '-trainingRunDirectory', str(directory),
        '-logFile', str(directory / 'unity.log')], cwd=GAME, creationflags=subprocess.CREATE_NO_WINDOW)
    save(directory / 'process.json', dict(unity_pid=process.pid))
    return process


class RecordedEnvironment(UnityGymEnv):
    def __init__(self, directory, process, config, count, seed, progress):
        super().__init__(port=config['port'], timeout_seconds=config['socket_timeout_seconds'])
        self._socket.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
        self.directory, self.process, self.config = directory, process, config
        self.count, self.seed, self.progress = count, seed, progress
        self.records = []
        self.terminal = None

    def reset(self, *, seed=None, options=None):
        # DummyVecEnv's terminal reset must not start an unauthorized 501st episode.
        if len(self.records) == self.count: return self.terminal.copy(), {}
        self.steps = 0
        self.return_total = 0.
        observation, info = super().reset(seed=self.seed + len(self.records))
        assert observation[130] == 1 and 0 < observation[133] <= min(1, self.config['waypoint_distance_range'][1] / 12) + .00001
        return observation, info

    def step(self, action):
        observation, reward, terminated, truncated, info = super().step(action)
        self.steps += 1
        self.return_total += reward
        assert np.all(observation[3:130:2] == 0) and np.all(np.abs(observation) <= 1.00001)
        if terminated or truncated:
            row = wait_record(self.directory, f'episode-{len(self.records)}.json', self.process, 30)
            assert row['seed'] == self.seed + len(self.records) and row['steps'] == self.steps
            low, high = self.config['waypoint_distance_range']
            assert low - .00001 <= row['initial_distance'] <= high + .00001 and row['elapsed_seconds'] <= 60.0001
            assert not row['game_completed'] and not row['player_died']
            assert row['positive_waypoint_reward'] <= 5.00001
            assert row['reached'] == (row['final_distance'] <= .5)
            assert abs(self.return_total - (row['positive_waypoint_reward'] - .001 * self.steps)) < .0001
            row['return'] = self.return_total
            self.records.append(row)
            self.terminal = observation
            if len(self.records) % 10 == 0 or len(self.records) == self.count:
                result = summary(self.records)
                save(self.progress, dict(status=self.directory.name, target_episodes=self.count, **result))
                print(f'{self.directory.name}: {len(self.records)}/{self.count}, {result["successes"]} arrivals', flush=True)
        return observation, reward, terminated, truncated, info


# nearby assigned targets -> verify drill/reward hierarchy -> train exact budget -> frozen evaluation
def run(config_path, mode):
    config = load_config(config_path)
    distance_range = config['waypoint_distance_range']
    assert .5 < distance_range[0] < distance_range[1] <= 18 and config['max_episodes'] == 500
    assert config['waypoint_reward_budget'] == 5 < config['minimum_local_objective_reward'] == 50
    assert config['episode_seconds'] == 60 and config['observation_contract'] == 'local-waypoint-v1'
    directory = (config_path.parent / config['output_dir']).resolve() / (datetime.now(timezone.utc).strftime('%Y%m%d-%H%M%S') + '-' + mode)
    directory.mkdir(parents=True)
    print(f'Run directory: {directory}', flush=True)
    shutil.copy2(config_path, directory / 'training-config.yaml')
    settings = {path: path.read_bytes() for path in (GAME / 'ProjectSettings').iterdir() if path.is_file()}
    actor_transfer = 'transfer_actor_from' in config
    if actor_transfer:
        parent = (config_path.parent / config['transfer_actor_from']).resolve()
        parent_hash = config['transfer_parent_sha256']
    else:
        parent = (config_path.parent / config['resume_from']).resolve()
        parent_hash = config['resume_sha256']
    assert checkpoint_hash(parent) == parent_hash
    process = environment = vector = None
    try:
        if not (GAME / config['unity_scene']).exists():
            process = subprocess.Popen([str(UNITY), '-batchmode', '-nographics', '-projectPath', str(GAME),
                '-executeMethod', 'QaWaypointReachTrainingSession.Prepare', '-logFile', str(directory / 'prepare.log')],
                cwd=GAME, creationflags=subprocess.CREATE_NO_WINDOW)
            process.wait(timeout=300)
            assert process.returncode == 0 and (GAME / config['unity_scene']).exists(), 'Scene preparation failed; see prepare.log'
        sources = [config['unity_scene'], 'Assets/Editor/QaWaypointReachTrainingSession.cs',
            'Assets/PCGDungeon/Scripts/RL/QaWaypointReachRlEnvironment.cs', 'Assets/PCGDungeon/Scripts/QA/QaNavigationCoordinator.cs',
            'Assets/PCGDungeon/Scripts/QA/ModelSensorPackage.cs', 'Assets/PCGDungeon/Scripts/QA/QaPlayerController.cs',
            'Assets/PCGDungeon/Scripts/Policy/QaWaypointPolicyMapping.cs', 'Assets/PCGDungeon/Scripts/Policy/QaLocalMovementPolicyMapping.cs',
            'Training/run_waypoint_reach.py', 'Training/run_local_movement.py']
        source_hashes = {name: checkpoint_hash(GAME / name) for name in sources}
        save(directory / 'run.json', dict(task=config.get('task', 'waypoint-reach-nearby'), observation_contract='local-waypoint-v1',
            parent_checkpoint=str(parent), parent_checkpoint_sha256=parent_hash,
            parent_lineage_episodes=config['parent_lineage_episodes'], actor_transfer=actor_transfer,
            fresh_critic=actor_transfer, fresh_optimizer=actor_transfer,
            training_seed_range=[config['seed'], config['seed'] + config['max_episodes'] - 1],
            evaluation_seed_range=[config['evaluation_seed'], config['evaluation_seed'] + config['evaluation_episodes'] - 1],
            waypoint_distance_range=distance_range, episode_seconds=60, waypoint_reward_budget=5,
            minimum_local_objective_reward=50, no_exit=True, no_automatic_stage_advancement=True,
            source_sha256=source_hashes))
        verify_dir = directory / 'verification'
        process = launch(verify_dir, 0, 'verify', distance_range, True)
        process.wait(timeout=300)
        assert process.returncode == 0 and (verify_dir / 'verification.json').exists(), 'See verification/unity.log'
        print('Waypoint drill preflight passed', flush=True)
        if mode == 'verify': return

        torch.set_num_threads(1)
        if actor_transfer:
            start = prepare_waypoint_transfer(config, config_path)
        else:
            start = parent
        model = PPO.load(start, device=config['device'], seed=config['seed'])
        assert model.observation_contract == config['observation_contract']
        assert model.learning_rate == config['learning_rate'] and model.gamma == config['gamma']
        assert model.ent_coef == config['ent_coef'] and model.n_steps == config['n_steps']
        assert model.batch_size == config['ppo']['batch_size']
        initial_model_steps = model.num_timesteps
        model.training_task = config.get('task', 'waypoint-reach-nearby')
        model.waypoint_reward_budget = config['waypoint_reward_budget']
        destination = GAME / 'SavedModels/qa/local-waypoint-v1' / ('qa-waypoint-reach-' + directory.name)
        destination.mkdir(parents=True)
        for name in ['training-config.yaml', 'run.json']:
            shutil.copy2(directory / name, destination / name)
        shutil.copy2(verify_dir / 'verification.json', destination / 'verification.json')
        for phase in ['training', 'evaluation']:
            training = phase == 'training'
            count = config['max_episodes'] if training else config['evaluation_episodes']
            seed = config['seed'] if training else config['evaluation_seed']
            phase_dir = directory / phase
            process = launch(phase_dir, count, phase, distance_range)
            deadline = time.monotonic() + 300
            while not (phase_dir / 'unity-ready.txt').exists():
                if (phase_dir / 'unity-failure.txt').exists(): raise RuntimeError((phase_dir / 'unity-failure.txt').read_text())
                if process.poll() is not None or time.monotonic() > deadline: raise RuntimeError(f'Unity startup failed: {phase_dir}')
                time.sleep(.1)
            contract = json.loads((phase_dir / 'policy-contract.json').read_text())
            assert contract == dict(observation_contract='local-waypoint-v1', min_distance=distance_range[0], max_distance=distance_range[1],
                episode_seconds=60, waypoint_budget=5, local_objective_floor=50)
            environment = RecordedEnvironment(phase_dir, process, config, count, seed, directory / 'progress.json')
            updates = model._n_updates
            before = {key: value.clone() for key, value in model.policy.state_dict().items()}
            if training:
                vector = DummyVecEnv([lambda: Monitor(environment)])
                model.set_env(vector)
                model.set_logger(configure(str(phase_dir), ['csv']))
                callbacks = [StopTrainingOnMaxEpisodes(count), CheckpointCallback(config['checkpoint_freq'], str(destination / 'checkpoints'))]
                model.learn(config['total_timesteps'], callback=CallbackList(callbacks), reset_num_timesteps=actor_transfer)
                model.save(destination / 'final_model.zip')
                save_checkpoint_manifest(destination, 'local-waypoint-v1')
            else:
                model.policy.set_training_mode(False)
                frozen_hash = checkpoint_hash(destination / 'final_model.zip')
                for _ in range(count):
                    observation, _ = environment.reset()
                    done = False
                    while not done:
                        action, _ = model.predict(observation, deterministic=True)
                        observation, _, terminated, truncated, _ = environment.step(action)
                        done = terminated or truncated
                assert model._n_updates == updates
                assert all(torch.equal(model.policy.state_dict()[key], value) for key, value in before.items())
                assert checkpoint_hash(destination / 'final_model.zip') == frozen_hash
            assert len(environment.records) == count
            result = dict(status='complete', phase=phase, **summary(environment.records),
                new_model_steps=model.num_timesteps, initial_model_steps=initial_model_steps,
                new_training_steps=model.num_timesteps - initial_model_steps,
                total_lineage_episodes=config['parent_lineage_episodes'] + config['max_episodes'],
                weight_updates=model._n_updates - updates, game_completions=0,
                max_positive_waypoint_reward=max(row['positive_waypoint_reward'] for row in environment.records))
            save(directory / (phase + '-result.json'), result)
            save(destination / (phase + '-result.json'), result)
            save(destination / (phase + '-episodes.json'), environment.records)
            if vector is not None:
                vector.close()
                vector = None
            else: environment.close()
            environment = None
            (phase_dir / 'session-finished.txt').write_text('Exact episode budget complete\n')
            process.wait(timeout=45)
            assert process.returncode == 0 and (phase_dir / 'unity-finished.txt').exists()
            print(json.dumps(result, indent=2), flush=True)
        save(directory / 'progress.json', dict(status='complete', training_episodes=500, evaluation_episodes=100))
        assert all(checkpoint_hash(GAME / name) == digest for name, digest in source_hashes.items())
    except BaseException as error:
        save(directory / 'failure.json', dict(error=str(error)))
        raise
    finally:
        if vector is not None: vector.close()
        elif environment is not None: environment.close()
        if process is not None and process.poll() is None:
            process.terminate()
            process.wait(timeout=30)
        for path, original in settings.items():
            if path.read_bytes() != original: path.write_bytes(original)
        assert checkpoint_hash(parent) == parent_hash


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--config', type=Path, default=GAME / 'Training/qa-player-waypoint-reach-nearby.yaml')
    parser.add_argument('--mode', choices=['verify', 'train'], default='verify')
    args = parser.parse_args()
    run(args.config.resolve(), args.mode)
