"""Read-only trace diagnosis and replay data for the finite QA-48 experiment."""

from collections import defaultdict
import csv
import gzip
from itertools import groupby
import json
from pathlib import Path

from run_local_movement import GAME, checkpoint_hash, save
import numpy as np
import torch
from stable_baselines3 import PPO


RUN = GAME / 'SavedModels/qa/local-waypoint-v1/qa-room-throughput-20261109-072306-train'
PARENT = GAME / 'SavedModels/qa/local-waypoint-v1/qa-two-room-20261109-070143-train/final_model.zip'
CASES = [453093, 453043, 453045, 453033, 453054]


def rows(path):
    return [json.loads(s) for s in path.read_text().splitlines()]


def means(model, observations):
    with torch.no_grad():
        tensor, _ = model.policy.obs_to_tensor(observations)
        return model.policy.get_distribution(tensor).distribution.mean.cpu().numpy()


def alignment(actions, directions):
    return np.sum(actions * directions, axis=1) / np.maximum(np.linalg.norm(actions, axis=1), 1e-8)


def main():
    output = RUN / 'behavior-analysis'; output.mkdir(exist_ok=True)
    parent_hash, child_hash = checkpoint_hash(PARENT), checkpoint_hash(RUN / 'final_model.zip')
    totals, details, replay, samples = {}, {}, {}, []
    for phase in ['parent-evaluation', 'child-evaluation']:
        episodes = {r['seed']: r for r in rows(RUN / phase / 'episodes.jsonl')}
        aggregate = defaultdict(int)
        phase_details = []
        with gzip.open(RUN / phase / 'actions.jsonl.gz', 'rt') as stream:
            grouped = groupby((json.loads(line) for line in stream), key=lambda r: r['seed'])
            for seed, records in grouped:
                records = list(records); ep = episodes[seed]
                assert len(records) == ep['steps'] == 600
                positions = np.array([[r['position']['x'], r['position']['z']] for r in records] + [[ep['position']['x'], ep['position']['z']]])
                observations = np.array([r['observation'] for r in records], dtype=np.float32)
                actions = np.array([r['action'] for r in records])
                moved = np.linalg.norm(np.diff(positions, axis=0), axis=1)
                intended = np.minimum(np.linalg.norm(actions, axis=1), 1) * .4
                guided = observations[:,130] == 1
                cosines = alignment(actions, observations[:,131:133])
                low = moved < .02
                strong = intended >= .2
                wall_values = observations[:,2:130:2]
                heading = np.mod(np.arctan2(actions[:,0], actions[:,1]), 2*np.pi)
                headings = np.mod(np.rint(heading * 64 / (2*np.pi)).astype(int),64)
                nearby_wall_in_command = np.zeros(600,dtype=bool)
                for offset in [-1,0,1]:
                    values = wall_values[np.arange(600),(headings+offset)%64]
                    nearby_wall_in_command |= (values > 0) & ((1-values)*12 <= .5)
                pinned = low & strong
                into_wall = pinned & nearby_wall_in_command
                stats = dict(seed=seed,new_rooms=ep['new_rooms'],actions=600,
                    low_motion_actions=int(low.sum()),strong_commands_low_motion=int(pinned.sum()),
                    wall_contact_commands=int(into_wall.sum()),weak_commands=int((intended < .05).sum()),
                    no_waypoint_actions=int((~guided).sum()),guided_actions=int(guided.sum()),
                    guided_away_actions=int(((cosines <= 0)&guided).sum()),
                    low_motion_while_guided=int((low&guided).sum()),
                    total_distance=float(moved.sum()),
                    last100_low_motion=int(low[-100:].sum()),
                    last100_wall_contact_commands=int(into_wall[-100:].sum()))
                assert stats['low_motion_actions'] == ep['low_movement_actions']
                phase_details.append(stats)
                for k,v in stats.items():
                    if k not in ['seed','new_rooms','total_distance']: aggregate[k] += v
                aggregate['no_new_room_cases'] += ep['new_rooms']==0
                aggregate['cases_majority_final100_low_motion'] += stats['last100_low_motion']>=50
                if phase == 'child-evaluation': samples.extend(observations[::5])
                if seed in CASES:
                    packed=[]
                    for i,r in enumerate(records):
                        hits=sorted([(j,float((1-v)*12)) for j,v in enumerate(wall_values[i]) if v>0],key=lambda t:t[1])[:3]
                        hits += [(-1,0)]*(3-len(hits))
                        packed.append([round(float(v),3) for v in [*positions[i],r['waypoint']['x'],r['waypoint']['z'],*actions[i]]]+
                            [int(r['has_waypoint']),r['new_rooms'],r['goal_room'],r['waypoint_version'],round(float(moved[i]),4)]+
                            [v for j,d in hits for v in [j,round(d,3)]])
                    last=packed[-1].copy(); last[:2]=[round(float(v),3) for v in positions[-1]]; last[4:6]=[0,0];last[7]=ep['new_rooms'];last[10]=0;last[11:]=[-1,0]*3
                    packed.append(last)
                    replay.setdefault(seed,{})['before' if phase=='parent-evaluation' else 'after']={'frames':packed,'stats':stats}
        totals[phase]=dict(aggregate);details[phase]=phase_details

    torch.set_num_threads(1)
    parent=PPO.load(PARENT,device='cpu');child=PPO.load(RUN/'final_model.zip',device='cpu')
    parent.policy.set_training_mode(False);child.policy.set_training_mode(False)
    sample=np.array(samples,dtype=np.float32);valid=sample[:,130]==1;sample=sample[valid]
    old_mean=means(parent,sample);new_mean=means(child,sample)
    zero_history=sample.copy();zero_history[:,:2]=0
    zero_mean=means(child,zero_history)
    probes={}
    for name,values in [('QA47_same_states',old_mean),('QA48_same_states',new_mean),('QA48_zero_previous_action',zero_mean)]:
        action=np.clip(values,-1,1);cos=alignment(action,sample[:,131:133])
        probes[name]=dict(observations=len(sample),mean_target_cosine=float(cos.mean()),
            fraction_away_or_perpendicular=float(np.mean(cos<=0)),fraction_toward_within60degrees=float(np.mean(cos>=.5)),
            fraction_action_component_saturated=float(np.mean(np.any(np.abs(action)>=.999,axis=1))))
    probes['parent_action_std']=np.exp(parent.policy.log_std.detach().cpu().numpy()).tolist()
    probes['child_action_std']=np.exp(child.policy.log_std.detach().cpu().numpy()).tolist()
    probes['mean_command_change_on_identical_states']=float(np.linalg.norm(np.clip(old_mean,-1,1)-np.clip(new_mean,-1,1),axis=1).mean())
    for x in rows(RUN/'child-evaluation/layouts.jsonl'):
        if x['seed'] not in CASES: continue
        byz=defaultdict(list)
        for p in x['floors']:byz[round(p['z'])].append(round(p['x']))
        runs=[]
        for z,xs in sorted(byz.items()):
            for _,g in groupby(enumerate(sorted(set(xs))),key=lambda t:t[1]-t[0]):
                values=[v for _,v in g];runs.append([values[0],z,len(values)])
        replay[x['seed']].update(seed=x['seed'],floor=runs,centers=[[r['x'],r['z']] for r in x['room_centers']],spawn=[x['spawn']['x'],x['spawn']['z']])
    result=dict(status='complete',source_run=str(RUN),parent_sha256=parent_hash,child_sha256=child_hash,
        thresholds=dict(low_motion=.02,strong_command_expected_step=.2,wall_nearby=.5),totals=totals,
        cases=details,offline_probes=probes,no_new_training=True,
        probe_limit='Same recorded states only; zeroing history is a sensitivity probe, not a tested closed-loop fix.')
    save(output/'analysis.json',result)
    save(output/'replay-data.json',dict(cases=[replay[s] for s in CASES]))
    assert checkpoint_hash(PARENT)==parent_hash and checkpoint_hash(RUN/'final_model.zip')==child_hash
    print(json.dumps(dict(totals=totals,offline_probes=probes,selected_cases=[dict(seed=s,before=replay[s]['before']['stats'],after=replay[s]['after']['stats']) for s in CASES]),indent=2),flush=True)


if __name__=='__main__':main()
