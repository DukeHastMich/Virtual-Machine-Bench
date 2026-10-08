#!/usr/bin/env python3
"""VMBS deterministic negotiated PCB router.
CP314+ continuation: physical occupancy, pad keepouts, via envelopes and deterministic rip-up/reroute.
No learned behavior; every choice is stable and rule/evidence driven.
"""
from pathlib import Path
import json, math, hashlib
from physical_router import *
from existing_copper import extract_existing_copper, existing_obstacles

def disk_offsets(radius):
    r=max(0,int(radius)); return tuple((x,y) for x in range(-r,r+1) for y in range(-r,r+1) if x*x+y*y<=r*r)

def corridor_radius_cells(width_mm,clearance_mm,step):
    return max(0,int(math.ceil((width_mm/2.0+clearance_mm)/step)))

def via_radius_cells(via_dia_mm,clearance_mm,step):
    return max(0,int(math.ceil((via_dia_mm/2.0+clearance_mm)/step)))

def path_occupancy(path,step,width,clearance,via_dia):
    """Return occupied copper states. Planar trace envelope stays on its layer; via envelope occupies both."""
    tr=corridor_radius_cells(width,clearance,step); vr=via_radius_cells(via_dia,clearance,step)
    out=set()
    for i,s in enumerate(path):
        rad=tr
        if (i and path[i-1][2]!=s[2]) or (i+1<len(path) and path[i+1][2]!=s[2]): rad=max(rad,vr)
        for dx,dy in disk_offsets(rad):
            out.add((s[0]+dx,s[1]+dy,s[2]))
            if rad==vr and ((i and path[i-1][2]!=s[2]) or (i+1<len(path) and path[i+1][2]!=s[2])):
                out.add((s[0]+dx,s[1]+dy,1-s[2]))
    return out

def pad_obstacles(pads,current_net,step,clearance,bounds):
    out=set()
    for p in pads:
        if p.get('net')==current_net or not p.get('xy') or not p.get('size_mm'): continue
        cells=rect_cells(p['xy'],p['size_mm'],step,clearance)
        for x,y in cells:
            for layer in p.get('copper_layers') or ():
                if layer in COPPER and in_bounds((x,y,0),bounds):out.add((x,y,COPPER.index(layer)))
    return out

def usage_from_routes(routes):
    u={}
    for n,r in routes.items():
        for s in r['occupancy']:u[s]=u.get(s,0)+1
    return u

def conflict_score(route,usage):return sum(max(0,usage.get(s,0)-1) for s in route['occupancy'])
def conflict_states(usage):return {s:v for s,v in usage.items() if v>1}

def _rule_for(net,nc,net_to_class):
    r=nc.get(net_to_class.get(net,'Default'),nc.get('Default',{}))
    need=('trace_width','clearance','via_dia','via_drill')
    return r if r and all(k in r for k in need) else None

def route_one(net,ps,rule,pads,bounds,step,other_usage,history,existing=None):
    terminals=[endpoint_states(p,step) for p in ps]
    if any(not t for t in terminals):return None,'TITS_UP_PAD_COPPER_LAYER'
    blocked=pad_obstacles(pads,net,step,rule['clearance'],bounds)
    if existing is not None: blocked |= existing_obstacles(existing,net,step,rule['clearance'],bounds)
    # terminal cells are legal for their own net
    for ts in terminals:
        for s in ts:blocked.discard(s)
    weighted={s:other_usage.get(s,0)*16+history.get(s,0)*4 for s in set(other_usage)|set(history)}
    rr=route_tree(terminals,bounds,blocked,weighted,via_cost=12)
    if rr is None:return None,'TITS_UP_ROUTE_NO_PATH'
    occ=set(); branches=[]
    for p in rr:
        occ |= path_occupancy(p,step,rule['trace_width'],rule['clearance'],rule['via_dia'])
        branches.append({'path':compress_path(p),'metrics':route_metrics(p,step)})
    return {'netclass':rule.get('name','Default'),'trace_width_mm':rule['trace_width'],'clearance_mm':rule['clearance'],
            'via_dia_mm':rule['via_dia'],'via_drill_mm':rule['via_drill'],'branches':branches,'occupancy':occ},None

