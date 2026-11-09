"""Maintained local-movement experiment runner: Unity preflight, exact episode budget, frozen evaluation."""

import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import sys
import time
import traceback

GAME = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(GAME.parent / 'unity-reinforcement-learning/PythonEnv'))
import numpy as np
import torch
from stable_baselines3 import PPO
from stable_baselines3.common.callbacks import CallbackList, CheckpointCallback, StopTrainingOnMaxEpisodes
from stable_baselines3.common.logger import configure
from stable_baselines3.common.monitor import Monitor
from stable_baselines3.common.vec_env import DummyVecEnv
from train import load_config
from unity_env import UnityGymEnv


def save(path, value):
    temporary = path.with_suffix(path.suffix + '.tmp')
    temporary.write_text(json.dumps(value, indent=2, allow_nan=False), encoding='utf-8')
    for attempt in range(100):
        try:
            temporary.replace(path)
            return
        except PermissionError:
            if attempt == 99:
                raise
            time.sleep(.05)


def wait_record(directory, name, process, timeout=300):
    path = directory / name
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        failure = directory / 'unity-failure.txt'
        if failure.exists():
            raise RuntimeError(failure.read_text())
        if process.poll() is not None:
            raise RuntimeError(f'Unity exited {process.returncode}; see {directory / "unity.log"}')
        try:
            return json.loads(path.read_text())
        except (FileNotFoundError, PermissionError, json.JSONDecodeError):
            time.sleep(.05)
    raise TimeoutError(str(path))


def launch_unity(executable, directory, config, episodes, verify, mode="train"):
    directory.mkdir(exist_ok=True)
    save(directory / 'session.json', {'scene': config['unity_scene'], 'port': config['port'], 'episodes': episodes,
         'verify': verify, 'mode': mode, 'policy_version': config.get('observation_contract', 'local-vision-v1'), 'room_width': config['room_size'][0], 'room_depth': config['room_size'][1],
         'initial_exit_visible': config['initial_exit_visible'],
         'completion_reward': config.get('completion_reward', 50.0),
         'episode_seconds': config.get('episode_seconds', 30), 'time_scale': config.get('time_scale', 20),
         'navigator_disabled': config.get('navigator_disabled', False),
         'trace_exit_decisions': config.get('trace_exit_decisions', False),
         'max_waypoint_distance': config.get('max_waypoint_distance', 6.0),
         'min_exit_distance': config['exit_distance_range'][0], 'max_exit_distance': config['exit_distance_range'][1],
         'training_seed': config['seed'], 'training_episodes': config['max_episodes'],
         'evaluation_seed': config['evaluation_seed'], 'evaluation_episodes': config['evaluation_episodes']})
    process = subprocess.Popen([
        str(executable), '-batchmode', '-nographics', '-projectPath', str(GAME),
        '-executeMethod', 'QaLocalMovementSession.Run', '-trainingRunDirectory', str(directory),
        '-logFile', str(directory / 'unity.log'),
    ], cwd=GAME, creationflags=subprocess.CREATE_NO_WINDOW)
    save(directory / 'process.json', {'unity_pid': process.pid})
    return process


def wait_ready(directory, process):
    deadline = time.monotonic() + 300
    while not (directory / 'unity-ready.txt').exists():
        if (directory / 'unity-failure.txt').exists():
            raise RuntimeError((directory / 'unity-failure.txt').read_text())
        if process.poll() is not None:
            raise RuntimeError(f'Unity exited {process.returncode}; see unity.log')
        if time.monotonic() > deadline:
            raise TimeoutError('Unity startup/preflight')
        time.sleep(.1)


def finish_unity(directory, process):
    (directory / 'session-finished.txt').write_text('Requested episodes complete.\n')
    process.wait(timeout=45)
    assert process.returncode == 0 and (directory / 'unity-finished.txt').exists()


def summary(episodes):
    successful = [e for e in episodes if e['completed']]
    return {
        'completed_episodes': len(episodes), 'successes': len(successful),
        'timeouts': sum(e['end_reason'] == 'max_duration' for e in episodes),
        'deaths': sum(e['player_died'] for e in episodes),
        'success_rate': len(successful) / len(episodes) if episodes else 0,
        'mean_successful_exit_seconds': float(np.mean([e['elapsed_seconds'] for e in successful])) if successful else None,
        'total_stuck_penalty': sum(e['stuck_penalty'] for e in episodes),
        'total_exploration_reward': sum(e['exploration_reward'] for e in episodes),
    }


