"""Account for every observed-dungeon instruction and navigation recurrence."""
from pathlib import Path
from collections import Counter,defaultdict
import gzip,json,math,statistics,sys
p=Path(sys.argv[1])
episodes=[json.loads(s) for s in (p/'evaluation/episodes.jsonl').read_text().splitlines()]
events=defaultdict(list)
with gzip.open(p/'evaluation/navigation-events.jsonl.gz','rt',encoding='utf-8') as f:
    for line in f:
        row=json.loads(line);events[row['seed']].append(row)
closures={'escape_arrived_free','escape_arrived_blocked','escape_invalid','start_cell_blocked','waypoint_arrived','waypoint_segment_invalid','waypoint_clearance_invalid','stalled_replan','stalled_abandon','goal_reached','goal_cell_invalid','select_exit','select_frontier','abandon_exit','abandon_frontier','oscillation_detected','escape_stalled','goal_progress_stalled'}
legs=[];cases=[];all_events=Counter()
for episode in episodes:
    current=None;rows=[]
    def close(reason,seconds,position):
        global current
        current.update(end_reason=reason,seconds=seconds-current['start_seconds'],end_position=position)
        rows.append(current);current=None
    for row in events[episode['seed']]:
        reason=row['reason'];all_events[reason]+=1
        if reason in ('route_assigned','escape_assigned'):
            if current is not None:close('unaccounted_replacement',row['seconds'],row['position'])
            current=dict(seed=row['seed'],version=row['waypoint_version'],kind=reason,
                waypoint=row['waypoint'],start_seconds=row['seconds'],start_position=row['position'])
        elif current is not None and reason in closures and row['has_waypoint'] and row['waypoint_version']==current['version']:
            close(reason,row['seconds'],row['position'])
    if current is not None:close('episode_ended',episode['elapsed_seconds'],episode['position'])
    assert len(rows)==episode['assignments'],(episode['seed'],len(rows),episode['assignments'])
    assert [r['version'] for r in rows]==list(range(1,len(rows)+1))
    counts=Counter(r['end_reason'] for r in rows)
    cases.append(dict(seed=episode['seed'],assignments=len(rows),end_reasons=dict(counts),
        no_waypoint_actions=episode['no_waypoint_actions'],stuck_windows=episode['stuck_events'],
        exit_discovered=episode['exit_discovered'],completed=episode['completed']))
    legs.extend(rows)
reasons=Counter(r['end_reason'] for r in legs)
actions=sum(e['steps'] for e in episodes);no_waypoint=sum(e['no_waypoint_actions'] for e in episodes)
guided=actions-no_waypoint
arrivals=reasons['waypoint_arrived']+reasons['escape_arrived_free']
replaced=len(legs)-arrivals-reasons['episode_ended']
mode={};states=defaultdict(list)
for seed,rows in events.items():
    for r in rows:
        if r['reason'] in ('route_assigned','escape_assigned'):mode[seed,r['waypoint_version']]=r['reason']=='escape_assigned'
with gzip.open(p/'evaluation/actions.jsonl.gz','rt',encoding='utf-8') as f:
    for line in f:
        r=json.loads(line)
        states[r['seed']].append((r['position']['x'],r['position']['z'],r['waypoint']['x'],r['waypoint']['z'],r['has_waypoint'],mode.get((r['seed'],r['waypoint_version']),False),r['step']))
returns=[]
for seed,rows in states.items():
    prefix=[0]
    for r in rows:prefix.append(prefix[-1]+r[5])
    counts=Counter()
    for i,r in enumerate(rows):
        if i<2 or not r[4]:continue
        previous=rows[i-1]
        if (r[0]-previous[0])**2+(r[1]-previous[1])**2<.05**2:continue
        for lag in range(2,min(i,100)+1):
            old=rows[i-lag]
            if old[4] and prefix[i+1]-prefix[i-lag]>0 and (r[0]-old[0])**2+(r[1]-old[1])**2<=.1**2 and (r[2]-old[2])**2+(r[3]-old[3])**2<=.25**2:
                counts[lag]+=1;break
    if counts:returns.append(dict(seed=seed,return_states=sum(counts.values()),nearest_lags=dict(counts)))
audit=dict(status='complete-policy-audit; see campaign for reference status',cases=len(episodes),
    actions=actions,actions_with_waypoint=guided,actions_without_waypoint=no_waypoint,
    no_waypoint_percent=100*no_waypoint/actions,
    assignments=len(legs),assignments_per_100_actions=100*len(legs)/actions,
    assignments_per_100_guided_actions=100*len(legs)/guided,
    assignment_arrivals=arrivals,assignments_replaced_before_arrival=replaced,
    assignment_end_reasons=dict(reasons),median_instruction_seconds=statistics.median(r['seconds'] for r in legs),
    median_assignments_per_case=statistics.median(e['assignments'] for e in episodes),
    max_assignments_per_case=max(e['assignments'] for e in episodes),
    event_counts=dict(all_events),broader_recovery_return_states=sum(r['return_states'] for r in returns),
    cases_with_recovery_returns=len(returns),recovery_returns=returns,
    exits=sum(e['completed'] for e in episodes),discoveries=sum(e['exit_discovered'] for e in episodes),
    entered_exit_room=sum(e['entered_exit_room'] for e in episodes),
    final_phase=dict(Counter(e['final_phase'] for e in episodes)),
    final_navigation_status=dict(Counter(e['navigation_status'] for e in episodes)),
    low_movement_actions_under_0_02=sum(e['low_movement_actions'] for e in episodes),
    stuck_windows=sum(e['stuck_events'] for e in episodes),cases_with_stuck_windows=sum(e['stuck_events']>0 for e in episodes),
    away_or_perpendicular_guided_actions=sum(e['away_actions'] for e in episodes),
    total_path_length=sum(e['path_length'] for e in episodes),
    cases_below_floor=sum(e['position']['y'] < -1 for e in episodes),
    per_case=cases)
(p/'navigation-audit.json').write_text(json.dumps(audit,indent=2),encoding='utf-8')
(p/'navigation-assignment-audit.json').write_text(json.dumps(legs,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in audit.items() if k not in ('per_case','recovery_returns')},indent=2))

# route -> escape -> same route target and position two actions later -> count exact short cycles
cycles=[]
for seed,rows in events.items():
    assignments=[r for r in rows if r['reason'] in ('route_assigned','escape_assigned')]
    count=0
    for a,b,c in zip(assignments,assignments[1:],assignments[2:]):
        if a['reason']!='route_assigned' or b['reason']!='escape_assigned' or c['reason']!='route_assigned':continue
        if c['step']-a['step']!=2:continue
        if math.dist([a['position'][k] for k in ['x','z']],[c['position'][k] for k in ['x','z']])>.001:continue
        if math.dist([a['waypoint'][k] for k in ['x','z']],[c['waypoint'][k] for k in ['x','z']])>.001:continue
        count+=1
    if count:cycles.append(dict(seed=seed,cycles=count))
short=dict(definition='route assignment -> escape assignment -> same route target and actor X/Z position within 0.001 units, exactly two actions later',affected_cases=len(cycles),two_action_cycles=sum(r['cycles'] for r in cycles),cases=cycles)
(p/'two-action-loops.json').write_text(json.dumps(short,indent=2),encoding='utf-8')
