"""Analyze the retained chain records; run beside the copied JSON/JSONL evidence."""
from collections import defaultdict
import gzip
import hashlib
import json
from pathlib import Path
import numpy as np

HERE = Path(__file__).resolve().parent


def rows(name):
    path = HERE / name
    if path.exists():
        return [json.loads(line) for line in path.read_text().splitlines()]
    with gzip.open(str(path) + '.gz', 'rt') as stream:
        return [json.loads(line) for line in stream]


episodes = rows('episodes.jsonl')
reference = rows('reference-episodes.jsonl')
legs = rows('legs.jsonl')
actions = rows('actions.jsonl')
assert len(episodes) == len(reference) == 100
assert all(row['reached'] for row in reference)
assert rows('chains.jsonl') == rows('reference-chains.jsonl')
by_leg = defaultdict(list)
for row in actions:
    by_leg[row['seed'], row['leg']].append(row)
leg_info = {(r['seed'], r['leg']): r for r in legs}
handoffs = 0
for episode in episodes:
    trace = [r for r in actions if r['seed'] == episode['seed']]
    assert len(trace) == episode['steps']
    assert trace[0]['observation'][:2] == [0, 0]
    for previous, current in zip(trace, trace[1:]):
        assert np.allclose(current['observation'][:2], previous['action'], atol=1e-6)
        if current['leg'] != previous['leg']:
            assert current['leg'] == previous['leg'] + 1
            end = leg_info[episode['seed'], previous['leg']]
            assert np.allclose([current['x'], current['z']], [end['final_x'], end['final_z']], atol=1e-5)
            handoffs += 1

per_leg = []
for index in range(10):
    subset = [r for r in legs if r['leg'] == index]
    arrivals = [r for r in subset if r['reached']]
    per_leg.append(dict(waypoint=index + 1, attempted=len(subset), reached=len(arrivals),
                        timed_out=len(subset) - len(arrivals),
                        successful_mean_seconds=float(np.mean([r['steps'] * .1 for r in arrivals])) if arrivals else None))

failure_details = []
all_alignment = []
for leg in legs:
    trace = by_leg[leg['seed'], leg['leg']]
    assert len(trace) == leg['steps']
    alignment = []
    for row in trace:
        command = np.array(row['action'], dtype=float)
        direction = np.array(row['observation'][131:133], dtype=float)
        norm = np.linalg.norm(command)
        value = float(np.dot(command, direction) / norm) if norm else 0.
        alignment.append(value)
    all_alignment.extend(alignment)
    if not leg['reached']:
        failure_details.append(dict(seed=leg['seed'], waypoint=leg['leg'] + 1,
            failed_leg_seconds=leg['steps'] * .1, initial_distance=leg['initial_distance'],
            final_distance=leg['final_distance'], minimum_distance=leg['minimum_distance'],
            mean_command_alignment=float(np.mean(alignment)),
            away_or_perpendicular_commands=sum(value <= 0 for value in alignment),
            failed_leg_actions=len(trace)))

first_handoff = [trace[0] for (seed, index), trace in by_leg.items() if index > 0]
ref_times = [r['elapsed_seconds'] for r in reference]
success_times = [r['elapsed_seconds'] for r in episodes if r['reached']]
result = dict(
    completed_chains=sum(r['reached'] for r in episodes),
    timeouts=sum(not r['reached'] for r in episodes),
    waypoint_arrivals=sum(r['reached'] for r in legs), attempted_waypoints=len(legs),
    unattempted_planned_waypoints=1000-len(legs), verified_arrival_handoffs=handoffs,
    preserved_previous_action_on_every_continuing_action=True,
    continuous_position_at_every_handoff=True,
    assigned_distance_min=min(r['initial_distance'] for r in legs),
    assigned_distance_max=max(r['initial_distance'] for r in legs),
    per_waypoint=per_leg,
    successful_mean_seconds=float(np.mean(success_times)) if success_times else None,
    successful_p90_seconds=float(np.percentile(success_times,90)) if success_times else None,
    reference_mean_seconds=float(np.mean(ref_times)), reference_max_seconds=max(ref_times),
    mean_command_alignment=float(np.mean(all_alignment)),
    failed_leg_seconds_mean=float(np.mean([r['failed_leg_seconds'] for r in failure_details])) if failure_details else None,
    failures=failure_details,
    first_action_after_handoff_nonzero_history=int(sum(np.linalg.norm(r['observation'][:2]) > 1e-6 for r in first_handoff)),
    all_source_hashes_verified=True)
run = json.loads((HERE / 'run.json').read_text())
for name, digest in run['source_sha256'].items():
    assert hashlib.sha256((HERE / 'source' / name).read_bytes()).hexdigest() == digest
(HERE / 'analysis.json').write_text(json.dumps(result, indent=2, allow_nan=False))
print(json.dumps({key: value for key, value in result.items() if key != 'failures'}, indent=2))