class RecordedEnvironment(UnityGymEnv):
    def __init__(self, directory, process, config, target, seed, progress_path, phase):
        super().__init__(port=config['port'], seed=seed, timeout_seconds=config['socket_timeout_seconds'])
        assert self.observation_size == 134 and self.action_size == 2
        self.directory, self.process = directory, process
        self.target, self.first_seed = target, seed
        self.policy_version = config.get('observation_contract', 'local-vision-v1')
        self.initial_exit_visible = config['initial_exit_visible']
        self.exit_distance_range = config['exit_distance_range']
        self.episode_seconds = config.get('episode_seconds', 30)
        self.navigator_disabled = config.get('navigator_disabled', False)
        self.max_waypoint_distance = config.get('max_waypoint_distance', 6.0)
        self.progress_path, self.phase = progress_path, phase
        self.episodes = []
        self.steps = 0
        self.terminal = None

    def reset(self, *, seed=None, options=None):
        # DummyVecEnv must not start an extra episode after the configured budget.
        if len(self.episodes) == self.target:
            return self.terminal.copy(), {}
        self.active_seed = self.first_seed + len(self.episodes)
        observation, info = super().reset(seed=self.active_seed, options=options)
        self.episode_steps, self.episode_reward = 0, 0.
        assert np.all(np.abs(observation) <= 1.00001)
        if self.navigator_disabled:
            assert np.all(observation[130:134] == 0)
        elif self.policy_version == 'local-waypoint-v1':
            assert observation[130] == 1 and 0 < observation[133] <= self.max_waypoint_distance / 12 + .00001
        else:
            assert observation[130] == int(self.initial_exit_visible)
        if not self.initial_exit_visible and self.policy_version == 'local-vision-v1':
            assert np.all(observation[130:134] == 0)
        return observation, info

    def step(self, action):
        observation, reward, terminated, truncated, info = super().step(action)
        if self.navigator_disabled:
            assert np.all(observation[130:134] == 0)
        self.steps += 1
        self.episode_steps += 1
        self.episode_reward += reward
        if terminated or truncated:
            record = wait_record(self.directory, f'episode-{len(self.episodes)}.json', self.process, 30)
            assert record['seed'] == self.active_seed
            late_terminal = (record['completed'] or record['player_died']) and record['steps'] == self.episode_steps - 1
            assert record['steps'] == self.episode_steps or late_terminal
            assert record['initial_exit_visible'] == self.initial_exit_visible
            assert self.exit_distance_range[0] - .0001 <= record['initial_exit_distance'] <= self.exit_distance_range[1] + .0001
            assert record['elapsed_seconds'] <= self.episode_seconds + .0001 and record['exploration_reward'] == 0
            if self.navigator_disabled:
                assert record['navigator_disabled'] and record['waypoint_assignments'] == record['replans'] == record['abandoned_goals'] == 0
                assert record['waypoint_bonus'] == 0
            record.update(reward=self.episode_reward, trainer_requests=self.episode_steps)
            self.episodes.append(record)
            self.terminal = observation
            with (self.directory / 'episodes.jsonl').open('a', encoding='utf-8') as stream:
                stream.write(json.dumps(record) + '\n')
            progress = dict(status=self.phase, target_episodes=self.target, steps=self.steps, **summary(self.episodes))
            save(self.progress_path, progress)
            if len(self.episodes) % 10 == 0:
                print(f'{self.phase}: {len(self.episodes)}/{self.target}; successes {progress["successes"]}; steps {self.steps}', flush=True)
        return observation, reward, terminated, truncated, info


