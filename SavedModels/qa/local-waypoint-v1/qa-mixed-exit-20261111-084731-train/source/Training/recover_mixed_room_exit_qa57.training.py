"""Finish QA-57's remaining authorized episodes after its Unity disconnect."""
from pathlib import Path
import json
import shutil
import torch
from stable_baselines3 import PPO
from stable_baselines3.common.callbacks import CallbackList, CheckpointCallback, StopTrainingOnMaxEpisodes
from stable_baselines3.common.logger import configure
from stable_baselines3.common.monitor import Monitor
from stable_baselines3.common.vec_env import DummyVecEnv
from run_local_movement import GAME, checkpoint_hash, save, save_checkpoint_manifest, load_config
from run_waypoint_chain import records, ready, finish, retain, launch as launch_chain
from run_waypoint_throughput import ThroughputEnvironment, summary as waypoint_summary
from train_two_room_navigation import LineageCallback, frozen_phase
from train_mixed_room_exit import MixedEnvironment, launch, frozen, summary
from evaluate_two_room_navigation import summary as exit_summary

runtime = GAME/'Logs/qa-mixed-exit/20261111-084731-train'
destination = GAME/'SavedModels/qa/local-waypoint-v1/qa-mixed-exit-20261111-084731-train'
config = load_config(destination/'training-config.yaml')
metadata = json.loads((destination/'run.json').read_text(encoding='utf-8'))
prefix = records(runtime/'training/episodes.jsonl')
assert len(prefix) == 1651 and [r['seed'] for r in prefix] == list(range(25800,27451))
assert config['max_episodes'] == 3000 and metadata['status'] == 'training'
checkpoint = destination/'checkpoints/rl_model_2197594_steps.zip'
assert checkpoint_hash(checkpoint) == '1d58c5f4d4b76b0c4d57ad8844678a518a3c6944580ce097c8d0a275ee2e8fcd'
assert all(checkpoint_hash(GAME/p) == h for p,h in metadata['source_sha256'].items())
assert not (runtime/'training-recovery-01').exists()
torch.set_num_threads(1)
model = PPO.load(checkpoint, device='cpu', seed=27451)
assert (model.num_timesteps,model._n_updates,model.completed_lineage_episodes) == (2197594,85770,13325)
assert model.learning_rate == .000002 and all(g['lr'] == .000002 for g in model.policy.optimizer.param_groups)
remaining = 3000-len(prefix)
recovery = dict(status='running', cause='Unity disconnected; cause not established',
    completed_before_disconnect=len(prefix), remaining_episodes=remaining, next_seed=27451,
    checkpoint=str(checkpoint), checkpoint_sha256=checkpoint_hash(checkpoint),
    checkpoint_steps=model.num_timesteps, checkpoint_optimizer_epochs=model._n_updates,
    checkpoint_completed_campaign_episodes=1625, completed_after_checkpoint=26,
    interrupted_seed=27451, interrupted_partial_actions=30,
    completed_prefix_actions=sum(r['steps'] for r in prefix), prefix_executed_actions=294281,
    executed_actions_after_checkpoint_not_retained=4281,
    completed_episode_actions_beyond_retained_counter=4251,
    checkpoint_pending_rollout_transitions_discarded=208,
    full_actor_critic_optimizer_continuation=True, repeated_completed_episodes=False)
save(destination/'recovery.json',recovery)
source = Path(__file__).resolve()
target = destination/'source/Training'/source.name
shutil.copy2(source,target)
metadata['recovery_source_sha256']={str(source.relative_to(GAME)):checkpoint_hash(source)}
metadata['status']='recovering';save(destination/'run.json',metadata)
settings={GAME/'ProjectSettings'/p.name:p.read_bytes() for p in (runtime/'original-settings').iterdir()}
results={name:json.loads((destination/(name+'-result.json')).read_text(encoding='utf-8'))
         for name in ['verification','parent-evaluation']}
