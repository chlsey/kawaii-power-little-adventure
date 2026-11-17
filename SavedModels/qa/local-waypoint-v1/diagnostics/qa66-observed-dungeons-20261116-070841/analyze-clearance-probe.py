"""Approximate an observed-map clearance counterfactual from recorded ray inputs."""
from pathlib import Path
from collections import deque
import math,json,gzip,sys
phase=Path(sys.argv[1]); seed=int(sys.argv[2]); dest=Path(sys.argv[3])
observed=set();walls=set();exits=set();seen=set();cellsize=.25

def cell(p):return math.floor(p[0]/cellsize),math.floor(p[1]/cellsize)
def trace(start,end,kind):
    dx,dz=end[0]-start[0],end[1]-start[1]
    if kind:
        length=math.hypot(dx,dz)
        if length:end=(end[0]+dx/length*.0001,end[1]+dz/length*.0001)
    c=cell(start);target=cell(end)
    dx,dz=end[0]-start[0],end[1]-start[1]
    sx=(dx>0)-(dx<0);sz=(dz>0)-(dz<0)
    tx=(((c[0]+(sx>0))*cellsize-start[0])/dx) if sx else float('inf')
    tz=(((c[1]+(sz>0))*cellsize-start[1])/dz) if sz else float('inf')
    ix=cellsize/abs(dx) if sx else float('inf');iz=cellsize/abs(dz) if sz else float('inf')
    for _ in range(abs(c[0]-target[0])+abs(c[1]-target[1])+2):
        observed.add(c)
        if c==target:break
        if tx<tz:c=(c[0]+sx,c[1]);tx+=ix
        elif tz<tx:c=(c[0],c[1]+sz);tz+=iz
        else:c=(c[0]+sx,c[1]+sz);tx+=ix;tz+=iz
    observed.add(target)
    if kind==1:walls.add(target)
    elif kind==2:exits.add(target)

source=phase/'actions.jsonl.gz'
with gzip.open(source,'rt',encoding='utf-8') as stream:
    for line in stream:
        row=json.loads(line)
        if row['seed']!=seed:continue
        start=(row['position']['x'],row['position']['z']);obs=row['observation'];key=(start,tuple(obs[2:130]))
        if key in seen:continue
        seen.add(key);observed.add(cell(start))
        for i in range(64):
            wall,exit=obs[2+2*i:4+2*i];kind=1 if wall>0 else (2 if exit>0 else 0)
            distance=12*(1-max(wall,exit));angle=i*2*math.pi/64
            trace(start,(start[0]+math.sin(angle)*distance,start[1]+math.cos(angle)*distance),kind)
episode=next(r for r in map(json.loads,(phase/'episodes.jsonl').read_text().splitlines()) if r['seed']==seed)
layout=next(r for r in map(json.loads,(phase/'layouts.jsonl').read_text().splitlines()) if r['seed']==seed)
position=(episode['position']['x'],episode['position']['z']);start=cell(position)
excluded=[]
with gzip.open(phase/'navigation-events.jsonl.gz','rt',encoding='utf-8') as stream:
    for line in stream:
        r=json.loads(line)
        if r['seed']==seed and r['reason']=='goal_reached' and not r['goal_is_exit']:
            excluded.append((r['waypoint']['x'],r['waypoint']['z']))

def probe(clearance):
    free=observed-walls;extent=math.ceil(clearance/cellsize+.5)
    offsets=[(x,z) for x in range(-extent,extent+1) for z in range(-extent,extent+1)
        if max(0,(abs(x)-.5)*cellsize)**2+max(0,(abs(z)-.5)*cellsize)**2<=clearance**2]
    for wx,wz in walls:
        for x,z in offsets:free.discard((wx+x,wz+z))
    reached=set();queue=deque()
    if start in free:reached.add(start);queue.append(start)
    while queue:
        x,z=queue.popleft()
        for a in (-1,0,1):
            for b in (-1,0,1):
                if not(a or b):continue
                n=(x+a,z+b)
                if n not in free or n in reached:continue
                if a and b and ((x+a,z) not in free or (x,z+b) not in free):continue
                reached.add(n);queue.append(n)
    frontiers=eligible=0
    for x,z in reached:
        point=((x+.5)*cellsize,(z+.5)*cellsize)
        if math.dist(point,position)<1:continue
        if not any((x+a,z+b) not in observed for a in (-1,0,1) for b in (-1,0,1)):continue
        frontiers+=1
        if not any(math.dist(point,p)<=.5 for p in excluded):eligible+=1
    return dict(clearance=clearance,start_free=start in free,reachable_cells=len(reached),frontiers_at_least_one_unit_away=frontiers,eligible_frontiers_after_completed_goal_exclusions=eligible)
rad=layout['actor_radius'];skin=layout['actor_skin_width'];scale=max(layout['actor_scale']['x'],layout['actor_scale']['z'])
result=dict(seed=seed,source='Reconstructed from serialized ray inputs; float-rounding differences from Unity are possible.',
    actual_status=episode['navigation_status'],actor_radius=rad,actor_skin_width=skin,actor_scale=scale,
    observed_cells=len(observed),wall_cells=len(walls),unique_ray_states=len(seen),
    normal=probe(rad+skin+.05),scaled_radius_only=probe(rad*scale+skin+.05),physical_radius_only=probe(rad*scale),no_inflation=probe(0.0))
dest.write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps(result,indent=2))
