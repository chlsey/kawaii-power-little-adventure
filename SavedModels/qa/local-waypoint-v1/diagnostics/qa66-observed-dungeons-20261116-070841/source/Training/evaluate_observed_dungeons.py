"""100 fresh generated two-room cases with frozen QA-50 and observed-map navigation."""

from collections import Counter
from datetime import datetime, timezone
from pathlib import Path
import gzip
import hashlib
import json
import shutil

import torch
from stable_baselines3 import PPO
from run_local_movement import GAME, checkpoint_hash, save
from evaluate_two_room_navigation import SOURCES, phase, summary
from run_waypoint_chain import records


SEEDS = list(range(530000, 530100))
CHECKPOINT = GAME/'SavedModels/qa/local-waypoint-v1/qa-waypoint-recovery-20261109-224625-train/final_model.zip'
SHA256 = 'acffd0594bb6a03a157e2853688dfc4d7647606d052cb241fe620d9f8eb4db64'


def geometry_key(layout):
    # Remove translation and quarter-turn orientation when auditing repeated geometry.
    spawn = layout['spawn']
    points = [(round((p['x']-spawn['x'])*1000),round((p['z']-spawn['z'])*1000)) for p in layout['floors']]
    goal = (round((layout['exit']['x']-spawn['x'])*1000),round((layout['exit']['z']-spawn['z'])*1000))
    versions = []
    for _ in range(4):
        versions.append(json.dumps([sorted(points),goal],separators=(',',':')))
        points = [(-z,x) for x,z in points]
        goal = (-goal[1],goal[0])
    return hashlib.sha256(min(versions).encode()).hexdigest()


def observed_summary(rows):
    return dict(**summary(rows),exit_discoveries=sum(r['exit_discovered'] for r in rows),
        planner_builds=sum(r['planner_builds'] for r in rows),
        goal_selections=sum(r['goal_selections'] for r in rows),
        route_searches=sum(r['route_searches'] for r in rows),
        abandoned_goals=sum(r['abandoned_goals'] for r in rows),
        outcomes_by_orientation={str(i*90):dict(cases=sum(r['orientation']==i for r in rows),
            exits=sum(r['orientation']==i and r['completed'] for r in rows)) for i in range(4)})


def retain_phase(directory, destination, name):
    source = directory/name
    target = destination/name
    target.mkdir(exist_ok=True)
    for file in source.glob('*.json*'):
        if file.name == 'navigation-events.jsonl':
            with file.open('rb') as src,gzip.open(target/(file.name+'.gz'),'wb') as dst:
                shutil.copyfileobj(src,dst)
        else: shutil.copy2(file,target/file.name)


