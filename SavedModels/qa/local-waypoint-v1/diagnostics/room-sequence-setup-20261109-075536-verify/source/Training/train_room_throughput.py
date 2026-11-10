"""Train exactly 300 full-minute episodes visiting as many distinct rooms as possible."""

import argparse
from datetime import datetime, timezone
import json
from pathlib import Path
import shutil
import socket
import subprocess

from run_local_movement import GAME, checkpoint_hash, save, save_checkpoint_manifest, load_config
from evaluate_two_room_navigation import SOURCES, phase as exit_phase, summary as exit_summary
from train_two_room_navigation import LineageCallback, launch_training
from run_waypoint_chain import ready, finish, retain, records, launch as launch_chain
from run_waypoint_throughput import ThroughputEnvironment, summary as waypoint_summary
from test_waypoint_reach import UNITY
from unity_env import UnityGymEnv
import numpy as np
import torch
from stable_baselines3 import PPO
from stable_baselines3.common.callbacks import CallbackList, CheckpointCallback, StopTrainingOnMaxEpisodes
from stable_baselines3.common.logger import configure
from stable_baselines3.common.monitor import Monitor
from stable_baselines3.common.vec_env import DummyVecEnv


def summary(rows):
    values = [r['new_rooms'] for r in rows]
    return dict(episodes=len(rows), total_new_rooms=sum(values), mean_new_rooms=float(np.mean(values)),
        median_new_rooms=float(np.median(values)), minimum_new_rooms=min(values), maximum_new_rooms=max(values),
        episodes_no_new_room=sum(v == 0 for v in values), episodes_all_available_rooms=sum(r['new_rooms'] == r.get('room_count', 8) - 1 for r in rows),
        actions=sum(r['steps'] for r in rows), mean_episode_seconds=float(np.mean([r['elapsed_seconds'] for r in rows])),
        no_waypoint_actions=sum(r['no_waypoint_actions'] for r in rows),
        low_movement_actions=sum(r['low_movement_actions'] for r in rows))


class RoomThroughputEnvironment(UnityGymEnv):
    def __init__(self, directory, count, seed, training_rewards, room_count=8):
        super().__init__(port=7784, timeout_seconds=300)
        self._socket.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
        self.directory, self.count, self.seed, self.training_rewards = directory, count, seed, training_rewards
        self.room_count = room_count
        self.records = []
        self.terminal = None

    def reset(self, *, seed=None, options=None):
        if len(self.records) == self.count:
            return self.terminal.copy(), {}
        observation, info = super().reset(seed=self.seed + len(self.records))
        self.steps, self.return_total = 0, 0.
        assert observation[0] == observation[1] == 0 and observation[130] == 1
        return observation, info

    def step(self, action):
        observation, reward, terminal, truncated, info = super().step(action)
        self.steps += 1; self.return_total += reward
        assert np.all(np.isfinite(observation)) and np.all(np.abs(observation) <= 1.00001)
        assert np.all(observation[3:130:2] == 0), 'Exit leaked into the room-throughput task'
        assert (terminal or truncated) == (self.steps == 600)
        if terminal or truncated:
            row = records(self.directory / 'episodes.jsonl')[-1]
            assert row['seed'] == self.seed + len(self.records) and row['steps'] == 600
            assert row['room_throughput'] and not row['completed'] and row['elapsed_seconds'] == 60
            assert row['training_rewards'] == self.training_rewards and 0 <= row['new_rooms'] < self.room_count
            assert row['positive_waypoint_reward'] <= 5.00001
            expected = 10 * row['new_rooms'] + row['positive_waypoint_reward'] - .6 if self.training_rewards else 0
            assert abs(expected - self.return_total) < .0005
            assert abs(row['total_reward'] - self.return_total) < .001
            row['python_return'] = self.return_total
            self.records.append(row); self.terminal = observation
            if len(self.records) % 10 == 0 or len(self.records) == self.count:
                progress = dict(phase=self.directory.name, target=self.count, **summary(self.records))
                save(self.directory.parent / 'progress.json', progress)
                print(json.dumps(progress), flush=True)
        return observation, reward, terminal, truncated, info


def launch(directory, rewards, sequence=False):
    if rewards:
        return launch_training(directory, room_throughput=True, room_sequence=sequence)
    directory.mkdir()
    save(directory / 'session.json', dict(training_rewards=False, room_throughput=True, room_sequence=sequence))
    process = subprocess.Popen([str(UNITY), '-batchmode', '-nographics', '-projectPath', str(GAME),
        '-executeMethod', 'QaTwoRoomNavigationSession.Run', '-trainingRunDirectory', str(directory),
        '-logFile', str(directory / 'unity.log')], cwd=GAME, creationflags=subprocess.CREATE_NO_WINDOW)
    save(directory / 'process.json', dict(unity_pid=process.pid))
    return process


def check_frozen(model, weights, counters):
    assert counters == (model.num_timesteps, model._n_updates)
    assert all(torch.equal(v, model.policy.state_dict()[k]) for k, v in weights.items())


