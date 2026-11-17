"""Continue a saved waypoint policy for the configured two-room episode budget, then evaluate it."""

import argparse
from datetime import datetime, timezone
import gzip
import json
from pathlib import Path
import shutil
import socket
import subprocess

from run_local_movement import GAME, checkpoint_hash, save, save_checkpoint_manifest, load_config
from evaluate_two_room_navigation import SOURCES, phase, summary
from run_waypoint_chain import ready, finish, retain, records, launch as launch_chain
from run_waypoint_throughput import ThroughputEnvironment, summary as throughput_summary
from test_waypoint_reach import UNITY
from unity_env import UnityGymEnv
import numpy as np
import torch
from stable_baselines3 import PPO
from stable_baselines3.common.callbacks import CallbackList, CheckpointCallback, StopTrainingOnMaxEpisodes, BaseCallback
from stable_baselines3.common.logger import configure
from stable_baselines3.common.monitor import Monitor
from stable_baselines3.common.vec_env import DummyVecEnv


class TwoRoomTrainingEnvironment(UnityGymEnv):
    def __init__(self, directory, count, seed):
        super().__init__(port=7784, timeout_seconds=300)
        self._socket.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
        self.directory, self.count, self.first_seed = directory, count, seed
        self.records = []
        self.terminal = None

    def reset(self, *, seed=None, options=None):
        # DummyVecEnv resets automatically before the stop callback sees the final episode.
        if len(self.records) == self.count:
            return self.terminal.copy(), {}
        observation, info = super().reset(seed=self.first_seed + len(self.records))
        self.steps, self.total_reward = 0, 0.
        assert observation[0] == observation[1] == 0 and observation[130] == 1
        return observation, info

    def step(self, action):
        observation, reward, terminated, truncated, info = super().step(action)
        self.steps += 1
        self.total_reward += reward
        assert self.steps <= 600 and np.all(np.isfinite(observation))
        assert np.all(np.abs(observation) <= 1.00001)
        if terminated or truncated:
            row = records(self.directory / 'episodes.jsonl')[-1]
            assert row['seed'] == self.first_seed + len(self.records) and row['steps'] == self.steps
            assert row['training_rewards'] and 0 <= row['positive_waypoint_reward'] <= 5.00001
            expected = row['positive_waypoint_reward'] - .001 * self.steps + 50 * row['completed']
            assert abs(expected - self.total_reward) < .0003
            assert abs(row['total_reward'] - self.total_reward) < .0005
            assert row['completed'] or self.steps == 600
            row['python_return'] = self.total_reward
            self.records.append(row)
            self.terminal = observation
            if len(self.records) % 10 == 0 or len(self.records) == self.count:
                progress = dict(phase=self.directory.name, target=self.count, **summary(self.records))
                save(self.directory.parent / 'progress.json', progress)
                print(json.dumps(progress), flush=True)
        return observation, reward, terminated, truncated, info


class LineageCallback(BaseCallback):
    def __init__(self, environment, parent_episodes):
        super().__init__()
        self.environment, self.parent_episodes = environment, parent_episodes

    def _on_step(self):
        self.model.completed_lineage_episodes = self.parent_episodes + len(self.environment.records)
        return True


def launch_training(directory, room_throughput=False, room_sequence=False):
    directory.mkdir()
    save(directory / 'session.json', dict(training_rewards=True, room_throughput=room_throughput, room_sequence=room_sequence))
    process = subprocess.Popen([str(UNITY), '-batchmode', '-nographics', '-projectPath', str(GAME),
        '-executeMethod', 'QaTwoRoomNavigationSession.Run', '-trainingRunDirectory', str(directory),
        '-logFile', str(directory / 'unity.log')], cwd=GAME, creationflags=subprocess.CREATE_NO_WINDOW)
    save(directory / 'process.json', dict(unity_pid=process.pid))
    return process


def frozen_phase(directory, name, seeds, model):
    before = {k: v.clone() for k, v in model.policy.state_dict().items()}
    counters = model.num_timesteps, model._n_updates
    model.policy.set_training_mode(False)
    rows = phase(directory, name, seeds, model=model)
    assert counters == (model.num_timesteps, model._n_updates)
    assert all(torch.equal(v, model.policy.state_dict()[k]) for k, v in before.items())
    return rows


