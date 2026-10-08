#!/usr/bin/env python3
"""CP389+ deterministic historical-copper reuse router.
Existing same-net copper is reusable topology; disconnected islands remain separate until explicitly joined.
"""
from pathlib import Path
import json
from physical_router import *
from existing_copper import extract_existing_copper,existing_obstacles
from historical_topology import net_topology_groups,stable
from negotiated_router import _rule_for,path_occupancy,pad_obstacles,usage_from_routes,conflict_states

def connect_groups(groups,bounds,blocked,weighted,via_cost=12):
    if len(groups)<2:return []
    ordered=sorted((set(g) for g in groups if g),key=lambda g:(-len(g),min(g)))
    if len(ordered)<2:return []
    tree=set(ordered[0]);paths=[]
    for goal in ordered[1:]:
        p=astar3(tree,goal,bounds,blocked,weighted,via_cost)
        if p is None:return None
        paths.append(p);tree.update(goal);tree.update(p)
    return paths

def route_one_reuse(net,ps,rule,pads,bounds,step,other_usage,history,evidence):
    groups=net_topology_groups(net,evidence,pads,step,bounds,include_zones=True)
    if not groups:return None,'TITS_UP_NO_NET_TOPOLOGY'
    blocked=pad_obstacles(pads,net,step,rule['clearance'],bounds)
    blocked |= existing_obstacles(evidence,net,step,rule['clearance'],bounds)
    # own historical topology/pads are always legal
    for g in groups: blocked.difference_update(g)
    weighted={s:other_usage.get(s,0)*16+history.get(s,0)*4 for s in set(other_usage)|set(history)}
    rr=connect_groups(groups,bounds,blocked,weighted,via_cost=12)
    if rr is None:return None,'TITS_UP_ROUTE_NO_PATH'
    occ=set();branches=[]
    for p in rr:
        occ |= path_occupancy(p,step,rule['trace_width'],rule['clearance'],rule['via_dia'])
        branches.append({'path':compress_path(p),'metrics':route_metrics(p,step)})
    return {'netclass':rule.get('name','Default'),'trace_width_mm':rule['trace_width'],'clearance_mm':rule['clearance'],
            'via_dia_mm':rule['via_dia'],'via_drill_mm':rule['via_drill'],'historical_island_count':len(groups),
            'new_branches':branches,'occupancy':occ},None

def historical_reuse_route(pcb,geom,out,step=1.27):
    ph=extract_physical(pcb);g=json.load(open(geom));bounds=board_bounds(g['outline']['polygon_mm'],step);ev=extract_existing_copper(pcb)
    ep={}
    for p in ph['pads']:
        if p['net_id']!=0:ep.setdefault(p['net'],[]).append(p)
    for n in ep:ep[n].sort(key=lambda p:(p['ref'],p['pad'],p.get('xy') or []))
    nc=ph['netclasses'];ntc={n:name for name,r in nc.items() for n in r.get('nets',[])}
    names=order([n for n,p in ep.items() if len(p)>=2]);routes={};fails=[];gaps=[];history={}
    for n in names:
        rule=_rule_for(n,nc,ntc)
        if not rule:gaps.append({'net':n,'code':'TITS_UP_NETCLASS_VIA_RULES'});continue
        u=usage_from_routes(routes)
        r,e=route_one_reuse(n,ep[n],rule,ph['pads'],bounds,step,u,history,ev)
        if r is None:fails.append({'net':n,'code':e});continue
        routes[n]=r
    usage=usage_from_routes(routes);bad=conflict_states(usage)
    serial={n:{**{k:v for k,v in r.items() if k!='occupancy'},'new_occupancy_state_count':len(r['occupancy'])} for n,r in sorted(routes.items())}
    result={'format':'VMBS-HISTORICAL-REUSE-ROUTER-CP413','mode':'HISTORICAL_AUDIT_REPAIR','grid_mm':step,'bounds':list(bounds),
            'candidate_net_count':len(names),'routed_net_count':len(routes),'failures':fails,'evidence_gaps':gaps+ev.get('gaps',[]),
            'shared_new_state_count':len(bad),'max_new_usage':max(usage.values(),default=0),
            'new_length_mm':round(sum(b['metrics']['length_mm'] for r in routes.values() for b in r['new_branches']),6),
            'new_vias':sum(b['metrics']['via_count'] for r in routes.values() for b in r['new_branches']),
            'route_digest':stable(serial),'routes':serial}
    Path(out).write_text(json.dumps(result,indent=2)+'\n');return result

if __name__=='__main__':
 import argparse
 a=argparse.ArgumentParser();a.add_argument('pcb');a.add_argument('geom');a.add_argument('out');a.add_argument('--step',type=float,default=1.27);z=a.parse_args()
 r=historical_reuse_route(z.pcb,z.geom,z.out,z.step)
 print(json.dumps({k:r[k] for k in ('candidate_net_count','routed_net_count','shared_new_state_count','max_new_usage','new_length_mm','new_vias','route_digest')},indent=2))