def run(config_path, mode):
    config = load_config(config_path)
    assert config['max_episodes'] == 300 and config['total_timesteps'] == 180000
    sequence = config.get('room_sequence', False)
    room_count = 16 if sequence else 8
    assert config['room_count'] == room_count and config['room_size'] == [20, 20]
    assert config['area_size'] == ([480, 200] if sequence else [160, 160])
    assert config['episode_seconds'] == 60 and config['new_room_reward'] == 10
    parent = (config_path.parent / config['resume_from']).resolve()
    assert checkpoint_hash(parent) == config['resume_sha256']
    torch.set_num_threads(1)
    model = PPO.load(parent, device='cpu', seed=config['seed'])
    assert model.observation_contract == 'local-waypoint-v1'
    assert (model.learning_rate, model.gamma, model.ent_coef, model.n_steps, model.batch_size) == (.0002, .99, .01, 256, 64)
    start_steps, start_updates = model.num_timesteps, model._n_updates
    stamp = datetime.now(timezone.utc).strftime('%Y%m%d-%H%M%S') + '-' + mode
    directory = (config_path.parent / config['output_dir']).resolve() / stamp
    directory.mkdir(parents=True)
    destination = directory
    if mode == 'train':
        destination = GAME / 'SavedModels/qa/local-waypoint-v1' / (('qa-room-sequence-' if sequence else 'qa-room-throughput-') + stamp)
        destination.mkdir()
    print('Run directory: ' + str(directory), flush=True)
    print('Retained directory: ' + str(destination), flush=True)
    sources = SOURCES + ['Training/train_room_throughput.py', 'Training/train_two_room_navigation.py',
        'Training/run_waypoint_chain.py', 'Training/run_waypoint_throughput.py',
        'Assets/Editor/QaWaypointChainSession.cs', 'Assets/PCGDungeon/Scripts/QA/QaNavigationCoordinator.cs',
        'Assets/PCGDungeon/Scripts/Generation/PcgGenerationParameters.cs']
    hashes = {name: checkpoint_hash(GAME / name) for name in sources}
    shutil.copy2(config_path, destination / 'training-config.yaml')
    save(destination / 'run.json', dict(status='running', parent_checkpoint=str(parent),
        parent_sha256=config['resume_sha256'], parent_model_steps=start_steps, parent_optimizer_updates=start_updates,
        parent_lineage_episodes=4300, training_episodes=300, training_seed_range=[config['seed'],config['seed']+299],
        evaluation_seed_range=[config['evaluation_seed'],config['evaluation_seed']+99], rooms=room_count, room_sequence=sequence, seconds=60, actions_per_episode=600,
        starting_room_excluded=True, revisits_rewarded=False, exit_disabled=True,
        reward='+10/distinct new room, shared +5 positive waypoint shaping cap, -0.001/action',
        room_entry='Actor at least one unit inside the 20x20 room footprint; starting room excluded',
        goal_heuristic='Next ordered room in a non-exhaustible prefix' if sequence else 'Nearest unvisited room by A* route length', prepared_sequence_exhaustible=False if sequence else True,
        observation_contract='local-waypoint-v1', source_sha256=hashes))
    for name in sources:
        target = destination / 'source' / name; target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(GAME / name, target)
    settings = {p: p.read_bytes() for p in (GAME / 'ProjectSettings').iterdir() if p.is_file()}
    for p, content in settings.items():
        target = directory / 'original-settings' / p.name; target.parent.mkdir(exist_ok=True)
        target.write_bytes(content)
    environment = vector = process = None
    results = {}
    try:
        for name in ['verification', 'parent-evaluation', 'training', 'child-evaluation']:
            if mode == 'verify' and name != 'verification': break
            training = name == 'training'; rewards = training or name == 'verification'
            count = 4 if name == 'verification' else (300 if training else 100)
            seed = config.get('verification_seed', 454000) if name == 'verification' else (config['seed'] if training else config['evaluation_seed'])
            output = directory / name
            process = launch(output, rewards, sequence); ready(output, process)
            environment = RoomThroughputEnvironment(output, count, seed, rewards, room_count)
            weights = {k: v.clone() for k, v in model.policy.state_dict().items()}
            counters = model.num_timesteps, model._n_updates
            if training:
                vector = DummyVecEnv([lambda: Monitor(environment)])
                model.set_env(vector); model.set_logger(configure(str(output), ['csv']))
                model.training_task = 'distinct-room-throughput-60seconds'
                model.learn(180000, reset_num_timesteps=False, callback=CallbackList([
                    LineageCallback(environment, 4300), CheckpointCallback(10000, str(destination / 'checkpoints')),
                    StopTrainingOnMaxEpisodes(300)]))
                assert len(environment.records) == 300 and model.num_timesteps - start_steps == 180000
                assert len(records(output / 'layouts.jsonl')) == 300
                model.completed_lineage_episodes = 4600
                model.save(destination / 'final_model.zip'); save_checkpoint_manifest(destination, 'local-waypoint-v1')
            else:
                model.policy.set_training_mode(False)
                for episode in range(count):
                    observation, _ = environment.reset(); done = False
                    while not done:
                        if name == 'verification':
                            action = observation[131:133] if episode < 3 else np.zeros(2, dtype=np.float32)
                        else: action, _ = model.predict(observation, deterministic=True)
                        observation, _, terminal, truncated, _ = environment.step(action)
                        done = terminal or truncated
                check_frozen(model, weights, counters)
            rows = environment.records
            visits = records(output / 'room-visits.jsonl') if (output / 'room-visits.jsonl').exists() else []
            assert len(visits) == sum(r['new_rooms'] for r in rows)
            assert len({(r['seed'], r['room']) for r in visits}) == len(visits)
            assert all(r['rooms'] == room_count for r in records(output / 'layouts.jsonl'))
            if name == 'verification':
                assert all(r['new_rooms'] >= 1 for r in rows[:3])
                assert rows[3]['new_rooms'] == 0 and rows[3]['positive_waypoint_reward'] == 0
            result = dict(status='complete', **summary(rows), frozen=not training,
                model_steps=model.num_timesteps, optimizer_updates=model._n_updates,
                added_model_steps=model.num_timesteps-counters[0], added_optimizer_updates=model._n_updates-counters[1])
            save(output / 'result.json', result); results[name] = result
            if vector is not None: vector.close(); vector = None
            else: environment.close()
            environment = None
            finish(output, process); retain(output, destination)
            save(destination / (name+'-result.json'), result)
        if mode == 'verify':
            metadata = json.loads((destination/'run.json').read_text())
            metadata.update(status='verified', actual_training_episodes=0)
            save(destination/'run.json', metadata)
            save(directory/'progress.json', dict(status='verified', training_episodes=0, reference_new_rooms=results['verification']['total_new_rooms']))
            print('Verification complete; zero learning updates.', flush=True)
            return
        assert records(directory/'parent-evaluation/layouts.jsonl') == records(directory/'child-evaluation/layouts.jsonl')

        weights = {k: v.clone() for k, v in model.policy.state_dict().items()}; counters = model.num_timesteps, model._n_updates
        exits = exit_phase(directory, 'two-room-regression', range(451000,451100), model=model)
        check_frozen(model, weights, counters); retain(directory/'two-room-regression', destination)
        results['two-room-regression'] = dict(parent=exit_summary(records(parent.parent/'child-evaluation/episodes.jsonl')), child=exit_summary(exits))

        output = directory / 'empty-room-regression'
        process = launch_chain(output, dict(min_waypoints=10,max_waypoints=10), False, True, continuous=True)
        ready(output, process); environment = ThroughputEnvironment(output, {'port':7783},100,288000,False)
        for _ in range(100):
            observation,_=environment.reset(); done=False
            while not done:
                action,_=model.predict(observation,deterministic=True)
                observation,_,terminal,truncated,_=environment.step(action); done=terminal or truncated
        check_frozen(model,weights,counters)
        assert records(parent.parent/'empty-room-regression/chains.jsonl') == records(output/'chains.jsonl')
        results['empty-room-regression'] = dict(parent=waypoint_summary(records(parent.parent/'empty-room-regression/episodes.jsonl')),child=waypoint_summary(environment.records))
        environment.close(); environment=None; finish(output,process); retain(output,destination)
        assert checkpoint_hash(parent)==config['resume_sha256']
        assert all(checkpoint_hash(GAME/name)==h for name,h in hashes.items())
        save(destination/'completion.json',dict(status='complete',training_episodes=300,training_steps=180000,
            final_model_steps=model.num_timesteps,final_optimizer_updates=model._n_updates,final_lineage_episodes=4600,
            discarded_partial_rollout_transitions=180000%256,final_sha256=checkpoint_hash(destination/'final_model.zip'),results=results))
        metadata=json.loads((destination/'run.json').read_text()); metadata['status']='complete'; save(destination/'run.json',metadata)
        save(directory/'progress.json',dict(status='complete',training_episodes=300))
        print(json.dumps(json.loads((destination/'completion.json').read_text()),indent=2),flush=True)
        print('Completed run: '+str(destination),flush=True)
    except BaseException as error:
        save(destination/'failure.json',dict(error=str(error),final_model_exists=(destination/'final_model.zip').exists())); raise
    finally:
        if vector is not None: vector.close()
        elif environment is not None: environment.close()
        if process is not None and process.poll() is None: process.terminate(); process.wait(timeout=30)
        for p,content in settings.items():
            if p.read_bytes()!=content: p.write_bytes(content)


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--config',type=Path,required=True)
    parser.add_argument('--mode',choices=['verify','train'],default='verify')
    args=parser.parse_args(); run(args.config.resolve(),args.mode)
