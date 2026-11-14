"""Continue the configured checkpoint for the explicit mixed actual-exit episode budget."""

from collections import Counter
from datetime import datetime, timezone
from pathlib import Path
import argparse
import json
import shutil
import subprocess

from run_local_movement import GAME, checkpoint_hash, load_config, save, save_checkpoint_manifest
from run_waypoint_chain import ready, finish, retain, records, launch as launch_chain
from run_waypoint_throughput import ThroughputEnvironment, summary as waypoint_summary
from train_two_room_navigation import TwoRoomTrainingEnvironment, LineageCallback, frozen_phase
from evaluate_two_room_navigation import SOURCES, summary as exit_summary
from test_waypoint_reach import UNITY
from unity_env import UnityGymEnv
import numpy as np
import torch
from stable_baselines3 import PPO
from stable_baselines3.common.callbacks import CallbackList, CheckpointCallback, StopTrainingOnMaxEpisodes
from stable_baselines3.common.logger import configure
from stable_baselines3.common.monitor import Monitor
from stable_baselines3.common.vec_env import DummyVecEnv


TYPES = ['regular', 'winding', 'middle_walls', 'sharp_corners', 'L_shape']


def summary(rows):
    result = exit_summary(rows)
    result['by_room_count'] = {str(n): dict(episodes=sum(r['room_count'] == n for r in rows),
        exits=sum(r['room_count'] == n and r['completed'] for r in rows)) for n in range(1,4)}
    result['by_first_room_type'] = {kind: dict(episodes=sum(r['room_types'][0] == kind for r in rows),
        exits=sum(r['room_types'][0] == kind and r['completed'] for r in rows)) for kind in TYPES}
    result['generated_room_types'] = dict(Counter(kind for r in rows for kind in r['room_types']))
    return result


class MixedEnvironment(TwoRoomTrainingEnvironment):
    def __init__(self, directory, count, seed, rewards):
        super().__init__(directory, count, seed)
        self.rewards = rewards

    def step(self, action):
        observation, reward, terminal, truncated, info = UnityGymEnv.step(self, action)
        self.steps += 1; self.total_reward += reward
        assert self.steps <= 600 and np.all(np.isfinite(observation)) and np.all(np.abs(observation) <= 1.00001)
        if terminal or truncated:
            row = records(self.directory / 'episodes.jsonl')[-1]
            assert row['seed'] == self.first_seed + len(self.records) and row['steps'] == self.steps
            assert row['mixed_rooms'] and 1 <= row['room_count'] <= 3
            assert len(row['room_types']) == row['room_count'] and set(row['room_types']) <= set(TYPES)
            assert row['training_rewards'] == self.rewards and row['positive_waypoint_reward'] <= 5.00001
            expected = row['positive_waypoint_reward'] - .001*self.steps + 50*row['completed'] if self.rewards else 0
            assert abs(expected-self.total_reward) < .0005
            assert abs(row['total_reward']-self.total_reward) < .001
            assert row['completed'] or self.steps == 600
            if row['completed']:
                assert row['final_phase'] == 'exit_room' and abs(row['position']['y']) < 1.5
            row['python_return'] = self.total_reward
            self.records.append(row); self.terminal = observation
            if len(self.records) % 10 == 0 or len(self.records) == self.count:
                progress = dict(phase=self.directory.name, target=self.count, **summary(self.records))
                save(self.directory.parent/'progress.json', progress)
                print(json.dumps(progress), flush=True)
        return observation, reward, terminal, truncated, info


def launch(directory, rewards):
    directory.mkdir()
    save(directory/'session.json',dict(training_rewards=rewards, mixed_rooms=True))
    process = subprocess.Popen([str(UNITY), '-batchmode', '-nographics', '-projectPath', str(GAME),
        '-executeMethod', 'QaTwoRoomNavigationSession.Run', '-trainingRunDirectory', str(directory),
        '-logFile', str(directory/'unity.log')], cwd=GAME, creationflags=subprocess.CREATE_NO_WINDOW)
    save(directory/'process.json',dict(unity_pid=process.pid))
    return process


def frozen(model, weights, counters):
    assert counters == (model.num_timesteps,model._n_updates)
    assert all(torch.equal(value,model.policy.state_dict()[key]) for key,value in weights.items())


