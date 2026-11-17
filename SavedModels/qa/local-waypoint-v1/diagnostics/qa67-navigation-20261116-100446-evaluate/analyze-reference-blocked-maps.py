from pathlib import Path
from collections import deque
import json,math,shutil
base=Path('SavedModels/qa/local-waypoint-v1/diagnostics/qa67-navigation-20261116-100446-evaluate');out=[]
for phase in ['reference-preflight','failure-reference']:
    root=base/phase
    if not (root/'episodes.jsonl').exists():continue
    episodes=[json.loads(s) for s in (root/'episodes.jsonl').read_text().splitlines()]
    for row in episodes:
        if row['completed']:continue
        d=json.loads((root/f"blocked-{row['seed']}-terminal.json").read_text());cells={(x['x'],x['z']):x for x in d['cells']};pos=d['position'];start=(math.floor(pos['x']/.25),math.floor(pos['z']/.25));entry=dict(seed=row['seed'],status=d['status'],excluded=len(d['excluded_points']),position_clear=d['position_clear'])
        for mode in ['planner_free_cells','observed_nonwall_cells']:
            free={k for k,v in cells.items() if (v['planner_state'] in [1,2] if mode=='planner_free_cells' else not v['wall'])};seen=set();queue=deque()
            if start in free:seen.add(start);queue.append(start)
            while queue:
                x,z=queue.popleft()
                for a in [-1,0,1]:
                    for b in [-1,0,1]:
                        k=(x+a,z+b)
                        if not(a or b) or k not in free or k in seen:continue
                        if a and b and ((x+a,z) not in free or (x,z+b) not in free):continue
                        seen.add(k);queue.append(k)
            eligible=[];frontiers=[]
            for x,z in seen:
                px,pz=(x+.5)*.25,(z+.5)*.25
                if math.hypot(px-pos['x'],pz-pos['z'])<1:continue
                if not any((x+a,z+b) not in cells for a in [-1,0,1] for b in [-1,0,1]):continue
                frontiers.append((x,z))
                if not any(math.hypot(px-p['x'],pz-p['z'])<=.5 for p in d['excluded_points']):eligible.append((x,z))
            entry[mode]=dict(reachable_cells=len(seen),frontier_cells=len(frontiers),eligible_frontiers=len(eligible))
        out.append(entry)
result=dict(status='offline terminal snapshot diagnostic; physical edge sweeps not reconstructed; a raw frontier is not proof of a safe passage',cases=out)
(base/'reference-blocked-map-audit.json').write_text(json.dumps(result,indent=2))
shutil.copy2(__file__,base/'analyze-reference-blocked-maps.py')
print(json.dumps(out,indent=2))
