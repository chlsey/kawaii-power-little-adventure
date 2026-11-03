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


def launch_unity(executable, directory, config, episodes, verify):
    directory.mkdir(exist_ok=True)
    save(directory / 'session.json', {'scene': config['unity_scene'], 'port': config['port'], 'episodes': episodes,
         'verify': verify, 'room_width': config['room_size'][0], 'room_depth': config['room_size'][1]})
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
        assert observation[130] == 1 and np.all(np.abs(observation) <= 1.00001)
        return observation, info

    def step(self, action):
        observation, reward, terminated, truncated, info = super().step(action)
        self.steps += 1
        self.episode_steps += 1
        self.episode_reward += reward
        if terminated or truncated:
            record = wait_record(self.directory, f'episode-{len(self.episodes)}.json', self.process, 30)
            assert record['seed'] == self.active_seed
            late_terminal = (record['completed'] or record['player_died']) and record['steps'] == self.episode_steps - 1
            assert record['steps'] == self.episode_steps or late_terminal
            assert record['initial_exit_visible'] and 2.9999 <= record['initial_exit_distance'] <= 6.0001
            assert record['elapsed_seconds'] <= 30.0001 and record['exploration_reward'] == 0
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
         'training_seed_range': [config['seed'], config['seed'] + config['max_episodes'] - 1],
         'evaluation_seed_range': [config['evaluation_seed'], config['evaluation_seed'] + config['evaluation_episodes'] - 1]})
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
            callbacks.append(CheckpointCallback(config['checkpoint_freq'], str(directory / 'checkpoints')))
        started = time.monotonic()
        model.learn(config['total_timesteps'], callback=CallbackList(callbacks), reset_num_timesteps=resume_path is None)
        assert len(environment.episodes) == config['max_episodes']
        checkpoint = directory / 'final_model.zip'
        model.save(checkpoint)
        result = dict(status='training_complete', **summary(environment.episodes), training_steps=environment.steps,
                      training_wall_seconds=time.monotonic() - started, fresh_policy=resume_path is None,
                      initial_model_timesteps=initial_model_timesteps, final_model_timesteps=model.num_timesteps,
                      parent_checkpoint=str(resume_path) if resume_path else None, parent_checkpoint_sha256=parent_hash,
                      observation_size=134, action_size=2, observation_contract='local-vision-v1', room_size=config['room_size'],
                      manual_advancement=True, checkpoint_sha256=hashlib.sha256(checkpoint.read_bytes()).hexdigest())
        save(directory / 'result.json', result)
        vector.close()
        vector = environment = None
        finish_unity(directory, process)
        assert all(hashlib.sha256((GAME / name).read_bytes()).hexdigest() == digest for name, digest in hashes.items())
        destination = GAME / 'SavedModels' / ('qa-local-movement-' + directory.name)
        destination.mkdir()
        shutil.copy2(checkpoint, destination / checkpoint.name)
        shutil.copy2(directory / 'training-config.yaml', destination / 'training-config.yaml')
        shutil.copy2(directory / 'run.json', destination / 'run.json')
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


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--config', type=Path, default=GAME / 'Training/qa-player-local-movement.yaml')
    parser.add_argument('--unity', type=Path, default=Path('C:/Program Files/Unity/Hub/Editor/6000.3.2f1/Editor/Unity.exe'))
    args = parser.parse_args()
    run(args.config.resolve(), args.unity)
