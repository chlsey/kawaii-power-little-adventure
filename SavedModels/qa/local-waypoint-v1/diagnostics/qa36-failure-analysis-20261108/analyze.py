"""QA-37: saved-record localization and six matched frozen replay traces."""

from collections import Counter, defaultdict
import gzip
import hashlib
import json
import math
from pathlib import Path
import shutil
import statistics

import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.patches import Rectangle
import numpy as np
import torch
from stable_baselines3 import PPO

HERE = Path(__file__).resolve().parent
GAME = HERE.parents[4]
ARCH = HERE.parents[1]
SEEDS = [285098, 285228, 285304, 285076, 285363, 285455]


def readlines(path):
    return [json.loads(line) for line in path.read_text().splitlines()]


def stage(episode):
    if episode['completed']:
        return 'clear'
    if episode['exit_discovered']:
        return 'discovered_timeout'
    return 'undiscovered_timeout'


def distance(a, b):
    return math.hypot(a['x'] - b['x'], a['z'] - b['z'])


def cosine(actions, directions):
    denominator = np.linalg.norm(actions, axis=1) * np.linalg.norm(directions, axis=1)
    return (actions * directions).sum(axis=1) / np.maximum(denominator, 1e-9)


def analyze():
    torch.set_num_threads(1)
    old = readlines(ARCH / 'diagnostics/distant-exit-20261107-220640-evaluate/episodes.jsonl')
    new = readlines(ARCH / 'qa-waypoint-20261108-051102-train/evaluation-episodes.jsonl')
    old_by_seed = {e['seed']: e for e in old}
    new_by_seed = {e['seed']: e for e in new}
    transitions = Counter((stage(a), stage(b)) for a, b in zip(old, new))
    direction_cases = defaultdict(list)
    for a, b in zip(old, new):
        assert a['seed'] == b['seed']
        angle = math.atan2(b['exit_x'] - b['spawn_x'], b['exit_z'] - b['spawn_z'])
        sector = int(math.floor((angle + math.pi / 8) / (math.pi / 4))) % 8
        direction_cases[sector].append((a, b))
    directions = []
    for i, name in enumerate(['N', 'NE', 'E', 'SE', 'S', 'SW', 'W', 'NW']):
        rs = direction_cases[i]
        directions.append(dict(direction=name, cases=len(rs), parent_clears=sum(a['completed'] for a, b in rs),
                               child_clears=sum(b['completed'] for a, b in rs)))
    full = dict(transitions=[dict(parent=a, child=b, cases=n) for (a, b), n in transitions.items()],
                directions=directions, lost_cases=[b['seed'] for a, b in zip(old, new) if a['completed'] and not b['completed']],
                gained_cases=[b['seed'] for a, b in zip(old, new) if not a['completed'] and b['completed']])
    (HERE / 'full-benchmark-analysis.json').write_text(json.dumps(full, indent=2), encoding='utf-8')

    parent_traces = defaultdict(list)
    with gzip.open(ARCH / 'diagnostics/exit-decisions-20261108-022500-evaluate/exit-decisions.jsonl.gz', 'rt') as stream:
        for line in stream:
            r = json.loads(line)
            if r['seed'] in SEEDS:
                parent_traces[r['seed']].append(r)

    parent_path = ARCH / 'qa-waypoint-reach-20261107-214655-train/final_model.zip'
    child_path = ARCH / 'qa-waypoint-20261108-051102-train/final_model.zip'
    sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
    hashes = [sha(parent_path), sha(child_path)]
    parent = PPO.load(parent_path, device='cpu')
    child = PPO.load(child_path, device='cpu')
    counters = [(m.num_timesteps, m._n_updates) for m in [parent, child]]
    cases = []
    fig, axes = plt.subplots(2, 3, figsize=(15, 10), constrained_layout=True)
    fig.suptitle('QA-37 selected lost cases: QA-32 clears (grey), QA-36 times out (blue)', fontsize=14)
    for ax, seed in zip(axes.flat, SEEDS):
        runs = sorted((GAME / f'Logs/qa36-failure-analysis/case-{seed}').glob('*-evaluate'))
        assert len(runs) == 1
        run = runs[0]
        records = readlines(run / 'evaluation/exit-decisions.jsonl')
        episode = readlines(run / 'evaluation/episodes.jsonl')[0]
        original = new_by_seed[seed]
        assert episode['completed'] == original['completed'] and episode['steps'] == original['steps'] == len(records) == 600
        assert abs(episode['final_exit_distance'] - original['final_exit_distance']) < 1e-5
        result = json.loads((run / 'evaluation-result.json').read_text())
        assert result['weight_updates'] == 0 and result['new_training_steps'] == 0
        assert (run / 'evaluation/unity-finished.txt').exists()
        obs = np.array([r['observation'] for r in records], dtype=np.float32)
        expected = np.array([[r['action_x'], r['action_z']] for r in records], dtype=np.float32)
        predicted = child.predict(obs, deterministic=True)[0]
        max_error = float(np.max(np.abs(predicted - expected)))
        assert max_error < 1e-5
        old_actions = parent.predict(obs, deterministic=True)[0]
        direction = obs[:, 131:133]
        assigned = obs[:, 130] > 0
        new_cos = cosine(predicted, direction)
        old_cos = cosine(old_actions, direction)
        no_previous = obs.copy()
        no_previous[:, :2] = 0
        no_previous_cos = cosine(child.predict(no_previous, deterministic=True)[0], direction)
        visible_steps = [r['step'] for r in records if r['exit_visible_before'] or r['exit_visible_after']]
        groups = defaultdict(list)
        for r in records:
            groups[r['waypoint_version']].append(r)
        assignments = []
        for version, rs in groups.items():
            assignments.append(dict(version=version, start_step=rs[0]['step'], end_step=rs[-1]['step'],
                                    status=rs[0]['status'], goal_is_exit=rs[0]['goal_is_exit'],
                                    final_exit_waypoint=rs[0]['final_exit_waypoint'], actions=len(rs),
                                    start_distance=rs[0]['waypoint_distance_before'],
                                    closest_distance=min(r['waypoint_distance_after'] for r in rs),
                                    end_distance=rs[-1]['waypoint_distance_after'],
                                    arrived=any(r['arrived'] for r in rs), waypoint=rs[0]['waypoint']))
        switches = [records[i]['step'] for i in range(1, len(records))
                    if records[i - 1]['goal_is_exit'] and not records[i]['goal_is_exit']]
        path_length = sum(distance(r['before'], r['after']) for r in records)
        displacement = distance(records[0]['before'], records[-1]['after'])
        final_arrivals = [r for r in records if r['arrived'] and r['final_exit_waypoint']]
        row = dict(seed=seed, category=stage(episode), parent_clear_seconds=old_by_seed[seed]['elapsed_seconds'],
                   child_seconds=episode['elapsed_seconds'], replay_matches_original=True,
                   min_exit_distance=min(r['exit_distance_after'] for r in records),
                   final_exit_distance=episode['final_exit_distance'],
                   first_exit_visible_seconds=visible_steps[0] * .1 if visible_steps else None,
                   path_length=path_length, net_displacement=displacement,
                   waypoint_arrivals=sum(r['arrived'] for r in records),
                   exit_directed_arrivals=sum(r['arrived'] and r['goal_is_exit'] for r in records),
                   final_waypoint_assigned=any(r['final_exit_waypoint'] for r in records),
                   final_waypoint_arrivals=len(final_arrivals),
                   exit_to_exploration_switch_steps=switches,
                   assigned_waypoint_actions=int(assigned.sum()),
                   actions_away_or_perpendicular_to_waypoint=int(((new_cos <= 0) & assigned).sum()),
                   mean_waypoint_alignment=float(new_cos[assigned].mean()),
                   parent_same_state_mean_waypoint_alignment=float(old_cos[assigned].mean()),
                   parent_same_state_away_actions=int(((old_cos <= 0) & assigned).sum()),
                   child_zero_previous_action_mean_alignment=float(no_previous_cos[assigned].mean()),
                   child_prediction_max_recorded_action_error=max_error,
                   replans=episode['replans'], abandoned_goals=episode['abandoned_goals'],
                   waypoint_assignments=episode['waypoint_assignments'], assignment_segments=assignments)
        cases.append(row)
        case_dir = HERE / f'trace-{seed}'
        case_dir.mkdir(exist_ok=True)
        for name in ['run.json', 'training-config.yaml', 'evaluation-result.json']:
            shutil.copy2(run / name, case_dir / name)
        shutil.copy2(run / 'evaluation/episodes.jsonl', case_dir / 'episodes.jsonl')
        with (run / 'evaluation/exit-decisions.jsonl').open('rb') as stream, gzip.open(case_dir / 'exit-decisions.jsonl.gz', 'wb') as target:
            shutil.copyfileobj(stream, target)
        old_rs = parent_traces[seed]
        ax.plot([r['before']['x'] for r in old_rs] + [old_rs[-1]['after']['x']],
                [r['before']['z'] for r in old_rs] + [old_rs[-1]['after']['z']], color='#a0a0a0', lw=1.2, label='QA-32 clear')
        ax.plot([r['before']['x'] for r in records] + [records[-1]['after']['x']],
                [r['before']['z'] for r in records] + [records[-1]['after']['z']], color='#2563eb', lw=1.3, alpha=.8, label='QA-36 timeout')
        start, end, exit_position = records[0]['before'], records[-1]['after'], records[0]['exit']
        ax.scatter([start['x']], [start['z']], c='black', s=25, label='Spawn')
        ax.scatter([end['x']], [end['z']], marker='x', c='#dc2626', s=65, label='Timeout position')
        ax.add_patch(Rectangle((exit_position['x'] - 1, exit_position['z'] - 1), 2, 2, color='#16a34a', alpha=.8))
        ax.set(xlim=(-20, 20), ylim=(-20, 20), xlabel='World X', ylabel='World Z',
               title=f"{seed}: {'exit seen' if visible_steps else 'exit never seen'}\n{row['waypoint_arrivals']} arrivals, {row['replans']} replans")
        ax.set_aspect('equal')
        ax.grid(alpha=.15)
    handles, labels = axes.flat[0].get_legend_handles_labels()
    fig.legend(handles, labels, loc='lower center', ncol=4, bbox_to_anchor=(.5, -.025))
    fig.savefig(HERE / 'trajectories.png', dpi=160, bbox_inches='tight')
    plt.close(fig)
    assert [sha(parent_path), sha(child_path)] == hashes
    assert [(m.num_timesteps, m._n_updates) for m in [parent, child]] == counters
    summary = dict(selected_case_count=6, selection='Three newly lost undiscovered-exit and three newly lost discovered-exit timeouts; purposive, not prevalence sample',
                   parent_checkpoint_sha256=hashes[0], child_checkpoint_sha256=hashes[1], weight_updates=0,
                   all_replays_match_original=True, model_counters_unchanged=True, cases=cases,
                   same_state_comparison_limit='Parent predictions on child states show action differences only; these are not counterfactual world rollouts.',
                   offline_input_ablation_limit='Zeroing previous-action inputs measures output sensitivity on modified observations, not a validated behavioural fix.')
    (HERE / 'trace-analysis.json').write_text(json.dumps(summary, indent=2), encoding='utf-8')
    for r in cases:
        print(json.dumps({k: v for k, v in r.items() if k != 'assignment_segments'}))


if __name__ == '__main__':
    analyze()
