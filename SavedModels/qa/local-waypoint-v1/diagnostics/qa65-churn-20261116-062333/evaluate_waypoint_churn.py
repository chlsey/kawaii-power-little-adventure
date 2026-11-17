"""Run 100 frozen QA-50 cases and account for every waypoint assignment."""

from collections import Counter, defaultdict
from datetime import datetime, timezone
from pathlib import Path
import argparse
import gzip
import json
import shutil
import statistics

from evaluate_exploration_waypoints import CONFIG
from run_local_movement import GAME, checkpoint_hash, load_config, run_waypoints, save
from test_waypoint_reach import UNITY


def analyze(directory, destination, baseline=None):
    phase = directory / 'evaluation'
    result = json.loads((directory / 'evaluation-result.json').read_text())
    assert result['completed_episodes'] == 100
    assert result['weight_updates'] == result['new_training_steps'] == 0
    episodes = [json.loads(line) for line in (phase / 'episodes.jsonl').read_text().splitlines()]
    assert [e['seed'] for e in episodes] == list(range(510000, 510100))
    events = defaultdict(list)
    for line in (phase / 'navigation-events.jsonl').read_text().splitlines():
        row = json.loads(line)
        events[row['seed']].append(row)
    closures = {'escape_arrived_free', 'escape_arrived_blocked', 'escape_invalid',
        'start_cell_blocked', 'waypoint_arrived', 'waypoint_segment_invalid',
        'waypoint_clearance_invalid', 'stalled_replan', 'stalled_abandon',
        'goal_reached', 'goal_cell_invalid', 'select_exit', 'select_frontier',
        'abandon_exit', 'abandon_frontier', 'oscillation_detected', 'escape_stalled', 'goal_progress_stalled'}
    legs, per_episode = [], []
    reasons, event_counts = Counter(), Counter()
    for episode in episodes:
        current = None
        seed_legs = []

        def close(reason, seconds, distance):
            nonlocal current
            current.update(end_reason=reason, seconds=seconds-current['start_seconds'],
                final_distance=distance)
            seed_legs.append(current)
            current = None

        for row in events[episode['seed']]:
            reason = row['reason']
            event_counts[reason] += 1
            if reason in ('route_assigned', 'escape_assigned'):
                if current is not None:
                    close('replaced_without_recorded_invalidation', row['seconds'], None)
                current = dict(seed=episode['seed'], version=row['waypoint_version'],
                    kind=reason, waypoint=row['waypoint'], start_seconds=row['seconds'],
                    initial_distance=row['waypoint_distance'], start_step=row['step'])
            elif (current is not None and reason in closures and row['has_waypoint']
                  and row['waypoint_version'] == current['version']):
                close(reason, row['seconds'], row['waypoint_distance'])
        if current is not None:
            close('episode_completed' if episode['completed'] else 'episode_timeout',
                episode['elapsed_seconds'], None)
        assert len(seed_legs) == episode['waypoint_assignments'], episode['seed']
        assert [leg['version'] for leg in seed_legs] == list(range(1, len(seed_legs)+1))
        counts = Counter(leg['end_reason'] for leg in seed_legs)
        reasons.update(counts)
        per_episode.append(dict(**episode, assignment_end_reasons=dict(counts),
            escape_assignments=sum(leg['kind']=='escape_assigned' for leg in seed_legs),
            route_assignments=sum(leg['kind']=='route_assigned' for leg in seed_legs)))
        legs.extend(seed_legs)
    before_dir = GAME/'Logs/qa50-exploration-waypoints/20261115-070559-evaluate/evaluation'
    if baseline is not None:
        before_dir = baseline / 'evaluation'
    before = [json.loads(line) for line in (before_dir/'episodes.jsonl').read_text().splitlines()]
    assert len(before) == 100
    for a, b in zip(before, episodes):
        assert all(a[k] == b[k] for k in ['seed','spawn_x','spawn_z','exit_x','exit_z'])
    traces = Counter()
    for line in (phase/'exit-decisions.jsonl').read_text().splitlines():
        row = json.loads(line)
        if row['active']:
            traces['actions'] += 1
            if not row['has_waypoint']: traces['without_waypoint'] += 1
            if ((row['after']['x']-row['before']['x'])**2 +
                (row['after']['z']-row['before']['z'])**2)**.5 < .05:
                traces['low_motion_actions'] += 1
    actions = sum(e['steps'] for e in episodes)
    assert traces['actions'] == actions
    duration = (phase/'episode-99.json').stat().st_mtime-(phase/'unity-ready.txt').stat().st_mtime
    old_duration = (before_dir/'episode-99.json').stat().st_mtime-(before_dir/'unity-ready.txt').stat().st_mtime
    comparison = dict(exits=sum(e['completed'] for e in before),
        discoveries=sum(e['exit_discovered'] for e in before),
        actions=sum(e['steps'] for e in before),
        waypoint_assignments=sum(e['waypoint_assignments'] for e in before),
        action_phase_seconds=old_duration,
        actions_per_second=sum(e['steps'] for e in before)/old_duration,
        gained_seeds=[b['seed'] for a,b in zip(before,episodes) if not a['completed'] and b['completed']],
        lost_seeds=[b['seed'] for a,b in zip(before,episodes) if a['completed'] and not b['completed']])
    summary = dict(status='complete', frozen=True, checkpoint='QA-50',
        checkpoint_sha256=result['checkpoint_sha256'], cases=100,seeds=[510000,510099],
        weight_updates=0,new_training_steps=0,
        exits=sum(e['completed'] for e in episodes),
        discoveries=sum(e['exit_discovered'] for e in episodes),actions=actions,
        waypoint_assignments=len(legs), assignment_end_reasons=dict(reasons),
        event_counts=dict(event_counts),
        escape_assignments=sum(e['escape_assignments'] for e in per_episode),
        route_assignments=sum(e['route_assignments'] for e in per_episode),
        planner_builds=sum(e['planner_builds'] for e in episodes),
        goal_selections=sum(e['goal_selections'] for e in episodes),
        route_searches=sum(e['route_searches'] for e in episodes),
        actions_without_waypoint=traces['without_waypoint'],
        low_motion_actions=traces['low_motion_actions'],
        median_assignment_seconds=statistics.median(leg['seconds'] for leg in legs),
        action_phase_seconds=duration,actions_per_second=actions/duration,
        before=comparison,
        highest_assignment_cases=sorted(per_episode,key=lambda e:e['waypoint_assignments'],reverse=True)[:10])
    save(destination/'churn-result.json',summary)
    save(destination/'churn-episodes.json',per_episode)
    save(destination/'churn-assignments.json',legs)
    for name in ['evaluation-result.json','training-config.yaml','run.json']:
        shutil.copy2(directory/name,destination/name)
    shutil.copy2(phase/'episodes.jsonl',destination/'episodes.jsonl')
    for name in ['exit-decisions.jsonl','navigation-events.jsonl']:
        with (phase/name).open('rb') as src, gzip.open(destination/(name+'.gz'),'wb') as dst:
            shutil.copyfileobj(src,dst)
    return summary