def negotiated_route(pcb,geom,out,step=1.27,passes=8):
    ph=extract_physical(pcb);g=json.load(open(geom));bounds=board_bounds(g['outline']['polygon_mm'],step); existing=extract_existing_copper(pcb)
    ep={}
    for p in ph['pads']:
        if p['net_id']!=0:ep.setdefault(p['net'],[]).append(p)
    for n in ep:ep[n].sort(key=lambda p:(p['ref'],p['pad'],p.get('xy') or []))
    nc=ph['netclasses'];ntc={n:name for name,r in nc.items() for n in r.get('nets',[])}
    names=order([n for n,p in ep.items() if len(p)>=2]); routes={};fails=[];gaps=[];history={};pass_stats=[]
    # initial deterministic routing
    for n in names:
        rule=_rule_for(n,nc,ntc)
        if not rule:gaps.append({'net':n,'code':'TITS_UP_NETCLASS_VIA_RULES'});continue
        u=usage_from_routes(routes);r,e=route_one(n,ep[n],rule,ph['pads'],bounds,step,u,history,existing)
        if r is None:fails.append({'net':n,'code':e});continue
        routes[n]=r
    best_routes=routes.copy();best_usage=usage_from_routes(routes);best_metric=(len(conflict_states(best_usage)),sum(v-1 for v in best_usage.values() if v>1))
    pass_stats.append({'pass':0,'shared_states':best_metric[0],'excess_occupancy':best_metric[1]})
    # PathFinder-style deterministic negotiated congestion: conflicted nets rerouted in descending conflict score.
    for pi in range(1,passes+1):
        usage=usage_from_routes(routes);bad=conflict_states(usage)
        if not bad:break
        for s,v in bad.items():history[s]=history.get(s,0)+(v-1)
        victims=sorted(routes,key=lambda n:(-conflict_score(routes[n],usage),n))
        changed=False
        for n in victims:
            if conflict_score(routes[n],usage)==0:continue
            old=routes[n]; other={s:v for s,v in usage.items()}
            for s in old['occupancy']:
                other[s]-=1
                if other[s]<=0:other.pop(s,None)
            rule=_rule_for(n,nc,ntc);r,e=route_one(n,ep[n],rule,ph['pads'],bounds,step,other,history,existing)
            if r is None:continue
            trial=dict(routes);trial[n]=r;tu=usage_from_routes(trial)
            oldm=(len(conflict_states(usage)),sum(v-1 for v in usage.values() if v>1))
            newm=(len(conflict_states(tu)),sum(v-1 for v in tu.values() if v>1))
            # accept non-worsening congestion, then shorter/via-fewer route as deterministic tie-break
            oldlen=sum(b['metrics']['length_mm'] for b in old['branches']);newlen=sum(b['metrics']['length_mm'] for b in r['branches'])
            oldvias=sum(b['metrics']['via_count'] for b in old['branches']);newvias=sum(b['metrics']['via_count'] for b in r['branches'])
            if newm<oldm or (newm==oldm and (newvias,newlen)<(oldvias,oldlen)):
                routes=trial;usage=tu;changed=True
        usage=usage_from_routes(routes);m=(len(conflict_states(usage)),sum(v-1 for v in usage.values() if v>1))
        pass_stats.append({'pass':pi,'shared_states':m[0],'excess_occupancy':m[1]})
        if m<best_metric:best_metric=m;best_routes=routes.copy();best_usage=usage.copy()
        if not changed:break
    routes=best_routes;usage=best_usage;bad=conflict_states(usage)
    serial={n:{**{k:v for k,v in r.items() if k!='occupancy'},'occupancy_state_count':len(r['occupancy'])} for n,r in sorted(routes.items())}
    result={'format':'VMBS-NEGOTIATED-ROUTER-CP338','grid_mm':step,'passes_requested':passes,'passes_run':len(pass_stats)-1,
      'bounds':list(bounds),'pad_count':len(ph['pads']),'candidate_net_count':len(names),'routed_net_count':len(routes),
      'failures':fails,'evidence_gaps':gaps+existing.get('gaps',[]),'shared_state_count':len(bad),'excess_occupancy':sum(v-1 for v in bad.values()),
      'max_usage':max(usage.values(),default=0),'routing_closed':not bad and not fails and not gaps,
      'total_length_mm':round(sum(b['metrics']['length_mm'] for r in routes.values() for b in r['branches']),6),
      'total_vias':sum(b['metrics']['via_count'] for r in routes.values() for b in r['branches']),
      'pass_stats':pass_stats,'route_digest':stable(serial),'routes':serial}
    Path(out).write_text(json.dumps(result,indent=2)+'\n');return result

if __name__=='__main__':
 import argparse
 a=argparse.ArgumentParser();a.add_argument('pcb');a.add_argument('geom');a.add_argument('out');a.add_argument('--step',type=float,default=1.27);a.add_argument('--passes',type=int,default=8);z=a.parse_args()
 r=negotiated_route(z.pcb,z.geom,z.out,z.step,z.passes);print(json.dumps({k:r[k] for k in ('routed_net_count','shared_state_count','excess_occupancy','max_usage','routing_closed','total_length_mm','total_vias','route_digest')},indent=2))
