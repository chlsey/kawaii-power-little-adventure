"""Count route -> escape -> identical route/position recurrences in a frozen trace."""
from collections import defaultdict
from pathlib import Path
import json
import sys

run = Path(sys.argv[1])
destination = Path(sys.argv[2])
phase = run / 'evaluation'
episodes = {r['seed']: r for r in (json.loads(s) for s in (phase/'episodes.jsonl').read_text().splitlines())}
assignments = defaultdict(list)
for line in (phase/'navigation-events.jsonl').read_text().splitlines():
    row = json.loads(line)
    if row['reason'] in ('route_assigned','escape_assigned'):
        assignments[row['seed']].append(row)

def near(a,b):
    return sum((a[k]-b[k])**2 for k in ('x','z')) < 1e-6

cases=[]
for seed, rows in assignments.items():
    count = streak = longest = 0
    last_end = None
    example = None
    for a,b,c in zip(rows,rows[1:],rows[2:]):
        if (a['reason']==c['reason']=='route_assigned' and b['reason']=='escape_assigned'
            and c['step']-a['step']==2 and near(a['position'],c['position'])
            and near(a['waypoint'],c['waypoint'])):
            count += 1
            streak = streak+1 if last_end == a['step'] else 1
            longest = max(longest,streak)
            last_end = c['step']
            if example is None:
                example = [a,b,c]
    if count:
        cases.append(dict(seed=seed,completed=episodes[seed]['completed'],
            exit_discovered=episodes[seed]['exit_discovered'],cycles=count,
            longest_consecutive_cycles=longest,example=example))
result = dict(definition='route assignment -> escape assignment -> same route target and actor X/Z position within 0.001 units, exactly two actions later',
    affected_cases=len(cases),affected_timeouts=sum(not c['completed'] for c in cases),
    two_action_cycles=sum(c['cycles'] for c in cases),
    actions_in_cycles=2*sum(c['cycles'] for c in cases),
    cases=sorted(cases,key=lambda c:c['cycles'],reverse=True))
(destination/'two-action-loops.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in result.items() if k!='cases'},indent=2))
