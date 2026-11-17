"""Frozen matched pilot for retaining physically clear active waypoint segments."""
from pathlib import Path
import sys
GAME = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(GAME/'Training'))
import gzip, json, shutil, subprocess, time
from datetime import datetime, timezone
import torch
from stable_baselines3 import PPO
from evaluate_two_room_navigation import phase, SOURCES
from evaluate_navigation_clearance import CHECKPOINT, SHA256, geometry_key, retain_phase, observed_summary
from run_local_movement import checkpoint_hash, save
from run_waypoint_chain import records
from test_waypoint_reach import UNITY

SEEDS = [530000,530001,530002,530003,530004,530006,530014,530017,530021,530023,530024,530031]
BASELINE = GAME/'SavedModels/qa/local-waypoint-v1/diagnostics/qa67-navigation-20261116-100446-evaluate'


def run():
    stamp = datetime.now(timezone.utc).strftime('%Y%m%d-%H%M%S')
    runtime = GAME/'Logs/qa68-churn'/stamp
    destination = GAME/'SavedModels/qa/local-waypoint-v1/diagnostics'/('qa68-churn-'+stamp)
    runtime.mkdir(); destination.mkdir()
    save(GAME/'Logs/qa68-churn/current.json',dict(runtime=str(runtime),artifact=str(destination)))
    shutil.copytree(GAME/'ProjectSettings',runtime/'original-settings')
    sources = list(dict.fromkeys(SOURCES+['Assets/PCGDungeon/Scripts/QA/QaNavigationCoordinator.cs',
        'Assets/PCGDungeon/Scripts/QA/ModelSensorGrid.cs','Assets/Editor/QaNavigationVerification.cs',
        'Training/analyze_observed_navigation.py','Training/evaluate_navigation_clearance.py']))
    hashes = {name:checkpoint_hash(GAME/name) for name in sources}
    for name in sources:
        target=destination/'source'/name; target.parent.mkdir(parents=True,exist_ok=True)
        shutil.copy2(GAME/name,target)
    shutil.copy2(__file__,destination/'run-pilot.py')
    campaign=dict(qa_id='QA-68',status='direct-verification',seeds=SEEDS,cases=12,
        selection='four original prefix failures, four high-churn cases, four successful orientation controls; three per quarter-turn',
        checkpoint='QA-50',checkpoint_sha256=SHA256,new_training_episodes=0,source_sha256=hashes,
        baseline=str(BASELINE),runtime=str(runtime),model_steps=656793,optimizer_epochs=25610,lineage_episodes=4700,
        episode_seconds=60,waypoint_cap=10,observed_navigation=True)
    save(destination/'campaign.json',campaign)
    print('Artifact: '+str(destination),flush=True)
    try:
        direct=runtime/'direct-verification';direct.mkdir()
        process=subprocess.Popen([str(UNITY),'-batchmode','-nographics','-projectPath',str(GAME),
            '-executeMethod','QaNavigationVerification.Run','-trainingRunDirectory',str(direct),
            '-logFile',str(direct/'unity.log')],cwd=GAME,creationflags=subprocess.CREATE_NO_WINDOW)
        save(direct/'process.json',dict(unity_pid=process.pid))
        try:
            assert process.wait(timeout=300)==0, 'Direct Unity verification failed'
        finally:
            if process.poll() is None:process.terminate();process.wait(timeout=30)
        direct_result=json.loads((direct/'verification.json').read_text())
        assert direct_result['status']=='passed'
        shutil.copytree(direct,destination/'direct-verification')
        print('Direct verification: '+json.dumps(direct_result),flush=True)
        assert checkpoint_hash(CHECKPOINT)==SHA256
        torch.set_num_threads(1)
        model=PPO.load(CHECKPOINT,device='cpu');model.policy.set_training_mode(False)
        weights={k:v.clone() for k,v in model.policy.state_dict().items()}
        counters=(model.num_timesteps,model._n_updates)
        campaign['status']='policy-evaluation';save(destination/'campaign.json',campaign)
        started=time.monotonic()
        rows=phase(runtime,'evaluation',SEEDS,model=model,observed=True)
        elapsed=time.monotonic()-started
        retain_phase(runtime,destination,'evaluation')
        before={r['seed']:r for r in records(BASELINE/'evaluation/episodes.jsonl')}
        layouts={r['seed']:geometry_key(r) for r in records(BASELINE/'evaluation/layouts.jsonl')}
        assert all(geometry_key(r)==layouts[r['seed']] for r in records(runtime/'evaluation/layouts.jsonl'))
        assert counters==(model.num_timesteps,model._n_updates)
        assert all(torch.equal(v,model.policy.state_dict()[k]) for k,v in weights.items())
        assert checkpoint_hash(CHECKPOINT)==SHA256
        assert all(checkpoint_hash(GAME/n)==h for n,h in hashes.items())
        subset=destination/'baseline-subset';(subset/'evaluation').mkdir(parents=True)
        for name in ['episodes.jsonl','layouts.jsonl']:
            selected=[r for r in records(BASELINE/'evaluation'/name) if r['seed'] in SEEDS]
            (subset/'evaluation'/name).write_text(''.join(json.dumps(r)+'\n' for r in selected),encoding='utf-8')
        for name in ['actions.jsonl.gz','navigation-events.jsonl.gz']:
            with gzip.open(BASELINE/'evaluation'/name,'rt',encoding='utf-8') as src, gzip.open(subset/'evaluation'/name,'wt',encoding='utf-8') as dst:
                for line in src:
                    if json.loads(line)['seed'] in SEEDS:dst.write(line)
        for target in [subset,destination]:
            with (target/'audit.stdout.log').open('w') as log:
                subprocess.run([sys.executable,str(GAME/'Training/analyze_observed_navigation.py'),str(target)],check=True,stdout=log)
        result=dict(status='complete',frozen=True,weight_updates=0,source_hashes_verified=True,
            all_weights_unchanged=True,all_layouts_match_qa67=True,policy_phase_wall_seconds=elapsed,
            direct_verification=direct_result,before=observed_summary([before[s] for s in SEEDS]),after=observed_summary(rows),
            gained_exits=[r['seed'] for r in rows if r['completed'] and not before[r['seed']]['completed']],
            lost_exits=[r['seed'] for r in rows if not r['completed'] and before[r['seed']]['completed']],
            baseline_audit=json.loads((subset/'navigation-audit.json').read_text()),
            current_audit=json.loads((destination/'navigation-audit.json').read_text()))
        save(destination/'result.json',result)
        campaign.update(status='complete',all_weights_unchanged=True,source_hashes_verified=True)
        print(json.dumps({k:v for k,v in result.items() if not k.endswith('_audit')},indent=2),flush=True)
    except BaseException as error:
        campaign.update(status='failed',error=str(error));raise
    finally:
        for backup in (runtime/'original-settings').iterdir():
            if backup.is_file() and (GAME/'ProjectSettings'/backup.name).read_bytes()!=backup.read_bytes():
                shutil.copy2(backup,GAME/'ProjectSettings'/backup.name)
        campaign['settings_restored']=all((GAME/'ProjectSettings'/p.name).read_bytes()==p.read_bytes() for p in (runtime/'original-settings').iterdir() if p.is_file())
        save(destination/'campaign.json',campaign)

if __name__=='__main__':run()