def run(config_path, unity):
    config = load_config(config_path)
    resume_path = None
    parent_hash = None
    if config['resume_from']:
        resume_path = (config_path.parent / config['resume_from']).resolve()
        parent_hash = hashlib.sha256(resume_path.read_bytes()).hexdigest()
    output = (config_path.parent / config['output_dir']).resolve()
    directory = output / datetime.now(timezone.utc).strftime('%Y%m%d-%H%M%S')
    directory.mkdir(parents=True)
    save(directory / 'training-config.json', config)
    shutil.copy2(config_path, directory / 'training-config.yaml')
    settings = directory / 'original-settings'
    shutil.copytree(GAME / 'ProjectSettings', settings)
    sources = [config['unity_scene'], 'Assets/PCGDungeon/Scripts/Policy/QaLocalMovementPolicyMapping.cs',
               'Assets/PCGDungeon/Scripts/RL/QaSingleRoomRlEnvironment.cs', 'Assets/PCGDungeon/Scripts/QA/ModelSensorPackage.cs',
               'Assets/PCGDungeon/Scripts/QA/QaPlayerController.cs', 'Assets/Editor/QaLocalMovementSession.cs', 'Training/run_local_movement.py']
    hashes = {name: hashlib.sha256((GAME / name).read_bytes()).hexdigest() for name in sources}
    save(directory / 'run.json', {'started_at_utc': datetime.now(timezone.utc).isoformat(), 'fresh_policy': resume_path is None,
         'parent_checkpoint': str(resume_path) if resume_path else None, 'parent_checkpoint_sha256': parent_hash,
         'observation_contract': 'local-vision-v1', 'observation_size': 134, 'action_size': 2,
         'room_size': config['room_size'], 'episode_seconds': 30, 'manual_advancement': True, 'source_sha256': hashes,
         'initial_exit_visible': config['initial_exit_visible'], 'exit_distance_range': config['exit_distance_range'],
         'training_seed_range': [config['seed'], config['seed'] + config['max_episodes'] - 1],
         'evaluation_seed_range': [config['evaluation_seed'], config['evaluation_seed'] + config['evaluation_episodes'] - 1]})
    destination = GAME / 'SavedModels' / 'qa' / 'local-vision-v1' / ('qa-local-movement-' + directory.name)
    destination.mkdir(parents=True)
    shutil.copy2(directory / 'training-config.yaml', destination / 'training-config.yaml')
    shutil.copy2(directory / 'run.json', destination / 'run.json')
    print(f'Run directory: {directory}', flush=True)
    save(directory / 'progress.json', {'status': 'preflight', 'target_episodes': config['max_episodes'], 'completed_episodes': 0})
    process = None
    environment = vector = model = None
    phase = 'training'
    try:
        torch.set_num_threads(1)
        process = launch_unity(unity, directory, config, config['max_episodes'], True)
        wait_ready(directory, process)
        environment = RecordedEnvironment(directory, process, config, config['max_episodes'], config['seed'], directory / 'progress.json', phase)
        vector = DummyVecEnv([lambda: Monitor(environment)])
        if resume_path:
            # Continue the compatible policy and optimizer; keep cumulative model steps.
            model = PPO.load(resume_path, env=vector, device=config['device'], seed=config['seed'])
            assert model.learning_rate == config['learning_rate'] and model.gamma == config['gamma']
            assert model.ent_coef == config['ent_coef'] and model.n_steps == config['n_steps']
            assert model.batch_size == config['ppo']['batch_size']
        else:
            model = PPO('MlpPolicy', vector, learning_rate=config['learning_rate'], gamma=config['gamma'],
                        ent_coef=config['ent_coef'], n_steps=config['n_steps'], batch_size=config['ppo']['batch_size'],
                        device=config['device'], seed=config['seed'], verbose=0)
        initial_model_timesteps = model.num_timesteps
        model.set_logger(configure(str(directory), ['csv']))
        callbacks = [StopTrainingOnMaxEpisodes(config['max_episodes'])]
        if config['checkpoint_freq']:
            callbacks.append(CheckpointCallback(config['checkpoint_freq'], str(destination / 'checkpoints')))
        started = time.monotonic()
        model.learn(config['total_timesteps'], callback=CallbackList(callbacks), reset_num_timesteps=resume_path is None)
        assert len(environment.episodes) == config['max_episodes']
        checkpoint = directory / 'final_model.zip'
        model.save(checkpoint)
        shutil.copy2(checkpoint, destination / checkpoint.name)
        save_checkpoint_manifest(destination, 'local-vision-v1')
        result = dict(status='training_complete', **summary(environment.episodes), training_steps=environment.steps,
                      training_wall_seconds=time.monotonic() - started, fresh_policy=resume_path is None,
                      initial_model_timesteps=initial_model_timesteps, final_model_timesteps=model.num_timesteps,
                      parent_checkpoint=str(resume_path) if resume_path else None, parent_checkpoint_sha256=parent_hash,
                      observation_size=134, action_size=2, observation_contract='local-vision-v1', room_size=config['room_size'],
                      initial_exit_visible=config['initial_exit_visible'], exit_distance_range=config['exit_distance_range'],
                      manual_advancement=True, checkpoint_sha256=hashlib.sha256(checkpoint.read_bytes()).hexdigest())
        save(directory / 'result.json', result)
        vector.close()
        vector = environment = None
        finish_unity(directory, process)
        assert all(hashlib.sha256((GAME / name).read_bytes()).hexdigest() == digest for name, digest in hashes.items())
        save(destination / 'result.json', result)

        phase = 'evaluation'
        model = PPO.load(checkpoint, device=config['device'])
        updates = model._n_updates
        evaluation_dir = directory / 'evaluation'
        process = launch_unity(unity, evaluation_dir, config, config['evaluation_episodes'], False)
        save(directory / 'progress.json', dict(status=phase, training=result, target_episodes=config['evaluation_episodes'], completed_episodes=0))
        wait_ready(evaluation_dir, process)
        environment = RecordedEnvironment(evaluation_dir, process, config, config['evaluation_episodes'], config['evaluation_seed'], directory / 'progress.json', phase)
        for _ in range(config['evaluation_episodes']):
            observation, _ = environment.reset()
            done = False
            while not done:
                action, _ = model.predict(observation, deterministic=True)
                observation, _, terminated, truncated, _ = environment.step(action)
                done = terminated or truncated
        evaluation = dict(status='complete', **summary(environment.episodes), seed_range=[config['evaluation_seed'], config['evaluation_seed'] + config['evaluation_episodes'] - 1],
                          deterministic_actions=True, weight_updates=0, checkpoint_sha256=result['checkpoint_sha256'])
        environment.close()
        environment = None
        finish_unity(evaluation_dir, process)
        assert model._n_updates == updates and hashlib.sha256(checkpoint.read_bytes()).hexdigest() == result['checkpoint_sha256']
        result.update(status='complete', evaluation=evaluation, saved_checkpoint=str(destination / checkpoint.name))
        save(directory / 'evaluation-result.json', evaluation)
        save(destination / 'evaluation.json', evaluation)
        save(directory / 'result.json', result)
        save(destination / 'result.json', result)
        save(directory / 'progress.json', result)
        print(json.dumps(result, indent=2), flush=True)
    except BaseException as error:
        if model is not None and phase == 'training':
            model.save(directory / 'emergency_model.zip')
        save(directory / 'failure.json', {'phase': phase, 'error': str(error), 'traceback': traceback.format_exc()})
        save(directory / 'progress.json', {'status': 'failed', 'phase': phase, 'error': str(error)})
        raise
    finally:
        if vector is not None:
            vector.close()
        elif environment is not None:
            environment.close()
        if process is not None and process.poll() is None:
            process.terminate()
            try:
                process.wait(timeout=30)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait(timeout=30)
        for backup in settings.iterdir():
            if backup.is_file():
                target = GAME / 'ProjectSettings' / backup.name
                if target.read_bytes() != backup.read_bytes():
                    target.write_bytes(backup.read_bytes())