def run(config_path):
    config = load_config(config_path)
    episodes = config['max_episodes']
    assert type(episodes) is int and episodes > 0
    assert config['total_timesteps'] == episodes * 600
    assert config['room_count_range'] == [1,3] and config['room_types'] == TYPES
    assert config['learning_rate'] == .000002 and config['episode_seconds'] == 60
    parent = (config_path.parent/config['resume_from']).resolve()
    assert checkpoint_hash(parent) == config['resume_sha256']
    torch.set_num_threads(1)
    model = PPO.load(parent,device='cpu',seed=config['seed'])
    assert model.observation_contract == 'local-waypoint-v1' and model.learning_rate == config.get('parent_learning_rate',.00002)
    assert (model.gamma,model.ent_coef,model.n_steps,model.batch_size) == (.99,.01,256,64)
    initial_steps,initial_updates = model.num_timesteps,model._n_updates
    initial_learning_rate = model.learning_rate
    parent_episodes = config['parent_lineage_episodes']
    model.learning_rate = config['learning_rate']; model.lr_schedule = lambda _: config['learning_rate']
    for group in model.policy.optimizer.param_groups: group['lr'] = config['learning_rate']
    stamp = datetime.now(timezone.utc).strftime('%Y%m%d-%H%M%S')+'-train'
    directory = (config_path.parent/config['output_dir']).resolve()/stamp
    destination = GAME/'SavedModels/qa/local-waypoint-v1'/('qa-mixed-exit-'+stamp)
    directory.mkdir(parents=True); destination.mkdir()
    print('Runtime directory: '+str(directory),flush=True)
    print('Retained directory: '+str(destination),flush=True)
    source_names = list(dict.fromkeys(SOURCES+['Assets/Editor/QaMixedRoomLayout.cs',
        'Assets/Editor/QaMixedRoomLayout.cs.meta','Training/train_mixed_room_exit.py',
        'Training/train_two_room_navigation.py','Training/run_waypoint_chain.py',
        'Training/run_waypoint_throughput.py','Assets/Editor/QaWaypointChainSession.cs',
        'Assets/Editor/QaTwistingRoomDrill.cs','Assets/Scenes/QaWaypoint40Training.unity',
        'Assets/PCGDungeon/Scripts/Generation/PcgGenerationParameters.cs',
        'Assets/PCGDungeon/Scripts/Policy/QaLocalMovementPolicyMapping.cs',
        'Assets/PCGDungeon/Scripts/QA/QaNavigationCoordinator.cs']))
    hashes = {s:checkpoint_hash(GAME/s) for s in source_names}
    shutil.copy2(config_path,destination/'training-config.yaml')
    metadata = dict(status='preflight',qa_id=config.get('qa_id','QA-52'),parent_checkpoint=str(parent),parent_sha256=config['resume_sha256'],
        parent_model_steps=initial_steps,parent_optimizer_epochs=initial_updates,parent_lineage_episodes=parent_episodes,
        learning_rate=.000002,previous_learning_rate=initial_learning_rate,full_actor_critic_optimizer_continuation=True,
        training_episodes=episodes,training_seed_range=[config['seed'],config['seed']+episodes-1],
        evaluation_seed_range=[config['evaluation_seed'],config['evaluation_seed']+149],
        one_active_waypoint=True,waypoint_cap=10,seconds_limit=60,rooms=[1,3],room_types=TYPES,
        sampling='Stratified room count and first-room type by seed; seeded random geometry, orientation, connectors and later-room types',
        actual_exit_in_final_room=True,room_resets=False,
        reward='+50 actual exit, shared +5 waypoint progress/arrival cap, -0.001/action',source_sha256=hashes)
    save(destination/'run.json',metadata)
    for s in source_names:
        target=destination/'source'/s;target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(GAME/s,target)
    settings={p:p.read_bytes() for p in (GAME/'ProjectSettings').iterdir() if p.is_file()}
    for p,data in settings.items():
        target=directory/'original-settings'/p.name;target.parent.mkdir(exist_ok=True);target.write_bytes(data)
    process=environment=vector=None
    results={}
    try:
        for name in ['verification','parent-evaluation','training','child-evaluation']:
            training=name=='training';rewards=training or name=='verification'
            count=16 if name=='verification' else (episodes if training else 150)
            seed=config['verification_seed'] if name=='verification' else (config['seed'] if training else config['evaluation_seed'])
            output=directory/name
            if training:
                metadata['status']='training';save(destination/'run.json',metadata)
            process=launch(output,rewards);ready(output,process)
            environment=MixedEnvironment(output,count,seed,rewards)
            weights={k:v.clone() for k,v in model.policy.state_dict().items()};counters=model.num_timesteps,model._n_updates
            if training:
                vector=DummyVecEnv([lambda:Monitor(environment)])
                model.set_env(vector);model.set_logger(configure(str(output),['csv']))
                model.training_task='mixed-1to3-room-actual-exit'
                model.learn(config['total_timesteps'],reset_num_timesteps=False,callback=CallbackList([
                    LineageCallback(environment,parent_episodes),CheckpointCallback(5000,str(destination/'checkpoints')),
                    StopTrainingOnMaxEpisodes(episodes)]))
                assert len(environment.records)==episodes
                assert model.num_timesteps-initial_steps==sum(r['steps'] for r in environment.records)
                model.completed_lineage_episodes=parent_episodes+episodes
                model.save(destination/'final_model.zip');save_checkpoint_manifest(destination,'local-waypoint-v1')
                metadata['status']='evaluation';save(destination/'run.json',metadata)
            else:
                model.policy.set_training_mode(False)
                for episode in range(count):
                    observation,_=environment.reset();done=False
                    while not done:
                        if name=='verification':
                            if episode==count-1:action=np.zeros(2,dtype=np.float32)
                            else:action=observation[131:133]
                        else:action,_=model.predict(observation,deterministic=True)
                        observation,_,terminal,truncated,_=environment.step(action);done=terminal or truncated
                frozen(model,weights,counters)
            rows=environment.records
            layouts=records(output/'layouts.jsonl');assert len(layouts)==count
            assert all(r['route_available'] and 1<=r['rooms']<=3 and r['mixed_rooms'] for r in layouts)
            if name=='verification':
                assert all(r['completed'] for r in rows[:15])
                assert not rows[-1]['completed'] and rows[-1]['positive_waypoint_reward']==0
                assert {(r['room_count'],r['room_types'][0]) for r in rows[:15]}=={(n,t) for n in range(1,4) for t in TYPES}
            if name=='parent-evaluation' and 'parent_mixed_phase' in config:
                previous=records(parent.parent/config['parent_mixed_phase']/'episodes.jsonl')
                assert [(r['seed'],r['completed'],r['steps']) for r in rows]==[(r['seed'],r['completed'],r['steps']) for r in previous]
            result=dict(status='complete',frozen=not training,**summary(rows),model_steps=model.num_timesteps,
                optimizer_epochs=model._n_updates,added_model_steps=model.num_timesteps-counters[0],
                added_optimizer_epochs=model._n_updates-counters[1])
            if training:result['discarded_partial_rollout_transitions']=(model.num_timesteps-initial_steps)%256
            if vector is not None:vector.close();vector=None
            else:environment.close()
            environment=None;finish(output,process);process=None
            save(output/'result.json',result);retain(output,destination)
            save(destination/(name+'-result.json'),result);results[name]=result
            if name=='verification':
                # Confirm the opt-in layout path leaves the legacy dungeon benchmark unchanged.
                old=frozen_phase(directory,'parent-two-room',range(451000,451100),model)
                previous=records(parent.parent/config.get('parent_legacy_phase','after-400-two-room')/'episodes.jsonl')
                assert [(r['completed'],r['steps']) for r in old]==[(r['completed'],r['steps']) for r in previous]
                retain(directory/'parent-two-room',destination)
                results['parent-two-room']=exit_summary(old)
            print(json.dumps(dict(phase=name,**result)),flush=True)
        assert records(directory/'parent-evaluation/layouts.jsonl')==records(directory/'child-evaluation/layouts.jsonl')
        weights={k:v.clone() for k,v in model.policy.state_dict().items()};counters=model.num_timesteps,model._n_updates
        rows=frozen_phase(directory,'child-two-room',range(451000,451100),model)
        frozen(model,weights,counters);retain(directory/'child-two-room',destination)
        results['child-two-room']=exit_summary(rows)
        output=directory/'empty-room-retention'
        process=launch_chain(output,dict(min_waypoints=10,max_waypoints=10),False,True,continuous=True)
        ready(output,process);environment=ThroughputEnvironment(output,{'port':7783},100,288000,False)
        for _ in range(100):
            observation,_=environment.reset();done=False
            while not done:
                action,_=model.predict(observation,deterministic=True)
                observation,_,terminal,truncated,_=environment.step(action);done=terminal or truncated
        frozen(model,weights,counters)
        assert records(output/'chains.jsonl')==records(parent.parent/config.get('parent_empty_phase','after-400-empty')/'chains.jsonl')
        results['empty-room-retention']=waypoint_summary(environment.records)
        save(output/'result.json',results['empty-room-retention'])
        environment.close();environment=None;finish(output,process);process=None;retain(output,destination)
        assert checkpoint_hash(parent)==config['resume_sha256']
        assert all(checkpoint_hash(GAME/s)==value for s,value in hashes.items())
        assert all(group['lr']==.000002 for group in model.policy.optimizer.param_groups)
        save(destination/'completion.json',dict(status='complete',training_episodes=episodes,
            training_actions=model.num_timesteps-initial_steps,final_model_steps=model.num_timesteps,
            final_optimizer_epochs=model._n_updates,final_lineage_episodes=parent_episodes+episodes,
            final_sha256=checkpoint_hash(destination/'final_model.zip'),results=results))
        metadata['status']='complete';save(destination/'run.json',metadata)
        save(directory/'progress.json',dict(status='complete',training_episodes=episodes))
        print('Completed run: '+str(destination),flush=True)
    except BaseException as error:
        save(destination/'failure.json',dict(error=str(error),final_model_exists=(destination/'final_model.zip').exists()))
        raise
    finally:
        if vector is not None:vector.close()
        elif environment is not None:environment.close()
        if process is not None and process.poll() is None:process.terminate();process.wait(timeout=30)
        for path,data in settings.items():
            if path.read_bytes()!=data:path.write_bytes(data)


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--config',type=Path,required=True)
    args=parser.parse_args();run(args.config.resolve())