def run():
    assert checkpoint_hash(CHECKPOINT) == SHA256
    torch.set_num_threads(1)
    model = PPO.load(CHECKPOINT,device='cpu')
    assert model.observation_contract == 'local-waypoint-v1'
    weights = {k:v.clone() for k,v in model.policy.state_dict().items()}
    counters = model.num_timesteps,model._n_updates
    stamp = datetime.now(timezone.utc).strftime('%Y%m%d-%H%M%S')
    directory = GAME/'Logs/qa-observed-dungeons'/stamp
    destination = GAME/'SavedModels/qa/local-waypoint-v1/diagnostics'/('qa66-observed-dungeons-'+stamp)
    directory.mkdir(parents=True);destination.mkdir(parents=True)
    source_names = list(dict.fromkeys(SOURCES+[
        'Assets/PCGDungeon/Scripts/QA/QaNavigationCoordinator.cs',
        'Assets/PCGDungeon/Scripts/QA/ModelSensorGrid.cs',
        'Assets/PCGDungeon/Scripts/Generation/PcgDungeonManager.cs',
        'Training/evaluate_observed_dungeons.py']))
    hashes = {name:checkpoint_hash(GAME/name) for name in source_names}
    shutil.copytree(GAME/'ProjectSettings',directory/'original-settings')
    campaign = dict(qa_id='QA-66',status='reference-preflight',frozen_cases=100,seeds=SEEDS,
        checkpoint='QA-50',checkpoint_sha256=SHA256,model_steps=counters[0],optimizer_epochs=counters[1],
        lineage_episodes=4700,new_training_episodes=0,
        navigation='QaNavigationCoordinator on observed sensor memory only; no oracle constructed',
        generation='PcgDungeonManager -> Generator -> DungeonValidator; 2 regular 20x20 rooms in 100x100 area',
        quarter_turn_rotations=25,episode_seconds=60,waypoint_cap=10,
        enemies=0,puzzles=0,training_rewards=False,source_sha256=hashes,runtime=str(directory))
    save(destination/'campaign.json',campaign)
    print('Retained campaign: '+str(destination),flush=True)
    try:
        # Reference steering checks the same observed planner; navigation failures are results, not a gate.
        reference = phase(directory,'reference-preflight',SEEDS[:4],observed=True)
        retain_phase(directory,destination,'reference-preflight')
        campaign.update(status='policy-evaluation',reference_preflight=observed_summary(reference))
        save(destination/'campaign.json',campaign)
        rows = phase(directory,'evaluation',SEEDS,model=model,observed=True)
        retain_phase(directory,destination,'evaluation')
        layouts = records(directory/'evaluation/layouts.jsonl')
        assert all(r['observed_navigation'] and r['rooms']==2 and not r['mixed_rooms'] for r in layouts)
        assert Counter(r['orientation'] for r in layouts)==Counter({0:25,1:25,2:25,3:25})
        failed = [r['seed'] for r in rows if not r['completed']]
        campaign.update(status='failure-reference',evaluation=observed_summary(rows),failed_seeds=failed)
        save(destination/'campaign.json',campaign)
        controls = {r['seed']:r for r in reference if r['seed'] in failed}
        remaining = [seed for seed in failed if seed not in controls]
        if remaining:
            replay = phase(directory,'failure-reference',remaining,observed=True)
            retain_phase(directory,destination,'failure-reference')
            controls.update({r['seed']:r for r in replay})
        expected = {r['seed']:geometry_key(r) for r in layouts}
        for name in ['reference-preflight','failure-reference']:
            if (directory/name/'layouts.jsonl').exists():
                for layout in records(directory/name/'layouts.jsonl'):
                    assert geometry_key(layout)==expected[layout['seed']]
        counts = Counter(expected.values())
        baseline_path = GAME/'SavedModels/qa/local-waypoint-v1/diagnostics/two-room-navigation-20261109-044803-evaluate/evaluation/layouts.jsonl'
        old_keys = set()
        if baseline_path.exists(): old_keys = {geometry_key(r) for r in records(baseline_path)}
        events = Counter(r['reason'] for r in records(directory/'evaluation/navigation-events.jsonl'))
        result = dict(status='complete',frozen=True,weight_updates=0,new_training_steps=0,
            evaluation=observed_summary(rows),initially_visible_exits=sum(r['initial_exit_visible'] for r in layouts),
            unique_translation_rotation_normalized_layouts=len(counts),
            overlap_with_qa45_layouts=sum(key in old_keys for key in expected.values()) if old_keys else None,
            navigation_events=dict(events),
            failed_cases_cleared_by_reference=[seed for seed in failed if controls[seed]['completed']],
            failed_cases_also_failed_reference=[seed for seed in failed if not controls[seed]['completed']],
            control_results=list(controls.values()),all_control_layouts_matched=True)
        assert counters==(model.num_timesteps,model._n_updates)
        assert all(torch.equal(v,model.policy.state_dict()[k]) for k,v in weights.items())
        assert checkpoint_hash(CHECKPOINT)==SHA256
        assert all(checkpoint_hash(GAME/name)==digest for name,digest in hashes.items())
        save(destination/'result.json',result)
        for name in source_names:
            target=destination/'source'/name;target.parent.mkdir(parents=True,exist_ok=True)
            shutil.copy2(GAME/name,target)
        campaign.update(status='complete',all_weights_unchanged=True,source_hashes_verified=True)
        print(json.dumps(result,indent=2),flush=True)
    except BaseException as error:
        campaign.update(status='failed',error=str(error))
        raise
    finally:
        for backup in (directory/'original-settings').iterdir():
            if backup.is_file() and (GAME/'ProjectSettings'/backup.name).read_bytes()!=backup.read_bytes():
                shutil.copy2(backup,GAME/'ProjectSettings'/backup.name)
        campaign['settings_restored']=True
        save(destination/'campaign.json',campaign)


if __name__=='__main__':run()
