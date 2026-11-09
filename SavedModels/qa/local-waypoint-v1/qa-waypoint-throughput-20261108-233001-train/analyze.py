"""Reconcile the pre-waypoint restart and matched 60-second throughput cases."""
import gzip
import hashlib
import json
from pathlib import Path
import zipfile

import numpy as np

HERE = Path(__file__).resolve().parent
GAME = HERE.parents[3]


def read(name):
    return json.loads((HERE / name).read_text())


def rows(name):
    return [json.loads(line) for line in (HERE / name).read_text().splitlines()]


phases = ['qa43-evaluation', 'before-continuous', 'evaluation']
data = {phase: rows(phase + '/episodes.jsonl') for phase in phases}
assert all(len(value) == 100 for value in data.values())
for phase in phases:
    assert [r['seed'] for r in data[phase]] == list(range(288000,288100))
    assert all(r['steps'] == 600 and r['elapsed_seconds'] == 60 and r['continuous'] for r in data[phase])
    assert read(phase + '-result.json')['added_optimizer_updates'] == 0
    assert read(phase + '-result.json')['added_model_steps'] == 0
    assert rows(phase + '/chains.jsonl') == rows(phases[0] + '/chains.jsonl')
training = read('training-episodes.json')
assert len(training) == 500 and [r['seed'] for r in training] == list(range(13500,14000))
assert all(r['steps'] == 600 and r['elapsed_seconds'] == 60 for r in training)

rng = np.random.default_rng(44)
samples = rng.integers(0,100,(20000,100))
comparisons = {}
for parent, child in [('qa43-evaluation','before-continuous'),('before-continuous','evaluation'),('qa43-evaluation','evaluation')]:
    a = np.array([r['reached_waypoints'] for r in data[parent]])
    b = np.array([r['reached_waypoints'] for r in data[child]])
    gain = b-a
    comparisons[parent + '_to_' + child] = dict(
        parent_mean=float(a.mean()),child_mean=float(b.mean()),mean_gain=float(gain.mean()),
        paired_bootstrap_95_arrival_gain=np.percentile(gain[samples].mean(axis=1),[2.5,97.5]).tolist(),
        parent_reaching_ten=int((a>=10).sum()),child_reaching_ten=int((b>=10).sum()),
        paired_ten_gain_95_percentage_points=np.percentile(((b>=10).astype(int)-(a>=10).astype(int))[samples].mean(axis=1)*100,[2.5,97.5]).tolist(),
        more_arrivals=int((gain>0).sum()),fewer_arrivals=int((gain<0).sum()),tied=int((gain==0).sum()))

continuity = {}
for phase in phases:
    previous = None
    leg_info = {(r['seed'],r['leg']):r for r in rows(phase + '/legs.jsonl')}
    handoffs = actions = 0
    with gzip.open(HERE / phase / 'actions.jsonl.gz','rt') as stream:
        for line in stream:
            current = json.loads(line)
            if previous is None or current['seed'] != previous['seed']:
                assert current['observation'][:2] == [0,0]
            else:
                assert np.allclose(current['observation'][:2],previous['action'],atol=1e-6)
                if current['leg'] != previous['leg']:
                    assert current['leg'] == previous['leg']+1
                    endpoint = leg_info[previous['seed'],previous['leg']]
                    assert np.allclose([current['x'],current['z']], [endpoint['final_x'],endpoint['final_z']], atol=1e-5)
                    handoffs += 1
            actions += 1
            previous = current
    assert actions == 60000
    continuity[phase] = dict(actions=actions,handoffs=handoffs,previous_action_continuity_verified=True,position_continuity_verified=True)
    assert all(.9999 <= r['initial_distance'] <= 10.0001 for r in rows(phase + '/legs.jsonl'))

assert all(.9999 <= r['initial_distance'] <= 10.0001 for r in rows('training/legs.jsonl'))

archives = read('checkpoints.json')['checkpoints']
for row in archives:
    path = HERE / row['path']
    assert hashlib.sha256(path.read_bytes()).hexdigest() == row['sha256']
    with zipfile.ZipFile(path) as archive:
        assert archive.testzip() is None
run = read('run.json')
for name,digest in run['source_sha256'].items():
    assert hashlib.sha256((HERE / 'source' / name).read_bytes()).hexdigest() == digest
    assert hashlib.sha256((GAME / name).read_bytes()).hexdigest() == digest
assert hashlib.sha256(Path(run['parent_checkpoint']).read_bytes()).hexdigest() == run['parent_sha256']
result = dict(comparisons=comparisons,continuity=continuity,verified_archives=len(archives),
    bootstrap_draws=20000,bootstrap_seed=44,source_hashes_verified=True,
    new_training_episodes=500,training_steps=300000,total_restart_training_episodes=2000,
    parent_checkpoint_unchanged=True)
(HERE / 'comparison.json').write_text(json.dumps(result,indent=2,allow_nan=False))
print(json.dumps(result,indent=2))