def run(config_path, mode):
    config = load_config(config_path)
    episodes = config['max_episodes']
    assert type(episodes) is int and episodes > 0 and config['episode_seconds'] == 60
    assert config['total_timesteps'] == episodes * 600
    assert config['completion_reward'] == 50 and config['waypoint_reward_budget'] == 5 and config['time_cost_per_action'] == .001
    assert config['room_count'] == 2 and config['room_size'] == [20, 20] and config['area_size'] == [100, 100]
    assert config['max_waypoint_distance'] == 10 and config['port'] == 7784
    parent = (config_path.parent / config['resume_from']).resolve()
    assert checkpoint_hash(parent) == config['resume_sha256']
    torch.set_num_threads(1)
    model = PPO.load(parent, device='cpu', seed=config['seed'])
    assert model.observation_contract == config['observation_contract']
    assert model.learning_rate == config['learning_rate'] and model.gamma == config['gamma']
    assert model.ent_coef == config['ent_coef'] and model.n_steps == config['n_steps'] and model.batch_size == config['ppo']['batch_size']
    initial_steps, initial_updates = model.num_timesteps, model._n_updates
    stamp = datetime.now(timezone.utc).strftime('%Y%m%d-%H%M%S') + '-' + mode
    directory = (config_path.parent / config['output_dir']).resolve() / stamp
    directory.mkdir(parents=True)
    destination = directory
    if mode == 'train':
        destination = GAME / 'SavedModels/qa/local-waypoint-v1' / ('qa-two-room-' + stamp)
        destination.mkdir()
    print('Run directory: ' + str(directory), flush=True)
    print('Retained directory: ' + str(destination), flush=True)
    sources = SOURCES + ['Training/train_two_room_navigation.py', 'Training/run_waypoint_throughput.py',
        'Training/run_waypoint_chain.py', 'Assets/Editor/QaWaypointChainSession.cs',
        'Assets/PCGDungeon/Scripts/QA/QaNavigationCoordinator.cs']
    hashes = {name: checkpoint_hash(GAME / name) for name in sources}
    shutil.copy2(config_path, destination / 'training-config.yaml')
    save(destination / 'run.json', dict(status='running', mode=mode, parent_checkpoint=str(parent),
        parent_sha256=config['resume_sha256'], parent_model_steps=initial_steps, parent_optimizer_updates=initial_updates,
        parent_lineage_episodes=config['parent_lineage_episodes'], new_training_episodes=episodes,
        training_seed_range=[config['seed'], config['seed'] + episodes - 1],
        evaluation_seed_range=[config['evaluation_seed'], config['evaluation_seed'] + config['evaluation_episodes'] - 1],
        observation_contract='local-waypoint-v1', full_actor_critic_optimizer_continuation=True,
        navigator_unchanged_from_qa45=True, reward='+50 actual exit, +0.1/new closest waypoint unit and +1/new waypoint arrival, shared +5 positive cap, -0.001/action',
        source_sha256=hashes))
    for name in sources:
        target = destination / 'source' / name
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(GAME / name, target)
    settings = {p: p.read_bytes() for p in (GAME / 'ProjectSettings').iterdir() if p.is_file()}
    for p, content in settings.items():
        target = directory / 'original-settings' / p.name
        target.parent.mkdir(exist_ok=True)
        target.write_bytes(content)
    process = environment = vector = None
    try:
        # Exercise reward/completion accounting with direct steering and one idle case, without learning.
        output = directory / 'reward-verification'
        process = launch_training(output)
        ready(output, process)
        environment = TwoRoomTrainingEnvironment(output, 4, 452000)
        for i in range(4):
            observation, _ = environment.reset()
            done = False
            while not done:
                action = observation[131:133] if i < 3 else np.zeros(2, dtype=np.float32)
                observation, _, terminal, truncated, _ = environment.step(action)
                done = terminal or truncated
        assert all(r['completed'] and r['total_reward'] > 49 for r in environment.records[:3])
        assert not environment.records[3]['completed'] and environment.records[3]['positive_waypoint_reward'] == 0
        save(output / 'result.json', dict(status='complete', **summary(environment.records)))
        environment.close(); environment = None
        finish(output, process)
        retain(output, destination)
        if mode == 'verify':
            return

        seeds = range(config['evaluation_seed'], config['evaluation_seed'] + config['evaluation_episodes'])
        before = frozen_phase(directory, 'parent-evaluation', seeds, model)
        if 'parent_evaluation_phase' in config:
            previous = records(parent.parent / config['parent_evaluation_phase'] / 'episodes.jsonl')
            assert len(previous) == len(before)
            assert all((a['seed'], a['completed'], a['steps']) == (b['seed'], b['completed'], b['steps'])
                for a, b in zip(previous, before)), 'Parent replay differs from retained evaluation'
        retain(directory / 'parent-evaluation', destination)
        output = directory / 'training'
        process = launch_training(output)
        ready(output, process)
        environment = TwoRoomTrainingEnvironment(output, episodes, config['seed'])
        vector = DummyVecEnv([lambda: Monitor(environment)])
        model.set_env(vector)
        model.set_logger(configure(str(output), ['csv']))
        model.training_task = 'two-room-oracle-exit-navigation'
        model.learn(config['total_timesteps'], reset_num_timesteps=False, callback=CallbackList([
            LineageCallback(environment, config['parent_lineage_episodes']),
            CheckpointCallback(config['checkpoint_freq'], str(destination / 'checkpoints')),
            StopTrainingOnMaxEpisodes(episodes)]))
        assert len(environment.records) == episodes
        assert model.num_timesteps - initial_steps == sum(r['steps'] for r in environment.records)
        assert len(records(output / 'layouts.jsonl')) == episodes
        model.completed_lineage_episodes = config['parent_lineage_episodes'] + episodes
        model.save(destination / 'final_model.zip')
        save_checkpoint_manifest(destination, 'local-waypoint-v1')
        training = summary(environment.records)
        training.update(mean_return=float(np.mean([r['python_return'] for r in environment.records])),
            max_positive_waypoint_reward=max(r['positive_waypoint_reward'] for r in environment.records),
            added_model_steps=model.num_timesteps - initial_steps, added_optimizer_updates=model._n_updates - initial_updates,
            discarded_partial_rollout_transitions=(model.num_timesteps - initial_steps) % model.n_steps)
        save(output / 'result.json', training)
        save(destination / 'training-result.json', training)
        vector.close(); vector = environment = None
        finish(output, process)
        retain(output, destination)
        after = frozen_phase(directory, 'child-evaluation', seeds, model)
        retain(directory / 'child-evaluation', destination)
        old_layouts = records(directory / 'parent-evaluation/layouts.jsonl')
        new_layouts = records(directory / 'child-evaluation/layouts.jsonl')
        assert old_layouts == new_layouts
        paired = dict(both_clear=sum(a['completed'] and b['completed'] for a, b in zip(before, after)),
            gained=sum(not a['completed'] and b['completed'] for a, b in zip(before, after)),
            lost=sum(a['completed'] and not b['completed'] for a, b in zip(before, after)),
            both_fail=sum(not a['completed'] and not b['completed'] for a, b in zip(before, after)))
        save(destination / 'exit-comparison.json', dict(parent=summary(before), child=summary(after), paired=paired,
            all_layouts_identical=True, evaluation_weight_updates=0))

        # Repeat the original empty-room timed benchmark to measure retention after the new stage.
        output = directory / 'empty-room-regression'
        process = launch_chain(output, dict(min_waypoints=10, max_waypoints=10), False, True, continuous=True)
        ready(output, process)
        environment = ThroughputEnvironment(output, {'port': 7783}, 100, 288000, False)
        counters = model.num_timesteps, model._n_updates
        weights = {k: v.clone() for k, v in model.policy.state_dict().items()}
        for _ in range(100):
            observation, _ = environment.reset()
            done = False
            while not done:
                action, _ = model.predict(observation, deterministic=True)
                observation, _, terminal, truncated, _ = environment.step(action)
                done = terminal or truncated
        assert counters == (model.num_timesteps, model._n_updates)
        assert all(torch.equal(v, model.policy.state_dict()[k]) for k, v in weights.items())
        throughput_phase = parent.parent / config.get('throughput_phase', 'evaluation')
        original = records(throughput_phase / 'episodes.jsonl')
        assert records(throughput_phase / 'chains.jsonl') == records(output / 'chains.jsonl')
        regression = dict(parent=throughput_summary(original), child=throughput_summary(environment.records),
            all_target_sequences_identical=True, weight_updates=0)
        save(destination / 'empty-room-comparison.json', regression)
        environment.close(); environment = None
        finish(output, process)
        retain(output, destination)
        assert checkpoint_hash(parent) == config['resume_sha256']
        assert all(checkpoint_hash(GAME / name) == digest for name, digest in hashes.items())
        save(destination / 'completion.json', dict(status='complete', training_episodes=episodes,
            training_steps=training['added_model_steps'], final_model_steps=model.num_timesteps,
            final_optimizer_updates=model._n_updates, final_lineage_episodes=model.completed_lineage_episodes,
            final_sha256=checkpoint_hash(destination / 'final_model.zip'), parent_unchanged=True,
            exit_comparison=dict(parent=summary(before), child=summary(after), paired=paired), empty_room=regression))
        metadata = json.loads((destination / 'run.json').read_text())
        metadata.update(status='complete', actual_training_episodes=episodes, completion_record='completion.json')
        save(destination / 'run.json', metadata)
        save(directory / 'progress.json', dict(status='complete', training_episodes=episodes))
        print(json.dumps(json.loads((destination / 'completion.json').read_text()), indent=2), flush=True)
        print('Completed run: ' + str(destination), flush=True)
    except BaseException as error:
        save(destination / 'failure.json', dict(error=str(error), final_model_exists=(destination / 'final_model.zip').exists()))
        raise
    finally:
        if vector is not None:
            vector.close()
        elif environment is not None:
            environment.close()
        if process is not None and process.poll() is None:
            process.terminate()
            process.wait(timeout=30)
        for p, content in settings.items():
            if p.read_bytes() != content:
                p.write_bytes(content)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--config', type=Path, required=True)
    parser.add_argument('--mode', choices=['verify', 'train'], default='verify')
    args = parser.parse_args()
    run(args.config.resolve(), args.mode)
