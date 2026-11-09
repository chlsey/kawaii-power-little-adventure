"""Summarize frozen exit/waypoint action traces, without updating or running a policy."""

import argparse
from collections import Counter, defaultdict
import gzip
import json
import math
from pathlib import Path
import shutil
import statistics


def read(path):
    return json.loads(path.read_text(encoding='utf-8'))


def dot_toward(record, target):
    dx = target['x'] - record['before']['x']
    dz = target['z'] - record['before']['z']
    length = math.hypot(dx, dz)
    speed = math.hypot(record['action_x'], record['action_z'])
    if length < 1e-6 or speed < 1e-6:
        return 0.0
    return (dx * record['action_x'] + dz * record['action_z']) / length / speed


def guidance_exit_cosine(record):
    wx = record['waypoint']['x'] - record['before']['x']
    wz = record['waypoint']['z'] - record['before']['z']
    ex = record['exit']['x'] - record['before']['x']
    ez = record['exit']['z'] - record['before']['z']
    length = math.hypot(wx, wz) * math.hypot(ex, ez)
    if length < 1e-6:
        return 0.0
    return (wx * ex + wz * ez) / length


def analyze(run, output, baseline):
    output.mkdir(parents=True, exist_ok=True)
    episodes = [json.loads(line) for line in (run / 'evaluation/episodes.jsonl').read_text().splitlines()]
    by_seed = defaultdict(list)
    with (run / 'evaluation/exit-decisions.jsonl').open(encoding='utf-8') as stream:
        for line in stream:
            record = json.loads(line)
            by_seed[record['seed']].append(record)
    assert len(episodes) == len(by_seed) == 500
    rows, arrivals = [], []
    for episode in episodes:
        trace = by_seed[episode['seed']]
        active = [r for r in trace if r['active']]
        assert len(active) == episode['steps']
        assert trace[-1]['done'] and trace[-1]['completed'] == episode['completed']
        assert all(r['seed'] == episode['seed'] for r in trace)
        assert all(r['waypoint_distance_after'] <= .50001 for r in trace if r['arrived'])
        exit_arrivals = [r for r in trace if r['arrived'] and r['goal_is_exit']]
        final_arrivals = [r for r in exit_arrivals if r['final_exit_waypoint']]
        row = dict(seed=episode['seed'], completed=episode['completed'],
                   exit_discovered=episode['exit_discovered'], steps=episode['steps'],
                   elapsed_seconds=episode['elapsed_seconds'],
                   any_waypoint_arrivals=sum(r['arrived'] for r in trace),
                   visible_exit_waypoint_arrivals=sum(r['arrived'] and r['exit_visible_after'] for r in trace),
                   exit_directed_arrivals=len(exit_arrivals), final_exit_waypoint_arrivals=len(final_arrivals),
                   min_exit_distance=min(r['exit_distance_after'] for r in trace),
                   exit_visible_actions=sum(r['exit_visible_before'] for r in active),
                   exit_goal_actions=sum(r['goal_is_exit'] for r in active),
                   final_waypoint_assigned=any(r['final_exit_waypoint'] for r in active),
                   final_arrival_then_away=False, final_arrival_then_exploring=False)
        visible = [r for r in active if r['exit_visible_before'] and r['has_waypoint']]
        row['visible_follows_away_guidance_actions'] = sum(
            guidance_exit_cosine(r) <= 0 and dot_toward(r, r['waypoint']) > .5 and
            dot_toward(r, r['exit']) <= 0 for r in visible)
        row['visible_moves_away_despite_exit_guidance_actions'] = sum(
            guidance_exit_cosine(r) > .5 and dot_toward(r, r['exit']) <= 0 for r in visible)
        if final_arrivals:
            first = final_arrivals[0]
            row['first_final_arrival_seconds'] = first['step'] * .1
            row['seconds_remaining_at_final_arrival'] = 60 - first['step'] * .1
        for index, record in enumerate(trace):
            if not record['arrived']:
                continue
            following = trace[index + 1] if index + 1 < len(trace) else None
            event = dict(seed=record['seed'], step=record['step'],
                         seconds=record['step'] * .1, goal_is_exit=record['goal_is_exit'],
                         final_exit_waypoint=record['final_exit_waypoint'],
                         completed_on_arrival=record['completed'], eventually_completed=episode['completed'],
                         exit_visible_on_arrival=record['exit_visible_after'],
                         exit_distance_on_arrival=record['exit_distance_after'],
                         arrival_position=record['after'], waypoint=record['waypoint'], exit=record['exit'])
            if following:
                event.update(next_status=following['status'],
                             next_goal_is_exit=following['goal_is_exit'],
                             next_has_waypoint=following['has_waypoint'],
                             next_exit_visible=following['exit_visible_before'],
                             next_action_exit_cosine=dot_toward(following, following['exit']),
                             next_action_waypoint_cosine=dot_toward(following, following['waypoint']),
                             next_guidance_exit_cosine=guidance_exit_cosine(following),
                             next_waypoint=following['waypoint'],
                             next_action=[following['action_x'], following['action_z']],
                             next_exit_distance_change=following['exit_distance_after'] - following['exit_distance_before'])
                if record['final_exit_waypoint']:
                    row['final_arrival_then_away'] |= event['next_action_exit_cosine'] <= 0
                    row['final_arrival_then_exploring'] |= following['status'] == 'exploring'
            arrivals.append(event)
        rows.append(row)
    counts = Counter()
    for row in rows:
        outcome = 'clear' if row['completed'] else 'timeout'
        counts[outcome] += 1
        for name, present in [('exit_discovered', row['exit_discovered']),
                              ('any_waypoint_arrival', row['any_waypoint_arrivals'] > 0),
                              ('waypoint_arrival_with_exit_visible', row['visible_exit_waypoint_arrivals'] > 0),
                              ('exit_directed_arrival', row['exit_directed_arrivals'] > 0),
                              ('final_exit_waypoint_assigned', row['final_waypoint_assigned']),
                              ('final_exit_waypoint_arrival', row['final_exit_waypoint_arrivals'] > 0),
                              ('final_arrival_then_away', row['final_arrival_then_away']),
                              ('final_arrival_then_exploring', row['final_arrival_then_exploring']),
                              ('visible_follows_away_guidance', row['visible_follows_away_guidance_actions'] > 0),
                              ('visible_moves_away_despite_exit_guidance', row['visible_moves_away_despite_exit_guidance_actions'] > 0)]:
            if present:
                counts[name] += 1
                counts[name + '_' + outcome] += 1
        if not row['completed']:
            if not row['exit_discovered']:
                counts['timeout_never_discovered_exit'] += 1
            elif not row['exit_directed_arrivals']:
                counts['timeout_discovered_without_exit_directed_arrival'] += 1
            elif not row['final_exit_waypoint_arrivals']:
                counts['timeout_exit_directed_arrival_without_final_arrival'] += 1
            else:
                counts['timeout_after_final_exit_waypoint_arrival'] += 1
    paired = None
    if baseline:
        previous = {r['seed']: r for r in map(json.loads, baseline.read_text(encoding='utf-8').splitlines())}
        if isinstance(previous, dict):
            paired = Counter()
            for episode in episodes:
                old = previous[episode['seed']]
                assert all(abs(old[k] - episode[k]) < 1e-5 for k in ('spawn_x', 'spawn_z', 'exit_x', 'exit_z'))
                paired['same_completion'] += old['completed'] == episode['completed']
                paired['same_steps'] += old['steps'] == episode['steps']
                paired['same_final_distance'] += abs(old['final_exit_distance'] - episode['final_exit_distance']) < 1e-4
    # Keep the experiment's primary zero counts explicit in the saved summary.
    counts['final_exit_waypoint_arrival_timeout'] += 0
    counts['timeout_after_final_exit_waypoint_arrival'] += 0
    final_events = [event for event in arrivals if event['final_exit_waypoint']]
    final_clear_delays = [max(0., row['elapsed_seconds'] - row['first_final_arrival_seconds'])
                          for row in rows if row['completed'] and row['final_exit_waypoint_arrivals']]
    final_event_counts = Counter()
    for event in final_events:
        final_event_counts['total'] += 1
        final_event_counts['completed_on_arrival'] += event['completed_on_arrival']
        if 'next_action_exit_cosine' not in event:
            final_event_counts['no_following_action'] += 1
            continue
        final_event_counts['next_action_toward_exit'] += event['next_action_exit_cosine'] > 0
        final_event_counts['next_action_away_or_perpendicular'] += event['next_action_exit_cosine'] <= 0
        final_event_counts['next_instruction_exploring'] += event['next_status'] == 'exploring'
        final_event_counts['next_instruction_exit'] += event['next_status'] == 'exit'
        if event['next_has_waypoint'] and event['next_guidance_exit_cosine'] <= 0:
            final_event_counts['next_guidance_away_from_exit'] += 1
            final_event_counts['away_guidance_but_action_toward_exit'] += event['next_action_exit_cosine'] > 0
    result = dict(episodes=len(rows), action_records=sum(map(len, by_seed.values())), counts=dict(counts),
                  final_arrival_event_counts=dict(final_event_counts),
                  seconds_from_first_final_arrival_to_clear=dict(
                      mean=statistics.mean(final_clear_delays), median=statistics.median(final_clear_delays),
                      minimum=min(final_clear_delays), maximum=max(final_clear_delays)) if final_clear_delays else None,
                  matched_baseline=dict(paired) if paired else None,
                  distinct_final_arrival_events=sum(r['final_exit_waypoint'] for r in arrivals),
                  distinct_exit_directed_arrival_events=sum(r['goal_is_exit'] for r in arrivals),
                  definitions=dict(arrival='Active assigned waypoint crossed from >0.5 to <=0.5 units during an action, before navigator handoff.',
                                   exit_directed='Navigator goal was an observed exit cell before this action; intermediate route points included.',
                                   final='Assigned waypoint equals last route point to observed exit cell; not necessarily exit centre.',
                                   away='Next commanded movement has nonpositive dot product toward actual exit centre; descriptive, not proof of intentional refusal.'))
    for name, value in [('analysis.json', result), ('episode-analysis.json', rows), ('waypoint-arrivals.json', arrivals)]:
        (output / name).write_text(json.dumps(value, indent=2), encoding='utf-8')
    print(json.dumps(result, indent=2))
    for source in ('training-config.yaml', 'run.json', 'evaluation-result.json'):
        shutil.copy2(run / source, output / source)
    for source in ('episodes.jsonl', 'exit-trace-contract.json', 'policy-contract.json'):
        shutil.copy2(run / 'evaluation' / source, output / source)
    with (run / 'evaluation/exit-decisions.jsonl').open('rb') as source:
        with gzip.open(output / 'exit-decisions.jsonl.gz', 'wb') as target:
            shutil.copyfileobj(source, target)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--run', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--baseline', type=Path)
    args = parser.parse_args()
    analyze(args.run, args.output, args.baseline)
