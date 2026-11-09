"""Train exactly 500 full 60-second waypoint-throughput episodes; default is verification."""

import argparse
from datetime import datetime, timezone
import json
from pathlib import Path
import shutil
import socket

import numpy as np
import torch
from stable_baselines3 import PPO
from stable_baselines3.common.callbacks import CallbackList, CheckpointCallback, StopTrainingOnMaxEpisodes
from stable_baselines3.common.logger import configure
from stable_baselines3.common.monitor import Monitor
from stable_baselines3.common.vec_env import DummyVecEnv

from run_local_movement import GAME, checkpoint_hash, load_config, save, save_checkpoint_manifest
from run_waypoint_chain import launch, ready, finish, retain, records
from unity_env import UnityGymEnv


def summary(rows):
    arrivals = [r['reached_waypoints'] for r in rows]
    return dict(completed_episodes=len(rows), total_arrivals=sum(arrivals),
        mean_arrivals=float(np.mean(arrivals)), median_arrivals=float(np.median(arrivals)),
        p10_arrivals=float(np.percentile(arrivals, 10)), p90_arrivals=float(np.percentile(arrivals, 90)),
        minimum_arrivals=min(arrivals), maximum_arrivals=max(arrivals),
        episodes_reaching_ten=sum(n >= 10 for n in arrivals))


class ThroughputEnvironment(UnityGymEnv):
    def __init__(self, directory, config, count, seed, rewards):
        super().__init__(port=config['port'], timeout_seconds=300)
        self._socket.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
        self.directory, self.count, self.seed, self.rewards = directory, count, seed, rewards
        self.records = []
        self.terminal = None

    def reset(self, *, seed=None, options=None):
        if len(self.records) == self.count:
            return self.terminal.copy(), {}
        self.steps, self.return_total = 0, 0.
        observation, info = super().reset(seed=self.seed + len(self.records))
        assert observation[0] == observation[1] == 0 and observation[130] == 1
        return observation, info

    def step(self, action):
        observation, reward, terminal, truncated, info = super().step(action)
        self.steps += 1
        self.return_total += reward
        assert np.all(np.isfinite(observation)) and np.all(np.abs(observation) <= 1.00001)
        assert np.all(observation[3:130:2] == 0)
        assert (terminal or truncated) == (self.steps == 600)
        if terminal or truncated:
            row = records(self.directory / 'episodes.jsonl')[-1]
            assert row['seed'] == self.seed + len(self.records)
            assert row['steps'] == 600 and row['elapsed_seconds'] == 60
            assert row['continuous'] and row['planned_waypoints'] == 0 and not row['reached']
            assert row['positive_waypoint_reward'] <= 5.00001
            expected = row['positive_waypoint_reward'] - .6 if self.rewards else 0
            assert abs(self.return_total - expected) < .0002
            row['return'] = self.return_total
            self.records.append(row)
            self.terminal = observation
            if len(self.records) % 10 == 0:
                result = dict(phase=self.directory.name, target=self.count, **summary(self.records))
                save(self.directory.parent / 'progress.json', result)
                print(json.dumps(result), flush=True)
        return observation, reward, terminal, truncated, info


def verify_qa43_prefix(directory):
    historical = GAME / 'SavedModels/qa/local-waypoint-v1/qa-waypoint-chain-20261108-194516-train/evaluation'
    old = records(historical / 'episodes.jsonl')
    new = records(directory / 'episodes.jsonl')
    a, b = records(historical / 'chains.jsonl'), records(directory / 'chains.jsonl')
    assert all(x['seed'] == y['seed'] and x['targets'] == y['targets'][:10] for x, y in zip(a, b))
    legs = records(directory / 'legs.jsonl')
    for previous, current in zip(old, new):
        assert previous['reached'] == (current['reached_waypoints'] >= 10)
        if previous['reached']:
            first_ten_steps = sum(r['steps'] for r in legs if r['seed'] == previous['seed'] and r['leg'] < 10)
            assert previous['steps'] == first_ten_steps


