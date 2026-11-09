"""Reconcile QA-42's saved blocks and matched frozen chain evaluation."""
import gzip
import hashlib
import json
import math
from pathlib import Path
import zipfile
import numpy as np

HERE = Path(__file__).resolve().parent
GAME = HERE.parents[3]


def read(name):
    return json.loads((HERE / name).read_text())


def rows(path):
    return [json.loads(line) for line in path.read_text().splitlines()]


parent = rows(HERE / 'parent-evaluation/episodes.jsonl')
child = rows(HERE / 'evaluation/episodes.jsonl')
training = read('training-episodes.json')
assert len(parent) == len(child) == 100 and len(training) == 500
assert [r['seed'] for r in training] == list(range(11000, 11500))
assert rows(HERE / 'parent-evaluation/chains.jsonl') == rows(HERE / 'evaluation/chains.jsonl')
for name in ['training-1','training-2','training-3','training-4']:
    data = rows(HERE / name / 'episodes.jsonl')
    lengths = {r['planned_waypoints'] for r in data}
    if name != 'training-4':
        assert lengths == {int(name[-1]) + 1}
    else:
        assert lengths == set(range(1, 11))
    for row in rows(HERE / name / 'legs.jsonl'):
        assert .9999 <= row['initial_distance'] <= 10.0001

p = np.array([r['reached'] for r in parent], dtype=int)
c = np.array([r['reached'] for r in child], dtype=int)
gain = int(np.sum((p == 0) & (c == 1)))
loss = int(np.sum((p == 1) & (c == 0)))
n = gain + loss
paired_p = min(1., 2 * sum(math.comb(n, i) for i in range(min(gain, loss) + 1)) / 2**n) if n else 1.
rng = np.random.default_rng(42)
sample = rng.integers(0,100,(20000,100))
completion_ci = np.percentile((c-p)[sample].mean(axis=1)*100,[2.5,97.5]).tolist()
pt = np.array([r['elapsed_seconds'] for r in parent])
ct = np.array([r['elapsed_seconds'] for r in child])
time_gain_ci = np.percentile((pt-ct)[sample].mean(axis=1),[2.5,97.5]).tolist()

continuity = {}
for phase in ['parent-evaluation','evaluation']:
    previous = None
    handoffs = away = count = 0
    with gzip.open(HERE / phase / 'actions.jsonl.gz','rt') as stream:
        for line in stream:
            row = json.loads(line)
            if previous is None or previous['seed'] != row['seed']:
                assert row['observation'][:2] == [0, 0]
            else:
                assert np.allclose(row['observation'][:2],previous['action'],atol=1e-6)
                if row['leg'] != previous['leg']:
                    assert row['leg'] == previous['leg'] + 1
                    handoffs += 1
            if np.dot(row['action'],row['observation'][131:133]) <= 0:
                away += 1
            count += 1
            previous = row
    assert count == sum(r['steps'] for r in (parent if phase == 'parent-evaluation' else child))
    continuity[phase] = dict(previous_action_continuity_verified=True, handoffs=handoffs,
                             away_or_perpendicular_commands=away, actions=count)

archives = read('checkpoints.json')['checkpoints']
for row in archives:
    path = HERE / row['path']
    assert hashlib.sha256(path.read_bytes()).hexdigest() == row['sha256']
    assert path.stat().st_size == row['bytes']
    with zipfile.ZipFile(path) as archive:
        assert archive.testzip() is None
run = read('run.json')
for name,digest in run['source_sha256'].items():
    assert hashlib.sha256((GAME/name).read_bytes()).hexdigest() == digest,name
    assert hashlib.sha256((HERE/'source'/name).read_bytes()).hexdigest() == digest,name
assert hashlib.sha256(Path(run['parent_checkpoint']).read_bytes()).hexdigest() == run['parent_sha256']
assert read('evaluation-result.json')['added_optimizer_updates'] == 0
assert read('evaluation-result.json')['added_model_steps'] == 0
length_summary=[]
mixed=training[300:]
for length in range(1,11):
    subset=[r for r in mixed if r['planned_waypoints']==length]
    length_summary.append(dict(length=length,episodes=len(subset),successes=sum(r['reached'] for r in subset)))
result=dict(parent_clears=int(p.sum()),child_clears=int(c.sum()),both_clear=int(np.sum((p==1)&(c==1))),
            child_only=gain,parent_only=loss,both_fail=int(np.sum((p==0)&(c==0))),
            gain_percentage_points=float((c-p).mean()*100),paired_bootstrap_95_percent_points=completion_ci,
            exact_paired_p=paired_p,parent_mean_seconds=float(pt.mean()),child_mean_seconds=float(ct.mean()),
            mean_seconds_saved=float((pt-ct).mean()),paired_time_gain_95_seconds=time_gain_ci,
            common_success_count=int(np.sum((p==1)&(c==1))),
            common_success_parent_mean_seconds=float(pt[(p==1)&(c==1)].mean()),
            common_success_child_mean_seconds=float(ct[(p==1)&(c==1)].mean()),
            bootstrap_draws=20000,bootstrap_seed=42,mixed_block_lengths=length_summary,
            per_waypoint_cumulative=[dict(waypoint=i,parent=sum(r['reached_waypoints']>=i for r in parent),
                child=sum(r['reached_waypoints']>=i for r in child)) for i in range(1,11)],
            continuity=continuity,verified_archives=len(archives),training_seed_count=500,
            parent_checkpoint_unchanged=True,source_hashes_verified=True)
(HERE/'comparison.json').write_text(json.dumps(result,indent=2,allow_nan=False))
print(json.dumps(result,indent=2))
