"""Compare recovery-related repeated navigation states, including longer cycles."""
from collections import Counter,defaultdict
from pathlib import Path
import gzip,json,sys

baseline=Path(sys.argv[1])
current=Path(sys.argv[2])

def analyze(directory):
    kinds={}
    with gzip.open(directory/'navigation-events.jsonl.gz','rt',encoding='utf-8') as stream:
        for line in stream:
            r=json.loads(line)
            if r['reason'] in ('route_assigned','escape_assigned'):
                kinds[r['seed'],r['waypoint_version']]=r['reason']=='escape_assigned'
    states=defaultdict(list)
    with gzip.open(directory/'exit-decisions.jsonl.gz','rt',encoding='utf-8') as stream:
        for line in stream:
            r=json.loads(line)
            if not r['active']:continue
            states[r['seed']].append((r['before']['x'],r['before']['z'],
                r['waypoint']['x'],r['waypoint']['z'],r['has_waypoint'],
                kinds.get((r['seed'],r['waypoint_version']),False),r['step']))
    cases=[]; total=0; hist=Counter()
    for seed,rows in states.items():
        prefix=[0]
        for r in rows:prefix.append(prefix[-1]+r[5])
        count=0; lags=Counter()
        for i,r in enumerate(rows):
            if i<2 or not r[4]:continue
            previous=rows[i-1]
            if (r[0]-previous[0])**2+(r[1]-previous[1])**2<.05**2:continue
            for lag in range(2,min(i,100)+1):
                old=rows[i-lag]
                if not old[4] or prefix[i+1]-prefix[i-lag]==0:continue
                if ((r[0]-old[0])**2+(r[1]-old[1])**2<=.1**2 and
                    (r[2]-old[2])**2+(r[3]-old[3])**2<=.25**2):
                    count+=1;lags[lag]+=1;break
        if count:cases.append(dict(seed=seed,return_states=count,nearest_return_lags=dict(lags)))
        total+=count;hist.update(lags)
    return dict(actions=sum(map(len,states.values())),return_states=total,
        cases_with_returns=len(cases),nearest_return_lags=dict(hist),
        cases=sorted(cases,key=lambda x:x['return_states'],reverse=True))

result=dict(definition='Before-action position within 0.1 units and waypoint within 0.25 units of a state 2-100 actions earlier, with recovery in the interval and at least 0.05 units movement since the prior action; counted once per returning action.',
    limits=['Revisiting a state does not by itself prove wasted movement; discoveries can change the world knowledge.',
        'Counts are returning states, not the number or duration of distinct cycles.'],
    before=analyze(baseline),after=analyze(current))
(current/'recovery-return-states.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps({k:{x:v for x,v in result[k].items() if x not in ('cases','nearest_return_lags')} for k in ('before','after')},indent=2))
