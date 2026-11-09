"""Run the explicit 500-episode waypoint-chain curriculum; defaults to verification."""

import argparse
from datetime import datetime, timezone
import gzip
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

from run_local_movement import GAME, checkpoint_hash, load_config, save, save_checkpoint_manifest
from test_waypoint_reach import UNITY
from unity_env import UnityGymEnv


def records(path):
    return [json.loads(line) for line in path.read_text().splitlines()]


def summary(rows):
    times = [r['elapsed_seconds'] for r in rows if r['reached']]
    return dict(completed_episodes=len(rows), complete_chains=sum(r['reached'] for r in rows),
                timeouts=sum(not r['reached'] for r in rows),
                waypoint_arrivals=sum(r['reached_waypoints'] for r in rows),
                planned_waypoints=sum(r['planned_waypoints'] for r in rows),
                successful_seconds_mean=float(np.mean(times)) if times else None,
                mean_episode_seconds=float(np.mean([r['elapsed_seconds'] for r in rows])))


class RecordedEnvironment(UnityGymEnv):
    def __init__(self, directory, config, count, seed, stage, training_rewards):
        super().__init__(port=config['port'], timeout_seconds=config['socket_timeout_seconds'])
        self._socket.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
        self.directory, self.count, self.seed, self.stage = directory, count, seed, stage
        self.training_rewards = training_rewards
        self.records = []
        self.terminal = None

    def reset(self, *, seed=None, options=None):
        # DummyVecEnv auto-reset must not begin a chain beyond the authorized block.
        if len(self.records) == self.count:
            return self.terminal.copy(), {}
        self.steps, self.return_total = 0, 0.
        observation, info = super().reset(seed=self.seed + len(self.records))
        assert observation[0] == observation[1] == 0 and observation[130] == 1
        return observation, info

    def step(self, action):
        observation, reward, terminated, truncated, info = super().step(action)
        self.steps += 1
        self.return_total += reward
        assert self.steps <= 600 and np.all(np.isfinite(observation))
        assert np.all(observation[3:130:2] == 0) and np.all(np.abs(observation) <= 1.00001)
        if terminated or truncated:
            row = records(self.directory / 'episodes.jsonl')[-1]
            assert row['seed'] == self.seed + len(self.records) and row['steps'] == self.steps
            assert self.stage['min_waypoints'] <= row['planned_waypoints'] <= self.stage['max_waypoints']
            assert row['reached'] == (row['reached_waypoints'] == row['planned_waypoints'])
            assert row['positive_waypoint_reward'] <= 5.00001
            expected = row['positive_waypoint_reward'] - .001 * self.steps if self.training_rewards else 0
            assert abs(expected - self.return_total) < .0001
            row['return'] = self.return_total
            self.records.append(row)
            self.terminal = observation
            if len(self.records) % 10 == 0:
                result = dict(phase=self.directory.name, target=self.count, **summary(self.records))
                save(self.directory.parent / 'progress.json', result)
                print(json.dumps(result), flush=True)
        return observation, reward, terminated, truncated, info


def launch(directory, stage, training_rewards, trace_actions):
    directory.mkdir(parents=True)
    save(directory / 'session.json', dict(min_waypoints=stage['min_waypoints'],
        max_waypoints=stage['max_waypoints'], training_rewards=training_rewards, trace_actions=trace_actions))
    process = subprocess.Popen([str(UNITY), '-batchmode', '-nographics', '-projectPath', str(GAME),
        '-executeMethod', 'QaWaypointChainSession.Run', '-trainingRunDirectory', str(directory),
        '-logFile', str(directory / 'unity.log')], cwd=GAME, creationflags=subprocess.CREATE_NO_WINDOW)
    save(directory / 'process.json', dict(unity_pid=process.pid))
    return process


def ready(directory, process):
    deadline = time.monotonic() + 300
    while not (directory / 'unity-ready.txt').exists():
        if (directory / 'unity-failure.txt').exists():
            raise RuntimeError((directory / 'unity-failure.txt').read_text())
        if process.poll() is not None or time.monotonic() > deadline:
            raise RuntimeError(f'Unity startup failed: {directory}')
        time.sleep(.1)


def finish(directory, process):
    (directory / 'session-finished.txt').write_text('Exact chain budget complete\n')
    process.wait(timeout=45)
    assert process.returncode == 0 and (directory / 'unity-finished.txt').exists()


def retain(phase_dir, destination):
    output = destination / phase_dir.name
    output.mkdir(exist_ok=True)
    if output.resolve() == phase_dir.resolve():
        return
    for path in phase_dir.glob('*.json*'):
        if path.name == 'actions.jsonl':
            with path.open('rb') as src, gzip.open(output / (path.name + '.gz'), 'wb') as dst:
                shutil.copyfileobj(src, dst)
        else:
            shutil.copy2(path, output / path.name)


