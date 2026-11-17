"""Frozen QA-50 frontier-waypoint arrival and unseen-exit discovery diagnostic."""

from collections import Counter, defaultdict
from pathlib import Path
import argparse
import gzip
import json
import shutil
import statistics

from run_local_movement import GAME, checkpoint_hash, load_config, run_waypoints, save
from test_waypoint_reach import UNITY


CONFIG = GAME / 'Training/qa-player-exploration-qa50-evaluate.yaml'


def analyze(directory):
    config = load_config(directory / 'training-config.yaml')
    result = json.loads((directory / 'evaluation-result.json').read_text())
    assert result['weight_updates'] == result['new_training_steps'] == 0
    assert result['completed_episodes'] == config['evaluation_episodes'] == 100
    checkpoint = (CONFIG.parent / config['resume_from']).resolve()
    assert checkpoint_hash(checkpoint) == config['resume_sha256']
    phase = directory / 'evaluation'
    episodes = [json.loads(line) for line in (phase / 'episodes.jsonl').read_text().splitlines()]
    assert [e['seed'] for e in episodes] == list(range(510000, 510100))
    assert all(not e['initial_exit_visible'] for e in episodes)
    by_seed = defaultdict(list)
    with (phase / 'exit-decisions.jsonl').open(encoding='utf-8') as stream:
        for line in stream:
            row = json.loads(line)
            if row['active']:
                by_seed[row['seed']].append(row)
    assert set(by_seed) == {e['seed'] for e in episodes}
    outcomes = Counter()
    per_episode, legs, arrival_times, discovery_times = [], [], [], []
    for episode in episodes:
        rows = by_seed[episode['seed']]
        assignments = defaultdict(list)
        first_exit = None
        for row in rows:
            if first_exit is None:
                if row['exit_visible_before']:
                    first_exit = row['seconds_before']
                elif row['exit_visible_after']:
                    first_exit = row['seconds_before'] + .1
            if row['has_waypoint']:
                assignments[row['waypoint_version']].append(row)
        seed_outcomes = Counter()
        for version, samples in assignments.items():
            first, last = samples[0], samples[-1]
            if first['goal_is_exit'] or first['status'] != 'exploring':
                continue
            assert all(not r['goal_is_exit'] and r['status'] == 'exploring' for r in samples)
            assert .5 < first['waypoint_distance_before'] <= 10.0001
            arrivals = [r for r in samples if r['arrived'] or r['waypoint_distance_after'] <= .5]
            later = next((r for r in rows if r['step'] > last['step']), None)
            if arrivals:
                outcome = 'arrived'
                duration = arrivals[0]['seconds_before'] + .1 - first['seconds_before']
                arrival_times.append(duration)
            else:
                duration = last['seconds_before'] + .1 - first['seconds_before']
                if later is not None and later['goal_is_exit']:
                    outcome = 'superseded_by_exit_goal'
                elif last['done'] or later is None:
                    outcome = 'episode_ended_before_arrival'
                else:
                    outcome = 'replaced_without_arrival'
            seed_outcomes[outcome] += 1
            legs.append(dict(seed=episode['seed'], version=version, outcome=outcome,
                initial_distance=first['waypoint_distance_before'], seconds=duration,
                waypoint=first['waypoint']))
        outcomes.update(seed_outcomes)
        # Final sensor-map evidence is authoritative; visibility traces provide timing.
        discovered = episode['exit_discovered']
        if first_exit is not None:
            assert discovered
            discovery_times.append(first_exit)
        per_episode.append(dict(seed=episode['seed'], exit_discovered=discovered,
            first_exit_observed_seconds=first_exit, completed=episode['completed'],
            exploration_assignments=sum(seed_outcomes.values()), outcomes=dict(seed_outcomes),
            replans=episode['replans'], abandoned_goals=episode['abandoned_goals']))
    discovered = sum(e['exit_discovered'] for e in per_episode)
    completed = sum(e['completed'] for e in per_episode)
    assert all(not e['completed'] or e['exit_discovered'] for e in per_episode)
    total = sum(outcomes.values())
    summary = dict(status='complete',checkpoint_sha256=config['resume_sha256'],
        frozen=True,weight_updates=0,episodes=100,seeds=[510000,510099],
        environment='50x50 empty room; exit 30-40 units away, initially unseen; 60 seconds',
        exploration_assignments=total,outcomes=dict(outcomes),
        exploration_assignment_arrival_rate=outcomes['arrived']/total if total else None,
        mean_successful_assignment_seconds=statistics.mean(arrival_times) if arrival_times else None,
        episodes_with_exploration_assignments=sum(e['exploration_assignments']>0 for e in per_episode),
        exit_discovered=discovered,exit_discovery_rate=discovered/100,
        exit_completed=completed,exit_completion_rate=completed/100,
        discovered_but_not_completed=discovered-completed,never_discovered=100-discovered,
        mean_discovery_seconds=statistics.mean(discovery_times) if discovery_times else None,
        discovery_timing_cases=len(discovery_times),
        replans=sum(e['replans'] for e in per_episode),
        abandoned_goals=sum(e['abandoned_goals'] for e in per_episode),
        limits=['Empty room only; no internal walls, corridors, combat or puzzles.',
            'Replacement is not necessarily a movement failure; discovery can change goals.',
            'Assignment arrival is different from reaching the frontier goal or completing the level.'])
    save(directory / 'exploration-result.json',summary)
    save(directory / 'exploration-episodes.json',per_episode)
    save(directory / 'exploration-assignments.json',legs)
    destination = GAME/'SavedModels/qa/local-waypoint-v1/diagnostics'/('qa50-exploration-'+directory.name)
    destination.mkdir(exist_ok=True)
    for path in directory.glob('*.json'):
        shutil.copy2(path,destination/path.name)
    shutil.copy2(directory/'training-config.yaml',destination/'training-config.yaml')
    shutil.copy2(phase/'episodes.jsonl',destination/'episodes.jsonl')
    with (phase/'exit-decisions.jsonl').open('rb') as src, gzip.open(destination/'exit-decisions.jsonl.gz','wb') as dst:
        shutil.copyfileobj(src,dst)
    shutil.copy2(Path(__file__),destination/Path(__file__).name)
    print(json.dumps(summary,indent=2),flush=True)
    print('Retained exploration diagnostic: '+str(destination),flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--run-directory',type=Path,help='Analyze an already finished frozen run without rerunning Unity.')
    args = parser.parse_args()
    if args.run_directory:
        analyze(args.run_directory.resolve())
    else:
        config = load_config(CONFIG)
        output = (CONFIG.parent/config['output_dir']).resolve()
        previous = set(output.glob('*-evaluate'))
        run_waypoints(CONFIG,UNITY,'evaluate',None)
        created = set(output.glob('*-evaluate'))-previous
        assert len(created)==1
        analyze(created.pop())
