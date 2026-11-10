"""QA-47 continuation: 100 timed waypoint episodes, then 300 wall/corner episodes."""

import argparse
from datetime import datetime, timezone
import json
from pathlib import Path
import shutil

from run_local_movement import GAME, checkpoint_hash, load_config, save, save_checkpoint_manifest
from run_waypoint_chain import launch, ready, finish, retain, records
from run_waypoint_throughput import ThroughputEnvironment, summary
from train_two_room_navigation import LineageCallback, frozen_phase
from evaluate_two_room_navigation import SOURCES, summary as exit_summary
import numpy as np
import torch
from stable_baselines3 import PPO
from stable_baselines3.common.callbacks import CallbackList, CheckpointCallback, StopTrainingOnMaxEpisodes
from stable_baselines3.common.logger import configure
from stable_baselines3.common.monitor import Monitor
from stable_baselines3.common.vec_env import DummyVecEnv


def metrics(rows):
    return dict(**summary(rows), actions=sum(r['steps'] for r in rows),
        low_movement_actions=sum(r['low_movement_actions'] for r in rows),
        away_actions=sum(r['away_actions'] for r in rows),
        wall_episodes=sum(r['recovery'] and not r['corner'] for r in rows),
        corner_episodes=sum(r['corner'] for r in rows))