def run(qa_id='QA-64', baseline=None):
    stamp = datetime.now(timezone.utc).strftime('%Y%m%d-%H%M%S')
    destination = GAME/'SavedModels/qa/local-waypoint-v1/diagnostics'/(qa_id.lower().replace('-','')+'-churn-'+stamp)
    destination.mkdir(parents=True)
    campaign = dict(qa_id=qa_id,status='verification',frozen_cases=100,
        new_training_episodes=0,checkpoint='QA-50',seeds=[510000,510099])
    if baseline is not None: campaign['baseline'] = str(baseline)
    save(destination/'campaign.json',campaign)
    config = load_config(CONFIG)
    output = (CONFIG.parent/config['output_dir']).resolve()
    try:
        previous = set(output.glob('*-verify'))
        run_waypoints(CONFIG,UNITY,'verify',None)
        verification = (set(output.glob('*-verify'))-previous).pop()
        shutil.copy2(verification/'verification.json',destination/'verification.json')
        campaign.update(status='frozen-evaluation',verification=str(verification))
        save(destination/'campaign.json',campaign)
        previous = set(output.glob('*-evaluate'))
        run_waypoints(CONFIG,UNITY,'evaluate',100)
        directory = (set(output.glob('*-evaluate'))-previous).pop()
        campaign.update(status='analyzing',runtime=str(directory))
        save(destination/'campaign.json',campaign)
        result = analyze(directory,destination,baseline)
        run_metadata = json.loads((directory/'run.json').read_text())
        for name,digest in run_metadata['source_sha256'].items():
            assert checkpoint_hash(GAME/name) == digest
            target = destination/'source'/name
            target.parent.mkdir(parents=True,exist_ok=True)
            shutil.copy2(GAME/name,target)
        shutil.copy2(Path(__file__),destination/Path(__file__).name)
        for backup in (directory/'original-settings').iterdir():
            if backup.is_file(): assert (GAME/'ProjectSettings'/backup.name).read_bytes() == backup.read_bytes()
        campaign.update(status='complete',parent_source_hashes_verified=True,settings_restored=True)
        save(destination/'campaign.json',campaign)
        print(json.dumps(result,indent=2),flush=True)
    except BaseException as error:
        campaign.update(status='failed',error=str(error))
        save(destination/'campaign.json',campaign)
        raise


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--qa-id',default='QA-64',choices=['QA-64','QA-65'])
    parser.add_argument('--baseline',type=Path,help='Completed matched runtime to compare against')
    args = parser.parse_args()
    run(args.qa_id,args.baseline.resolve() if args.baseline else None)