def run(config_path, mode):
    config = load_config(config_path)
    assert config['curriculum'] == [dict(episodes=100, min_waypoints=2, max_waypoints=2),
        dict(episodes=100, min_waypoints=3, max_waypoints=3), dict(episodes=100, min_waypoints=4, max_waypoints=4),
        dict(episodes=200, min_waypoints=1, max_waypoints=10)]
    assert config['max_episodes'] == 500 and config['episode_seconds'] == 60
    parent = (config_path.parent / config['resume_from']).resolve()
    assert checkpoint_hash(parent) == config['resume_sha256']
    parent_lineage = config['parent_lineage_episodes']
    directory = (config_path.parent / config['output_dir']).resolve() / (datetime.now(timezone.utc).strftime('%Y%m%d-%H%M%S') + '-' + mode)
    directory.mkdir(parents=True)
    print(f'Run directory: {directory}', flush=True)
    destination = GAME / 'SavedModels/qa/local-waypoint-v1' / ('qa-waypoint-chain-' + directory.name)
    if mode == 'train':
        destination.mkdir(parents=True)
    else:
        destination = directory
    shutil.copy2(config_path, directory / 'training-config.yaml')
    sources = [config['unity_scene'], 'Assets/Editor/QaWaypointChainSession.cs',
        'Assets/Editor/QaWaypointChainSession.cs.meta', 'Training/run_waypoint_chain.py',
        'Assets/PCGDungeon/Scripts/QA/QaNavigationCoordinator.cs', 'Assets/PCGDungeon/Scripts/QA/QaPlayerController.cs',
        'Assets/PCGDungeon/Scripts/QA/ModelSensorPackage.cs', 'Assets/PCGDungeon/Scripts/Policy/QaWaypointPolicyMapping.cs',
        'Assets/PCGDungeon/Scripts/Policy/QaLocalMovementPolicyMapping.cs']
    hashes = {name: checkpoint_hash(GAME / name) for name in sources}
    save(directory / 'run.json', dict(mode=mode, parent_checkpoint=str(parent), parent_sha256=config['resume_sha256'],
        parent_lineage_episodes=parent_lineage,
        training_seed_range=[config['seed'], config['seed'] + config['max_episodes'] - 1],
        evaluation_seed_range=[config['evaluation_seed'], config['evaluation_seed'] + config['evaluation_episodes'] - 1],
        full_optimizer_continuation=True, observation_contract='local-waypoint-v1', source_sha256=hashes,
        reward='(0.1 * new closest approach + 1 * arrival) / chain length; -0.001/action; total positive cap 5',
        reset_between_waypoints=False, trained_chain_lengths=config['curriculum']))
    if mode == 'train':
        for name in ['run.json', 'training-config.yaml']:
            shutil.copy2(directory / name, destination / name)
        for name in sources:
            target = destination / 'source' / name
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(GAME / name, target)
    settings = {path: path.read_bytes() for path in (GAME / 'ProjectSettings').iterdir() if path.is_file()}
    for path, content in settings.items():
        target = directory / 'original-settings' / path.name
        target.parent.mkdir(exist_ok=True)
        target.write_bytes(content)
    process = environment = vector = None
    try:
        # Reference random-length chains verify shaping, handoffs and every curriculum length.
        phase_dir = directory / 'verification'
        stage = dict(min_waypoints=1, max_waypoints=10)
        process = launch(phase_dir, stage, True, True)
        ready(phase_dir, process)
        environment = RecordedEnvironment(phase_dir, config, 100, 320000, stage, True)
        for _ in range(100):
            observation, _ = environment.reset()
            done = False
            while not done:
                observation, _, terminal, truncated, _ = environment.step(observation[131:133])
                done = terminal or truncated
        assert all(row['reached'] for row in environment.records)
        assert set(r['planned_waypoints'] for r in environment.records) == set(range(1, 11))
        save(phase_dir / 'result.json', dict(status='complete', **summary(environment.records)))
        environment.close()
        environment = None
        finish(phase_dir, process)
        retain(phase_dir, destination)
        if mode == 'verify':
            return

        torch.set_num_threads(1)
        model = PPO.load(parent, device=config['device'], seed=config['seed'])
        assert model.observation_contract == config['observation_contract']
        for attr in ['learning_rate', 'gamma', 'ent_coef', 'n_steps']:
            assert getattr(model, attr) == config[attr]
        assert model.batch_size == config['ppo']['batch_size']
        initial_steps, initial_updates = model.num_timesteps, model._n_updates
        model.training_task = config['task']
        model.waypoint_reward_budget = 5
        phases = [('parent-evaluation', dict(episodes=config['evaluation_episodes'], min_waypoints=10, max_waypoints=10))]
        phases += [(f'training-{i + 1}', stage) for i, stage in enumerate(config['curriculum'])]
        phases += [('evaluation', dict(episodes=config['evaluation_episodes'], min_waypoints=10, max_waypoints=10))]
        trained = 0
        all_training = []
        for name, stage in phases:
            training = name.startswith('training-')
            phase_dir = directory / name
            process = launch(phase_dir, stage, training, not training)
            ready(phase_dir, process)
            seed = config['seed'] + trained if training else config['evaluation_seed']
            environment = RecordedEnvironment(phase_dir, config, stage['episodes'], seed, stage, training)
            before = {key: value.clone() for key, value in model.policy.state_dict().items()}
            steps_before, updates_before = model.num_timesteps, model._n_updates
            if training:
                vector = DummyVecEnv([lambda: Monitor(environment)])
                model.set_env(vector)
                model.set_logger(configure(str(phase_dir), ['csv']))
                callbacks = CallbackList([StopTrainingOnMaxEpisodes(stage['episodes']),
                    CheckpointCallback(config['checkpoint_freq'], str(destination / 'checkpoints'), name_prefix=name)])
                model.learn(config['total_timesteps'], callback=callbacks, reset_num_timesteps=False)
                trained += len(environment.records)
                all_training.extend(environment.records)
                model.completed_lineage_episodes = config['parent_lineage_episodes'] + trained
                model.save(destination / f'{name}-model.zip')
                model.save(destination / 'final_model.zip')
                save_checkpoint_manifest(destination, 'local-waypoint-v1')
            else:
                model.policy.set_training_mode(False)
                for _ in range(stage['episodes']):
                    observation, _ = environment.reset()
                    done = False
                    while not done:
                        action, _ = model.predict(observation, deterministic=True)
                        observation, _, terminal, truncated, _ = environment.step(action)
                        done = terminal or truncated
                assert model.num_timesteps == steps_before and model._n_updates == updates_before
                assert all(torch.equal(model.policy.state_dict()[key], value) for key, value in before.items())
            assert len(environment.records) == stage['episodes']
            if name == 'parent-evaluation':
                historical = GAME / 'SavedModels/qa/local-waypoint-v1/diagnostics/waypoint-chain-20261108-182109'
                if 'parent_evaluation_dir' in config:
                    historical = (config_path.parent / config['parent_evaluation_dir']).resolve()
                expected = records(historical / 'episodes.jsonl')
                assert records(phase_dir / 'chains.jsonl') == records(historical / 'chains.jsonl')
                assert all(a['reached']==b['reached'] and a['steps']==b['steps'] and
                    a['reached_waypoints']==b['reached_waypoints'] for a,b in zip(environment.records,expected))
            result = dict(status='complete', phase=name, **summary(environment.records),
                model_steps=model.num_timesteps, added_model_steps=model.num_timesteps-steps_before,
                optimizer_updates=model._n_updates, added_optimizer_updates=model._n_updates-updates_before,
                completed_training_episodes=trained, total_lineage_episodes=parent_lineage+trained,
                frozen=not training)
            save(phase_dir / 'result.json', result)
            save(destination / f'{name}-result.json', result)
            if vector is not None:
                vector.close()
                vector = None
            else:
                environment.close()
            environment = None
            finish(phase_dir, process)
            retain(phase_dir, destination)
            print(json.dumps(result, indent=2), flush=True)
        assert trained == 500
        save(destination / 'training-episodes.json', all_training)
        save(destination / 'training-result.json', dict(status='complete', **summary(all_training),
            added_training_steps=model.num_timesteps-initial_steps, model_steps=model.num_timesteps,
            added_optimizer_updates=model._n_updates-initial_updates, optimizer_updates=model._n_updates,
            total_lineage_episodes=parent_lineage+trained, final_sha256=checkpoint_hash(destination/'final_model.zip')))
        assert all(checkpoint_hash(GAME / name) == digest for name, digest in hashes.items())
        save(directory / 'progress.json', dict(status='complete', training_episodes=trained,
            evaluation_episodes=config['evaluation_episodes']))
        print(f'Completed run: {destination}', flush=True)
    except BaseException as error:
        save(directory / 'failure.json', dict(error=str(error)))
        raise
    finally:
        if vector is not None:
            vector.close()
        elif environment is not None:
            environment.close()
        if process is not None and process.poll() is None:
            process.terminate()
            process.wait(timeout=30)
        for path, original in settings.items():
            if path.read_bytes() != original:
                path.write_bytes(original)
        assert checkpoint_hash(parent) == config['resume_sha256']


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--config', type=Path, default=GAME / 'Training/qa-player-waypoint-chain-curriculum.yaml')
    parser.add_argument('--mode', choices=['verify', 'train'], default='verify')
    args = parser.parse_args()
    run(args.config.resolve(), args.mode)