def run(config_path):
    config = load_config(config_path)
    assert config['curriculum'] == [dict(episodes=100, recovery=False), dict(episodes=300, recovery=True)]
    assert config['max_episodes'] == 400 and config['episode_seconds'] == 60
    assert config['learning_rate'] == .00002
    parent = (config_path.parent / config['resume_from']).resolve()
    assert checkpoint_hash(parent) == config['resume_sha256']
    torch.set_num_threads(1)
    model = PPO.load(parent, device='cpu', seed=config['seed'])
    assert model.observation_contract == 'local-waypoint-v1'
    assert (model.learning_rate, model.gamma, model.ent_coef, model.n_steps, model.batch_size) == (.0002, .99, .01, 256, 64)
    initial_steps, initial_updates = model.num_timesteps, model._n_updates
    # Keep actor, critic and optimizer moments; replace both the saved LR and schedule.
    model.learning_rate = config['learning_rate']
    model.lr_schedule = lambda _: config['learning_rate']
    for group in model.policy.optimizer.param_groups:
        group['lr'] = config['learning_rate']
    stamp = datetime.now(timezone.utc).strftime('%Y%m%d-%H%M%S') + '-train'
    directory = (config_path.parent / config['output_dir']).resolve() / stamp
    destination = GAME / 'SavedModels/qa/local-waypoint-v1' / ('qa-waypoint-recovery-' + stamp)
    directory.mkdir(parents=True); destination.mkdir()
    print('Runtime directory: ' + str(directory), flush=True)
    print('Retained directory: ' + str(destination), flush=True)
    sources = list(dict.fromkeys(SOURCES + ['Training/train_waypoint_recovery.py',
        'Training/run_waypoint_chain.py', 'Training/run_waypoint_throughput.py',
        'Training/train_two_room_navigation.py', 'Assets/Editor/QaWaypointChainSession.cs',
        'Assets/Scenes/QaWaypoint40Training.unity', 'Assets/PCGDungeon/Scripts/QA/QaNavigationCoordinator.cs',
        'Assets/PCGDungeon/Scripts/Policy/QaLocalMovementPolicyMapping.cs']))
    hashes = {s: checkpoint_hash(GAME / s) for s in sources}
    shutil.copy2(config_path, destination / 'training-config.yaml')
    metadata = dict(status='preflight', qa_id='QA-50', parent_checkpoint=str(parent), parent_sha256=config['resume_sha256'],
        parent_model_steps=initial_steps, parent_optimizer_updates=initial_updates, parent_lineage_episodes=4300,
        learning_rate=config['learning_rate'], previous_learning_rate=.0002, full_optimizer_continuation=True,
        observation_contract='local-waypoint-v1', training_episodes=400, training_steps=240000,
        training_seed_range=[18000,18399], recovery_evaluation_seed_range=[466000,466099],
        empty_evaluation_seed_range=[288000,288099], two_room_evaluation_seed_range=[451000,451099],
        curriculum=config['curriculum'], seconds=60, one_active_waypoint=True, target_replacement='arrival only',
        recovery='Physical 40x40 perimeter walls; alternate wall/corner starts, four rotations; convex reachable patches',
        reward='Existing timed shaping: 0.015/arrival + 0.0015/new closest unit - 0.001/action, no new penalty',
        source_sha256=hashes)
    save(destination / 'run.json', metadata)
    for name in sources:
        target = destination / 'source' / name; target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(GAME / name, target)
    settings = {p: p.read_bytes() for p in (GAME / 'ProjectSettings').iterdir() if p.is_file()}
    for p, data in settings.items():
        target = directory / 'original-settings' / p.name; target.parent.mkdir(exist_ok=True); target.write_bytes(data)
    process = environment = vector = None
    results = {}

    def waypoint_phase(name, count, first_seed, recovery, training=False, reference=False):
        nonlocal process, environment, vector
        output = directory / name
        stage = dict(min_waypoints=10, max_waypoints=10, recovery=recovery)
        process = launch(output, stage, training or reference, True, continuous=True)
        ready(output, process)
        environment = ThroughputEnvironment(output, config, count, first_seed, training or reference)
        weights = {k: v.clone() for k, v in model.policy.state_dict().items()}
        counters = model.num_timesteps, model._n_updates
        if training:
            vector = DummyVecEnv([lambda: Monitor(environment)])
            model.set_env(vector); model.set_logger(configure(str(output), ['csv']))
            model.training_task = 'waypoint-wall-corner-recovery' if recovery else 'waypoint-throughput-60seconds'
            model.learn(count * 600, reset_num_timesteps=False, callback=CallbackList([
                LineageCallback(environment, 4300 + completed),
                CheckpointCallback(10000, str(destination / 'checkpoints'), name_prefix=name),
                StopTrainingOnMaxEpisodes(count)]))
            assert model.num_timesteps - counters[0] == count * 600
            assert all(group['lr'] == config['learning_rate'] for group in model.policy.optimizer.param_groups)
        else:
            model.policy.set_training_mode(False)
            for episode in range(count):
                observation, _ = environment.reset(); done = False
                while not done:
                    if reference:
                        if episode == count - 1: action = np.zeros(2, dtype=np.float32)
                        else: action = observation[131:133]
                    else: action, _ = model.predict(observation, deterministic=True)
                    observation, _, terminal, truncated, _ = environment.step(action)
                    done = terminal or truncated
            assert counters == (model.num_timesteps, model._n_updates)
            assert all(torch.equal(v, model.policy.state_dict()[k]) for k, v in weights.items())
        rows = environment.records
        assert len(rows) == count and all(r['recovery'] == recovery for r in rows)
        assert len(records(output / 'chains.jsonl')) == count
        assert all(.9999 <= r['initial_distance'] <= 10.0001 for r in records(output / 'legs.jsonl'))
        if reference:
            assert all(r['reached_waypoints'] >= 10 and r['low_movement_actions'] == 0 for r in rows[:-1])
            assert rows[-1]['reached_waypoints'] == 0 and rows[-1]['positive_waypoint_reward'] == 0
        result = dict(status='complete', frozen=not training, **metrics(rows),
            model_steps=model.num_timesteps, optimizer_updates=model._n_updates,
            added_model_steps=model.num_timesteps-counters[0], added_optimizer_updates=model._n_updates-counters[1])
        if vector is not None: vector.close(); vector = None
        else: environment.close()
        environment = None
        finish(output, process); process = None
        save(output / 'result.json', result); retain(output, destination)
        save(destination / (name + '-result.json'), result); results[name] = result
        print(json.dumps(dict(phase=name, **result)), flush=True)
        return rows

    def evaluate(label):
        empty = waypoint_phase(label + '-empty', 100, 288000, False)
        old = records(parent.parent / 'empty-room-regression/chains.jsonl')
        new = records(directory / (label + '-empty') / 'chains.jsonl')
        assert old == new
        waypoint_phase(label + '-recovery', 100, config['evaluation_seed'], True)
        name = label + '-two-room'
        rows = frozen_phase(directory, name, range(451000,451100), model)
        retain(directory / name, destination)
        result = dict(status='complete', frozen=True, **exit_summary(rows))
        results[name] = result; save(destination / (name + '-result.json'), result)
        if label == 'parent':
            previous = records(parent.parent / 'child-evaluation/episodes.jsonl')
            assert [(r['completed'], r['steps']) for r in rows] == [(r['completed'],r['steps']) for r in previous]
            prior_empty = records(parent.parent / 'empty-room-regression/episodes.jsonl')
            assert [r['reached_waypoints'] for r in empty] == [r['reached_waypoints'] for r in prior_empty]

    completed = 0
    try:
        waypoint_phase('verification-empty', 9, 468000, False, reference=True)
        waypoint_phase('verification-recovery', 9, 468000, True, reference=True)
        metadata['status'] = 'parent-evaluation'; save(destination / 'run.json', metadata)
        evaluate('parent')
        for block in range(4):
            metadata.update(status='training', completed_training_episodes=completed, current_block=block+1)
            save(destination / 'run.json', metadata)
            name = f'training-{block+1:02d}'
            waypoint_phase(name, 100, config['seed']+completed, block > 0, training=True)
            completed += 100
            model.completed_lineage_episodes = 4300 + completed
            checkpoint = destination / f'after-{completed:03d}'
            checkpoint.mkdir(); model.save(checkpoint / 'final_model.zip')
            save_checkpoint_manifest(checkpoint, 'local-waypoint-v1')
            reloaded = PPO.load(checkpoint / 'final_model.zip', device='cpu')
            assert reloaded.learning_rate == config['learning_rate'] and reloaded.lr_schedule(1) == config['learning_rate']
            assert reloaded.num_timesteps == model.num_timesteps and reloaded._n_updates == model._n_updates
            assert all(torch.equal(v, reloaded.policy.state_dict()[k]) for k,v in model.policy.state_dict().items())
            metadata.update(status='evaluation', completed_training_episodes=completed)
            save(destination / 'run.json', metadata)
            evaluate(f'after-{completed:03d}')
        assert completed == 400 and model.num_timesteps-initial_steps == 240000
        model.save(destination / 'final_model.zip'); save_checkpoint_manifest(destination,'local-waypoint-v1')
        assert checkpoint_hash(parent) == config['resume_sha256']
        assert all(checkpoint_hash(GAME / s) == digest for s,digest in hashes.items())
        result = dict(status='complete', training_episodes=400, training_steps=240000,
            final_model_steps=model.num_timesteps, final_optimizer_updates=model._n_updates,
            final_lineage_episodes=4700, discarded_partial_rollout_transitions_per_block=60000%256,
            final_sha256=checkpoint_hash(destination/'final_model.zip'),results=results)
        save(destination / 'completion.json', result)
        metadata.update(status='complete', completed_training_episodes=400); save(destination / 'run.json',metadata)
        save(directory / 'progress.json', dict(status='complete', training_episodes=400))
        print('Completed run: ' + str(destination), flush=True)
    except BaseException as error:
        save(destination / 'failure.json',dict(error=str(error),completed_training_episodes=completed))
        raise
    finally:
        if vector is not None: vector.close()
        elif environment is not None: environment.close()
        if process is not None and process.poll() is None:
            process.terminate(); process.wait(timeout=30)
        for path,data in settings.items():
            if path.read_bytes() != data: path.write_bytes(data)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--config', type=Path, required=True)
    args = parser.parse_args()
    run(args.config.resolve())