def checkpoint_hash(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def save_checkpoint_manifest(directory, architecture):
    """Keep portable file identities beside each run, including periodic snapshots."""
    checkpoints = []
    for path in sorted(directory.rglob('*.zip')):
        checkpoints.append({'path': path.relative_to(directory).as_posix(),
                            'sha256': checkpoint_hash(path), 'bytes': path.stat().st_size})
    save(directory / 'checkpoints.json', {'architecture': architecture, 'checkpoints': checkpoints})


def prepare_waypoint_transfer(config, config_path):
    """Parent actor -> fresh PPO critic/optimizer -> versioned zero-step checkpoint."""
    import gymnasium as gym

    parent_path = (config_path.parent / config['transfer_actor_from']).resolve()
    assert checkpoint_hash(parent_path) == config['transfer_parent_sha256']
    parent_metadata = json.loads((parent_path.parent / 'run.json').read_text())
    assert parent_metadata['observation_contract'] == config.get('transfer_parent_contract', 'local-vision-v1')
    destination = (config_path.parent / config['resume_from']).resolve()
    if destination.exists():
        manifest = json.loads((destination.parent / 'transfer.json').read_text())
        assert manifest['checkpoint_sha256'] == checkpoint_hash(destination)
        assert manifest['parent_checkpoint_sha256'] == config['transfer_parent_sha256']
        assert manifest['observation_contract'] == config['observation_contract']
        return destination

    parent = PPO.load(parent_path, device=config['device'])

    class ContractEnvironment(gym.Env):
        observation_space = parent.observation_space
        action_space = parent.action_space

    torch.set_num_threads(1)
    model = PPO('MlpPolicy', ContractEnvironment(), learning_rate=config['learning_rate'], gamma=config['gamma'],
                ent_coef=config['ent_coef'], n_steps=config['n_steps'], batch_size=config['ppo']['batch_size'],
                device=config['device'], seed=config['seed'], policy_kwargs=parent.policy_kwargs)
    state = model.policy.state_dict()
    critic = {key: value.clone() for key, value in state.items() if 'value' in key}
    actor = {key: value for key, value in parent.policy.state_dict().items()
             if key.startswith(('mlp_extractor.policy_net.', 'action_net.')) or key == 'log_std'}
    assert len(actor) == 7 and model.policy.mlp_extractor.latent_dim_pi == 64
    state.update(actor)
    model.policy.load_state_dict(state)
    assert all(torch.equal(model.policy.state_dict()[key], value) for key, value in actor.items())
    assert all(torch.equal(model.policy.state_dict()[key], value) for key, value in critic.items())
    assert any(not torch.equal(value, parent.policy.state_dict()[key]) for key, value in critic.items())
    assert not model.policy.optimizer.state and model._n_updates == 0 and model.num_timesteps == 0
    model.observation_contract = config['observation_contract']
    model.parent_checkpoint_sha256 = config['transfer_parent_sha256']
    model.parent_lineage_episodes = config['parent_lineage_episodes']
    destination.parent.mkdir(parents=True)
    model.save(destination)
    save(destination.parent / 'transfer.json', {
        'observation_contract': model.observation_contract, 'observation_size': 134, 'action_size': 2,
        'architecture': [134, 64, 64, 2], 'parent_checkpoint': str(parent_path),
        'parent_checkpoint_sha256': config['transfer_parent_sha256'],
        'parent_lineage_episodes': model.parent_lineage_episodes, 'parent_model_steps': parent.num_timesteps,
        'new_training_episodes': 0, 'new_training_steps': 0, 'weight_updates': 0,
        'transferred_parameters': sorted(actor), 'fresh_critic': True, 'fresh_optimizer': True,
        'checkpoint_sha256': checkpoint_hash(destination)})
    save_checkpoint_manifest(destination.parent, model.observation_contract)
    assert checkpoint_hash(parent_path) == config['transfer_parent_sha256']
    return destination


def run_waypoints(config_path, unity, mode, episodes, without_waypoint=False):
    config = load_config(config_path)
    completion_reward = float(config.get('completion_reward', 50.0))
    assert config['observation_contract'] == 'local-waypoint-v1', 'Waypoint modes require the versioned waypoint config'
    assert episodes is None or (mode == 'evaluate' and episodes > 0)
    assert not without_waypoint or mode == 'evaluate', 'Waypoint masking is a frozen-evaluation control only'
    torch.set_num_threads(1)
    if config.get('transfer_actor_from'):
        checkpoint = prepare_waypoint_transfer(config, config_path)
        parent_path = (config_path.parent / config['transfer_actor_from']).resolve()
        parent_hash = config['transfer_parent_sha256']
    else:
        # A compatible waypoint stage continues the complete model and optimizer.
        checkpoint = (config_path.parent / config['resume_from']).resolve()
        parent_path = checkpoint
        parent_hash = config['resume_sha256']
        assert checkpoint_hash(checkpoint) == parent_hash
    model = PPO.load(checkpoint, device=config['device'], seed=config['seed'])
    assert model.observation_contract == config['observation_contract']
    assert model.observation_space.shape == (134,) and model.action_space.shape == (2,)
    assert model.learning_rate == config['learning_rate'] and model.gamma == config['gamma']
    assert model.ent_coef == config['ent_coef'] and model.n_steps == config['n_steps']
    assert model.batch_size == config['ppo']['batch_size']
    initial_model_timesteps = model.num_timesteps
    if mode == 'prepare':
        print(f'Validated starting checkpoint ({initial_model_timesteps} model steps): {checkpoint}', flush=True)
        return
    output = (config_path.parent / config['output_dir']).resolve()
    suffix = mode
    if without_waypoint:
        suffix += '-without-waypoint'
    directory = output / (datetime.now(timezone.utc).strftime('%Y%m%d-%H%M%S') + '-' + suffix)
    directory.mkdir(parents=True)
    shutil.copy2(config_path, directory / 'training-config.yaml')
    settings = directory / 'original-settings'
    shutil.copytree(GAME / 'ProjectSettings', settings)
    source_paths = [config['unity_scene'], 'Training/run_local_movement.py', 'Assets/Editor/QaLocalMovementSession.cs',
                    'Assets/Editor/QaWaypointVerification.cs', 'Assets/PCGDungeon/Scripts/QA/QaMapNavigation.cs',
                    'Assets/PCGDungeon/Scripts/QA/QaNavigationCoordinator.cs', 'Assets/PCGDungeon/Scripts/QA/ModelSensorGrid.cs',
                    'Assets/PCGDungeon/Scripts/QA/ModelSensorPackage.cs', 'Assets/PCGDungeon/Scripts/Policy/QaWaypointPolicyMapping.cs',
                    'Assets/PCGDungeon/Scripts/Policy/QaLocalMovementPolicyMapping.cs', 'Assets/PCGDungeon/Scripts/RL/QaSingleRoomRlEnvironment.cs']
    sources = {name: checkpoint_hash(GAME / name) for name in source_paths}
    save(directory / 'run.json', dict(mode=mode, observation_contract=model.observation_contract,
         task=config.get('task', 'exit-navigation'),
         parent_checkpoint_sha256=checkpoint_hash(parent_path), parent_lineage_episodes=config['parent_lineage_episodes'],
         checkpoint_sha256=checkpoint_hash(checkpoint), source_sha256=sources, manual_advancement=True,
         waypoint_inputs_masked=without_waypoint, initial_model_timesteps=initial_model_timesteps,
         navigator_disabled=config.get('navigator_disabled', False),
         completion_reward=completion_reward,
         episode_seconds=config.get('episode_seconds', 30),
         max_waypoint_distance=config.get('max_waypoint_distance', 6.0),
         parent_training_task=getattr(model, 'training_task', 'exit-navigation'),
         parent_training_completion_reward=getattr(model, 'training_completion_reward', None)))
    destination = None
    if mode == 'train':
        model.training_task = config.get('task', 'exit-navigation')
        model.parent_checkpoint_sha256 = parent_hash
        model.parent_lineage_episodes = config['parent_lineage_episodes']
        model.training_completion_reward = completion_reward
        destination = GAME / 'SavedModels' / 'qa' / model.observation_contract / ('qa-waypoint-' + directory.name)
        destination.mkdir(parents=True)
        shutil.copy2(directory / 'training-config.yaml', destination / 'training-config.yaml')
        shutil.copy2(directory / 'run.json', destination / 'run.json')
    print(f'Run directory: {directory}', flush=True)
    process = environment = vector = None
    try:
        if mode == 'verify':
            process = launch_unity(unity, directory, config, 0, True, mode)
            process.wait(timeout=600)
            assert process.returncode == 0 and (directory / 'verification.json').exists(), 'See unity.log/unity-failure.txt'
            print((directory / 'verification.json').read_text(), flush=True)
            return
        phases = ['training', 'evaluation'] if mode == 'train' else ['evaluation']
        for phase in phases:
            training = phase == 'training'
            target = config['max_episodes'] if training else (episodes or config['evaluation_episodes'])
            seed = config['seed'] if training else config['evaluation_seed']
            phase_dir = directory / phase
            process = launch_unity(unity, phase_dir, config, target, training, mode)
            wait_ready(phase_dir, process)
            if training:
                shutil.copy2(phase_dir / 'verification.json', destination / 'verification.json')
            ready = json.loads((phase_dir / 'policy-contract.json').read_text())
            assert ready['observation_contract'] == model.observation_contract
            assert ready['completion_reward'] == completion_reward
            assert ready['episode_seconds'] == config.get('episode_seconds', 30)
            assert ready['navigator_disabled'] == config.get('navigator_disabled', False)
            assert ready['max_waypoint_distance'] == config.get('max_waypoint_distance', 6.0)
            environment = RecordedEnvironment(phase_dir, process, config, target, seed, directory / 'progress.json', phase)
            before = {key: value.clone() for key, value in model.policy.state_dict().items()}
            updates = model._n_updates
            source_hash = checkpoint_hash(checkpoint)
            if training:
                vector = DummyVecEnv([lambda: Monitor(environment)])
                model.set_env(vector)
                model.set_logger(configure(str(phase_dir), ['csv']))
                callbacks = [StopTrainingOnMaxEpisodes(target)]
                if config['checkpoint_freq']:
                    callbacks.append(CheckpointCallback(config['checkpoint_freq'], str(destination / 'checkpoints')))
                model.learn(config['total_timesteps'], callback=CallbackList(callbacks), reset_num_timesteps=model.num_timesteps == 0)
                checkpoint = directory / 'final_model.zip'
                model.save(checkpoint)
                # Retain the final checkpoint before starting the separate evaluation process.
                shutil.copy2(checkpoint, destination / checkpoint.name)
                save_checkpoint_manifest(destination, model.observation_contract)
            else:
                for _ in range(target):
                    observation, _ = environment.reset()
                    done = False
                    while not done:
                        policy_observation = observation
                        if without_waypoint:
                            policy_observation = observation.copy()
                            policy_observation[130:134] = 0
                        action, _ = model.predict(policy_observation, deterministic=True)
                        observation, _, terminated, truncated, _ = environment.step(action)
                        done = terminated or truncated
                assert model._n_updates == updates and all(torch.equal(model.policy.state_dict()[key], value) for key, value in before.items())
                assert checkpoint_hash(checkpoint) == source_hash
            assert len(environment.episodes) == target
            result = dict(status='complete', phase=phase, **summary(environment.episodes),
                          seed_range=[seed, seed + target - 1], steps=environment.steps,
                          weight_updates=model._n_updates - updates, observation_contract=model.observation_contract,
                          parent_lineage_episodes=config['parent_lineage_episodes'],
                          initial_model_timesteps=initial_model_timesteps,
                          new_training_steps=model.num_timesteps - initial_model_timesteps,
                          new_model_training_steps=model.num_timesteps,
                          waypoint_inputs_masked=without_waypoint,
                          navigator_disabled=config.get('navigator_disabled', False),
                          max_waypoint_distance=config.get('max_waypoint_distance', 6.0),
                          completion_reward=completion_reward,
                          checkpoint_sha256=checkpoint_hash(checkpoint), parent_checkpoint_unchanged=True,
                          total_waypoint_bonus=sum(e['waypoint_bonus'] for e in environment.episodes))
            save(directory / (phase + '-result.json'), result)
            if destination is not None:
                shutil.copy2(directory / (phase + '-result.json'), destination / (phase + '-result.json'))
                shutil.copy2(phase_dir / 'episodes.jsonl', destination / (phase + '-episodes.jsonl'))
            print(json.dumps(result, indent=2), flush=True)
            if vector is not None:
                vector.close(); vector = None
            else:
                environment.close()
            environment = None
            finish_unity(phase_dir, process)
    except BaseException as error:
        save(directory / 'failure.json', dict(error=str(error), traceback=traceback.format_exc()))
        raise
    finally:
        if vector is not None: vector.close()
        elif environment is not None: environment.close()
        if process is not None and process.poll() is None:
            process.terminate()
            process.wait(timeout=30)
        for backup in settings.iterdir():
            if backup.is_file():
                target_path = GAME / 'ProjectSettings' / backup.name
                if target_path.read_bytes() != backup.read_bytes(): target_path.write_bytes(backup.read_bytes())
        assert checkpoint_hash(parent_path) == parent_hash
        assert all(checkpoint_hash(GAME / name) == digest for name, digest in sources.items())


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--config', type=Path, default=GAME / 'Training/qa-player-waypoint-20x20.yaml')
    parser.add_argument('--unity', type=Path, default=Path('C:/Program Files/Unity/Hub/Editor/6000.3.2f1/Editor/Unity.exe'))
    parser.add_argument('--mode', choices=['prepare', 'verify', 'evaluate', 'train'], default='verify')
    parser.add_argument('--episodes', type=int, help='Evaluation-only override (e.g. 10 frozen smoke episodes)')
    parser.add_argument('--without-waypoint', action='store_true', help='Evaluation control: mask only waypoint inputs 130-133')
    args = parser.parse_args()
    if args.without_waypoint and args.mode != 'evaluate':
        parser.error('--without-waypoint is only available with --mode evaluate')
    if args.mode == 'train' and load_config(args.config).get('observation_contract', 'local-vision-v1') == 'local-vision-v1':
        run(args.config.resolve(), args.unity)
    else:
        run_waypoints(args.config.resolve(), args.unity, args.mode, args.episodes, args.without_waypoint)