def run(config_path, mode):
    config = load_config(config_path)
    assert config['max_episodes'] == 500 and config['episode_seconds'] == 60
    parent = (config_path.parent / config['resume_from']).resolve()
    assert checkpoint_hash(parent) == config['resume_sha256']
    directory = (config_path.parent / config['output_dir']).resolve() / (datetime.now(timezone.utc).strftime('%Y%m%d-%H%M%S') + '-' + mode)
    directory.mkdir(parents=True)
    destination = directory
    if mode == 'train':
        destination = GAME / 'SavedModels/qa/local-waypoint-v1' / ('qa-waypoint-throughput-' + directory.name)
        destination.mkdir(parents=True)
    print(f'Run directory: {directory}', flush=True)
    shutil.copy2(config_path, directory / 'training-config.yaml')
    source_names = ['Assets/Scenes/QaWaypoint40Training.unity', 'Assets/Editor/QaWaypointChainSession.cs',
        'Training/run_waypoint_throughput.py', 'Training/run_waypoint_chain.py',
        'Assets/PCGDungeon/Scripts/QA/QaPlayerController.cs', 'Assets/PCGDungeon/Scripts/QA/ModelSensorPackage.cs',
        'Assets/PCGDungeon/Scripts/Policy/QaWaypointPolicyMapping.cs', 'Assets/PCGDungeon/Scripts/Policy/QaLocalMovementPolicyMapping.cs',
        'Assets/PCGDungeon/Scripts/QA/QaNavigationCoordinator.cs']
    hashes = {name: checkpoint_hash(GAME / name) for name in source_names}
    save(directory / 'run.json', dict(mode=mode, parent_checkpoint=str(parent), parent_sha256=config['resume_sha256'],
        parent_lineage_episodes=config['parent_lineage_episodes'], observation_contract='local-waypoint-v1',
        training_seed_range=[config['seed'], config['seed'] + 499], evaluation_seed_range=[288000,288099],
        episode_seconds=60, action_limit=600, no_waypoint_count_termination=True,
        reward_scale=.015, reward='0.015 arrival + 0.0015/new closest unit - 0.001/action',
        positive_budget=5, theoretical_positive_maximum=4.86, source_sha256=hashes))
    if mode == 'train':
        for name in ['training-config.yaml','run.json']:
            shutil.copy2(directory/name,destination/name)
        for name in source_names:
            target = destination/'source'/name
            target.parent.mkdir(parents=True,exist_ok=True)
            shutil.copy2(GAME/name,target)
    settings = {path:path.read_bytes() for path in (GAME/'ProjectSettings').iterdir() if path.is_file()}
    for path,content in settings.items():
        target=directory/'original-settings'/path.name
        target.parent.mkdir(exist_ok=True)
        target.write_bytes(content)
    environment = process = vector = None
    stage = dict(min_waypoints=10,max_waypoints=10)
    try:
        phase_dir=directory/'verification'
        process=launch(phase_dir,stage,True,True,continuous=True)
        ready(phase_dir,process)
        environment=ThroughputEnvironment(phase_dir,config,11,330000,True)
        for episode in range(11):
            observation,_=environment.reset()
            done=False
            while not done:
                action=observation[131:133] if episode < 10 else np.zeros(2,dtype=np.float32)
                observation,_,terminal,truncated,_=environment.step(action)
                done=terminal or truncated
        assert all(r['reached_waypoints']>10 for r in environment.records[:10])
        assert environment.records[-1]['reached_waypoints']==0 and environment.records[-1]['positive_waypoint_reward']==0
        assert all(1-.0001<=r['initial_distance']<=10.0001 for r in records(phase_dir/'legs.jsonl'))
        save(phase_dir/'result.json',dict(status='complete',reference_episodes=10,idle_episodes=1,
            **summary(environment.records[:10])))
        environment.close()
        environment=None
        finish(phase_dir,process)
        retain(phase_dir,destination)
        if mode=='verify':
            return

        torch.set_num_threads(1)
        model=PPO.load(parent,device='cpu',seed=config['seed'])
        assert model.observation_contract=='local-waypoint-v1'
        initial_steps,initial_updates=model.num_timesteps,model._n_updates
        old_path=(config_path.parent/config['comparison_checkpoint']).resolve()
        assert checkpoint_hash(old_path)==config['comparison_sha256']
        old=PPO.load(old_path,device='cpu')
        for phase in ['qa43-evaluation','before-continuous','training','evaluation']:
            training=phase=='training'
            policy=old if phase=='qa43-evaluation' else model
            count=500 if training else 100
            seed=config['seed'] if training else config['evaluation_seed']
            phase_dir=directory/phase
            process=launch(phase_dir,stage,training,not training,continuous=True)
            ready(phase_dir,process)
            environment=ThroughputEnvironment(phase_dir,config,count,seed,training)
            before={key:value.clone() for key,value in policy.policy.state_dict().items()}
            steps_before,updates_before=policy.num_timesteps,policy._n_updates
            if training:
                vector=DummyVecEnv([lambda:Monitor(environment)])
                policy.set_env(vector)
                policy.set_logger(configure(str(phase_dir),['csv']))
                policy.training_task='waypoint-throughput-60seconds'
                policy.learn(300000,callback=CallbackList([StopTrainingOnMaxEpisodes(500),
                    CheckpointCallback(10000,str(destination/'checkpoints'))]),reset_num_timesteps=False)
                policy.completed_lineage_episodes=config['parent_lineage_episodes']+500
                policy.save(destination/'final_model.zip')
                save_checkpoint_manifest(destination,'local-waypoint-v1')
            else:
                policy.policy.set_training_mode(False)
                for _ in range(count):
                    observation,_=environment.reset()
                    done=False
                    while not done:
                        action,_=policy.predict(observation,deterministic=True)
                        observation,_,terminal,truncated,_=environment.step(action)
                        done=terminal or truncated
                assert policy.num_timesteps==steps_before and policy._n_updates==updates_before
                assert all(torch.equal(policy.policy.state_dict()[key],value) for key,value in before.items())
            assert len(environment.records)==count
            if phase=='qa43-evaluation':
                verify_qa43_prefix(phase_dir)
            result=dict(status='complete',phase=phase,**summary(environment.records),model_steps=policy.num_timesteps,
                optimizer_updates=policy._n_updates,added_model_steps=policy.num_timesteps-steps_before,
                added_optimizer_updates=policy._n_updates-updates_before,frozen=not training)
            save(phase_dir/'result.json',result)
            save(destination/(phase+'-result.json'),result)
            if training:
                save(destination/'training-episodes.json',environment.records)
            if vector is not None:
                vector.close()
                vector=None
            else:
                environment.close()
            environment=None
            finish(phase_dir,process)
            retain(phase_dir,destination)
            print(json.dumps(result,indent=2),flush=True)
        assert model.num_timesteps-initial_steps==300000
        assert checkpoint_hash(parent)==config['resume_sha256'] and checkpoint_hash(old_path)==config['comparison_sha256']
        assert all(checkpoint_hash(GAME/name)==digest for name,digest in hashes.items())
        save(destination/'completion.json',dict(training_episodes=500,training_steps=300000,
            initial_model_steps=initial_steps,final_model_steps=model.num_timesteps,
            added_optimizer_updates=model._n_updates-initial_updates,total_lineage_episodes=config['parent_lineage_episodes']+500,
            final_sha256=checkpoint_hash(destination/'final_model.zip')))
        save(directory/'progress.json',dict(status='complete',training_episodes=500))
        print(f'Completed run: {destination}',flush=True)
    except BaseException as error:
        save(directory/'failure.json',dict(error=str(error)))
        raise
    finally:
        if vector is not None:
            vector.close()
        elif environment is not None:
            environment.close()
        if process is not None and process.poll() is None:
            process.terminate()
            process.wait(timeout=30)
        for path,content in settings.items():
            if path.read_bytes()!=content:
                path.write_bytes(content)


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--config',type=Path,required=True)
    parser.add_argument('--mode',choices=['verify','train'],default='verify')
    args=parser.parse_args()
    run(args.config.resolve(),args.mode)