results['parent-two-room']=exit_summary(records(destination/'parent-two-room/episodes.jsonl'))
process=environment=vector=None
try:
    output=runtime/'training-recovery-01'
    process=launch(output,True);ready(output,process)
    environment=MixedEnvironment(output,remaining,27451,True)
    vector=DummyVecEnv([lambda:Monitor(environment)])
    model.set_env(vector);model.set_logger(configure(str(output),['csv']))
    model.learn(remaining*600,reset_num_timesteps=False,callback=CallbackList([
        LineageCallback(environment,11700+len(prefix)),
        CheckpointCallback(5000,str(destination/'recovery-checkpoints')),
        StopTrainingOnMaxEpisodes(remaining)]))
    tail=environment.records
    assert len(tail)==remaining and model.num_timesteps-2197594==sum(r['steps'] for r in tail)
    model.completed_lineage_episodes=14700
    model.save(destination/'final_model.zip');save_checkpoint_manifest(destination,'local-waypoint-v1')
    recovery.update(status='training-complete',recovery_actions=sum(r['steps'] for r in tail),
        recovery_discarded_partial_rollout_transitions=sum(r['steps'] for r in tail)%256)
    save(destination/'recovery.json',recovery)
    vector.close();vector=environment=None;finish(output,process);process=None
    # Keep raw segments separate; the campaign aggregate contains each completed seed once.
    retain(runtime/'training',destination/'segments')
    retain(output,destination/'segments')
    rows=prefix+tail
    layouts=records(runtime/'training/layouts.jsonl')[:len(prefix)]+records(output/'layouts.jsonl')
    assert len(rows)==len(layouts)==3000
    assert [r['seed'] for r in rows]==[r['seed'] for r in layouts]==list(range(25800,28800))
    aggregate=destination/'training';aggregate.mkdir()
    for name,data in [('episodes',rows),('layouts',layouts)]:
        (aggregate/(name+'.jsonl')).write_text(''.join(json.dumps(r)+'\n' for r in data),encoding='utf-8')
    result=dict(status='complete',frozen=False,**summary(rows),model_steps=model.num_timesteps,
        optimizer_epochs=model._n_updates,added_model_steps=model.num_timesteps-1907594,
        added_optimizer_epochs=model._n_updates-74450,recovered=True,
        discarded_partial_rollout_transitions=recovery['recovery_discarded_partial_rollout_transitions'],
        interrupted_checkpoint_pending_rollout_transitions_discarded=208)
    save(aggregate/'result.json',result);save(destination/'training-result.json',result)
    results['training']=result
    metadata['status']='evaluation';save(destination/'run.json',metadata)
    output=runtime/'child-evaluation'
    process=launch(output,False);ready(output,process)
    environment=MixedEnvironment(output,150,481000,False)
    weights={k:v.clone() for k,v in model.policy.state_dict().items()};counters=model.num_timesteps,model._n_updates
    model.policy.set_training_mode(False)
    for _ in range(150):
        observation,_=environment.reset();done=False
        while not done:
            action,_=model.predict(observation,deterministic=True)
            observation,_,terminal,truncated,_=environment.step(action);done=terminal or truncated
    frozen(model,weights,counters)
    results['child-evaluation']=dict(status='complete',frozen=True,**summary(environment.records),
        model_steps=model.num_timesteps,optimizer_epochs=model._n_updates,added_model_steps=0,added_optimizer_epochs=0)
    environment.close();environment=None;finish(output,process);process=None
    save(output/'result.json',results['child-evaluation']);retain(output,destination)
    save(destination/'child-evaluation-result.json',results['child-evaluation'])
    assert records(destination/'parent-evaluation/layouts.jsonl')==records(output/'layouts.jsonl')
    rows=frozen_phase(runtime,'child-two-room',range(451000,451100),model)
    frozen(model,weights,counters);retain(runtime/'child-two-room',destination)
    results['child-two-room']=exit_summary(rows)
    output=runtime/'empty-room-retention'
    process=launch_chain(output,dict(min_waypoints=10,max_waypoints=10),False,True,continuous=True)
    ready(output,process);environment=ThroughputEnvironment(output,{'port':7783},100,288000,False)
    for _ in range(100):
        observation,_=environment.reset();done=False
        while not done:
            action,_=model.predict(observation,deterministic=True)
            observation,_,terminal,truncated,_=environment.step(action);done=terminal or truncated
    frozen(model,weights,counters)
    parent=Path(metadata['parent_checkpoint']).parent
    assert records(output/'chains.jsonl')==records(parent/'empty-room-retention/chains.jsonl')
    results['empty-room-retention']=waypoint_summary(environment.records)
    save(output/'result.json',results['empty-room-retention'])
    environment.close();environment=None;finish(output,process);process=None;retain(output,destination)
    assert checkpoint_hash(Path(metadata['parent_checkpoint']))==metadata['parent_sha256']
    assert all(checkpoint_hash(GAME/p)==h for p,h in metadata['source_sha256'].items())
    assert checkpoint_hash(source)==metadata['recovery_source_sha256'][str(source.relative_to(GAME))]
    actions=sum(r['steps'] for r in prefix)+recovery['recovery_actions']
    save(destination/'completion.json',dict(status='complete',training_episodes=3000,
        training_actions=actions,executed_training_actions=actions+30,
        retained_model_actions=model.num_timesteps-1907594,final_model_steps=model.num_timesteps,
        final_optimizer_epochs=model._n_updates,final_lineage_episodes=14700,
        final_sha256=checkpoint_hash(destination/'final_model.zip'),recovered=True,results=results))
    recovery['status']='complete';save(destination/'recovery.json',recovery)
    metadata['status']='complete';save(destination/'run.json',metadata)
    save(runtime/'progress.json',dict(status='complete',training_episodes=3000,recovered=True))
    print('Completed recovered campaign: '+str(destination),flush=True)
except BaseException as error:
    save(destination/'recovery-failure.json',dict(error=str(error),final_model_exists=(destination/'final_model.zip').exists()))
    raise
finally:
    if vector is not None:vector.close()
    elif environment is not None:environment.close()
    if process is not None and process.poll() is None:process.terminate();process.wait(timeout=30)
    for p,data in settings.items():
        if p.read_bytes()!=data:p.write_bytes(data)
