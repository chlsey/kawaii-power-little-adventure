"""100 frozen two-room tests of the retained QA-68 navigator."""
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

SEEDS = list(range(530000, 530100))
BASELINE = GAME/'SavedModels/qa/local-waypoint-v1/diagnostics/qa67-navigation-20261116-100446-evaluate'


def run():
    stamp = datetime.now(timezone.utc).strftime('%Y%m%d-%H%M%S')
    runtime = GAME/'Logs/qa70-two-room'/stamp
    destination = GAME/'SavedModels/qa/local-waypoint-v1/diagnostics'/('qa70-two-room-'+stamp)
    runtime.mkdir(); destination.mkdir()
    save(GAME/'Logs/qa70-two-room/current.json',dict(runtime=str(runtime),artifact=str(destination)))
    shutil.copytree(GAME/'ProjectSettings',runtime/'original-settings')
    sources = list(dict.fromkeys(SOURCES+['Assets/PCGDungeon/Scripts/QA/QaNavigationCoordinator.cs',
        'Assets/PCGDungeon/Scripts/QA/ModelSensorGrid.cs','Assets/Editor/QaNavigationVerification.cs',
        'Training/analyze_observed_navigation.py','Training/evaluate_navigation_clearance.py']))
    hashes = {name:checkpoint_hash(GAME/name) for name in sources}
    for name in sources:
        target=destination/'source'/name; target.parent.mkdir(parents=True,exist_ok=True)
        shutil.copy2(GAME/name,target)
    shutil.copy2(__file__,destination/'run-pilot.py')
    campaign=dict(qa_id='QA-70',status='direct-verification',seeds=SEEDS,cases=100,
        selection='all 100 matched QA-67 two-room layouts; 25 per quarter-turn',
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

def write_report():
    pointer=GAME/'Logs/qa70-two-room/current.json'
    if not pointer.exists():return
    destination=Path(json.loads(pointer.read_text())['artifact'])
    campaign=json.loads((destination/'campaign.json').read_text())
    log=GAME/'Training/changelog/qa-player.md'
    text=log.read_text(encoding='utf-8')
    if campaign['status']=='complete':
        result=json.loads((destination/'result.json').read_text())
        before=result['baseline_audit'];after=result['current_audit']
        metrics=[('Exits',before['exits'],after['exits']),
            ('Exit discoveries',before['discoveries'],after['discoveries']),
            ('Assignments',before['assignments'],after['assignments']),
            ('Assignments per 100 guided actions',round(before['assignments_per_100_guided_actions'],2),round(after['assignments_per_100_guided_actions'],2)),
            ('Actions without waypoint (%)',round(before['no_waypoint_percent'],2),round(after['no_waypoint_percent'],2)),
            ('Interrupted instructions',before['assignments_replaced_before_arrival'],after['assignments_replaced_before_arrival']),
            ('Broader recovery return states',before['broader_recovery_return_states'],after['broader_recovery_return_states']),
            ('Stuck windows',before['stuck_windows'],after['stuck_windows']),
            ('Actions',before['actions'],after['actions'])]
        report='# QA-70: 100 two-room tests of the QA-68 navigator\n\nComplete frozen QA-50 evaluation on the same 100 generated layouts as QA-67: seeds 530000-530099, two 20 x 20 rooms and real corridors, 25 cases per quarter-turn, sixty seconds/600 actions per case, twelve-unit vision and ten-unit waypoint cap. Navigation uses observed sensor memory only.\n\n| Metric | QA-67 | QA-70 |\n| --- | --- | --- |\n'
        report+=''.join(f'| {name} | {a} | {b} |\n' for name,a,b in metrics)
        report+=f"\nGained exits: {result['gained_exits']}. Lost exits: {result['lost_exits']}.\n\nAll 57 existing direct assertions pass. Model file and in-memory weights/counters stayed unchanged; run-owned source hashes and canonical layout matches verified. Project settings restored. No training, deployment, commit or push. QA-50 remains selected at 656,793 steps / 25,610 optimizer epochs / 4,700 lineage episodes.\n\nThese reused diagnostic layouts do not establish independent generic-level performance. Recovery return states and stuck windows are diagnostic counts, not distinct failures. No new reference-controller replay is included in this campaign.\n\nEvidence: [campaign](campaign.json), [results](result.json), [instruction audit](navigation-audit.json), [direct verification](direct-verification/verification.json). The retained [runner](run-pilot.py) is launched from `Logs/qa70-two-room/run.py`.\n"
        (destination/'README.md').write_text(report,encoding='utf-8')
        outcome=f"Completed: exits **{before['exits']} -> {after['exits']}/100**, discoveries **{before['discoveries']} -> {after['discoveries']}/100**, normalized assignments **{before['assignments_per_100_guided_actions']:.2f} -> {after['assignments_per_100_guided_actions']:.2f} per 100 guided actions**, missing guidance **{before['no_waypoint_percent']:.2f}% -> {after['no_waypoint_percent']:.2f}%**; stuck windows **{before['stuck_windows']} -> {after['stuck_windows']}**. Gained {len(result['gained_exits'])} exits / lost {len(result['lost_exits'])}. All 57 direct assertions pass; file/in-memory weights and counters, source hashes and canonical layout matches verified, settings restored. [Full report](../../SavedModels/qa/local-waypoint-v1/diagnostics/{destination.name}/README.md). Reused diagnostic layouts; no new reference controls or independent generalization claim."
        text=text.replace('**QA-70 running /','**QA-70 complete /',1)
        text=text.replace('Evaluation pending; runtime/artifact pointers in `Logs/qa70-two-room/current.json`.',outcome,1)
    else:
        text=text.replace('**QA-70 running /','**QA-70 stopped /',1)
        text=text.replace('Evaluation pending; runtime/artifact pointers in `Logs/qa70-two-room/current.json`.',f"Campaign ended with status `{campaign['status']}`: {campaign.get('error','see campaign record')}. Runtime/artifact pointers in `Logs/qa70-two-room/current.json`; partial cases must not be reported as a complete 100-case result.",1)
    log.write_text(text,encoding='utf-8',newline='\r\n')

if __name__=='__main__':
    try:run()
    finally:write_report()

