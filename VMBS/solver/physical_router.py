#!/usr/bin/env python3
"""Deterministic two-copper-layer routing kernel for VMBS.
Evidence-preserving: missing/unsupported rules are reported, never invented as verified facts.
"""
from pathlib import Path
import json, math, heapq, hashlib
from real_board_router import sexpr_tokens, parse, children, child, f, transform, endpoints, extract, stable
from route_order import order

COPPER=("F.Cu","B.Cu")
LAYER_ALIAS={'Front':'F.Cu','Back':'B.Cu','F.Cu':'F.Cu','B.Cu':'B.Cu'}

def _layers(node):
    x=child(node,'layers')
    return tuple(v.strip('"') for v in x[1:]) if x else ()

def pad_copper_layers(pad):
    ls=_layers(pad)
    if '*.Cu' in ls:return COPPER
    norm=tuple(LAYER_ALIAS.get(x,x) for x in ls)
    return tuple(x for x in COPPER if x in norm)

def pad_size_mm(pad):
    s=child(pad,'size')
    return (float(s[1]),float(s[2])) if s and len(s)>=3 else None
def pad_roundrect_ratio(pad):
    r=child(pad,'roundrect_rratio')
    try:return float(r[1]) if r and len(r)>=2 else None
    except:return None

def parse_netclasses(root):
    out={}
    for nc in children(root,'net_class'):
        if len(nc)<2:continue
        name=nc[1]; rec={'name':name,'nets':[]}
        for x in nc[2:]:
            if not isinstance(x,list) or not x:continue
            if x[0] in ('clearance','trace_width','via_dia','via_drill') and len(x)>1:
                try:rec[x[0]]=float(x[1])
                except ValueError:pass
            elif x[0]=='add_net' and len(x)>1:rec['nets'].append(x[1])
        out[name]=rec
    return out

def extract_physical(path):
    roots=parse(sexpr_tokens(Path(path).read_text(errors='replace')));root=roots[0]
    base=extract(path); bykey={(p['ref'],str(p['pad'])):p for p in base['pads']}
    pads=[]
    for m in children(root,'module')+children(root,'footprint'):
        at=child(m,'at'); ox,oy=f(at[1]),f(at[2]); rot=f(at[3]) if len(at)>3 else 0.0
        fp=child(m,'fp_text');ref=fp[2] if fp and len(fp)>2 and fp[1]=='reference' else '?'
        for p in children(m,'pad'):
            nn=child(p,'net');pa=child(p,'at')
            if not nn or not pa:continue
            key=(ref,str(p[1])); q=dict(bykey.get(key,{}))
            q.update({'ref':ref,'pad':str(p[1]),'net_id':int(nn[1]),'net':nn[2] if len(nn)>2 else '',
                      'copper_layers':pad_copper_layers(p),'size_mm':pad_size_mm(p),'pad_type':p[2] if len(p)>2 else None,
                      'pad_shape':p[3] if len(p)>3 else None,'roundrect_rratio':pad_roundrect_ratio(p),
                      'rotation':q.get('rotation',0.0)})
            pads.append(q)
    return {'pads':pads,'netclasses':parse_netclasses(root)}

def mm_to_cell(v,step):return int(round(v/step))
def cell_to_mm(v,step):return round(v*step,6)
def inflate_cells(mm,step):return max(0,int(math.ceil(mm/step)))

def rect_cells(center,size,step,margin=0.0):
    if not size:return set()
    cx,cy=center; hx=size[0]/2+margin;hy=size[1]/2+margin
    return {(x,y) for x in range(math.floor((cx-hx)/step),math.ceil((cx+hx)/step)+1)
                  for y in range(math.floor((cy-hy)/step),math.ceil((cy+hy)/step)+1)}

def board_bounds(poly,step):
    xs=[p[0] for p in poly];ys=[p[1] for p in poly]
    return (math.ceil(min(xs)/step),math.ceil(min(ys)/step),math.floor(max(xs)/step),math.floor(max(ys)/step))

def in_bounds(c,b):return b[0]<=c[0]<=b[2] and b[1]<=c[1]<=b[3]

def state_neighbors(s,bounds,blocked,usage,via_cost=12,congestion_cost=8):
    x,y,l=s
    for dx,dy in ((-1,0),(0,-1),(0,1),(1,0)):
        n=(x+dx,y+dy,l)
        if in_bounds(n,bounds) and n not in blocked:
            yield n,1+congestion_cost*usage.get(n,0)
    n=(x,y,1-l)
    if in_bounds(n,bounds) and n not in blocked:yield n,via_cost+congestion_cost*usage.get(n,0)

def astar3(starts,goals,bounds,blocked,usage,via_cost=12):
    starts=sorted(set(starts));goals=set(goals)
    if not starts or not goals:return None
    def h(s):return min(abs(s[0]-g[0])+abs(s[1]-g[1])+(0 if s[2]==g[2] else via_cost) for g in goals)
    q=[];cost={};prev={}
    for s in starts:cost[s]=0;prev[s]=None;heapq.heappush(q,(h(s),0,s))
    end=None
    while q:
        _,g,u=heapq.heappop(q)
        if g!=cost[u]:continue
        if u in goals:end=u;break
        for v,w in state_neighbors(u,bounds,blocked,usage,via_cost):
            ng=g+w
            if ng<cost.get(v,10**30):cost[v]=ng;prev[v]=u;heapq.heappush(q,(ng+h(v),ng,v))
    if end is None:return None
    p=[];u=end
    while u is not None:p.append(u);u=prev[u]
    return p[::-1]

def compress_path(path):
    if len(path)<=2:return path
    out=[path[0]];last=None
    for i in range(1,len(path)):
        a,b=path[i-1],path[i];d=(b[0]-a[0],b[1]-a[1],b[2]-a[2])
        if last is not None and d!=last:out.append(a)
        last=d
    out.append(path[-1]);return out

def route_metrics(path,step):
    planar=0;vias=0
    for a,b in zip(path,path[1:]):
        if a[2]!=b[2]:vias+=1
        else:planar+=abs(a[0]-b[0])+abs(a[1]-b[1])
    return {'length_mm':round(planar*step,6),'via_count':vias,'state_count':len(path)}

def route_tree(terminals,bounds,blocked,usage,via_cost=12):
    if len(terminals)<2:return []
    tree=set(terminals[0]);paths=[]
    for target in terminals[1:]:
        p=astar3(tree,target,bounds,blocked,usage,via_cost)
        if p is None:return None
        paths.append(p);tree.update(p)
    return paths

def endpoint_states(p,step):
    if not p.get('xy'):return []
    x,y=(mm_to_cell(p['xy'][0],step),mm_to_cell(p['xy'][1],step))
    ls=p.get('copper_layers') or ()
    return [(x,y,COPPER.index(l)) for l in ls if l in COPPER]

def route_design(pcb,geom,out,step=1.27):
    ph=extract_physical(pcb); g=json.load(open(geom)); poly=g['outline']['polygon_mm'];bounds=board_bounds(poly,step)
    ep={}
    for p in ph['pads']:
        if p['net_id']!=0:ep.setdefault(p['net'],[]).append(p)
    for n in ep:ep[n].sort(key=lambda p:(p['ref'],p['pad'],p.get('xy') or []))
    nc=ph['netclasses']; net_to_class={n:name for name,r in nc.items() for n in r.get('nets',[])}
    default=nc.get('Default',{})
    usage={};routes={};fails=[];evidence_gaps=[]
    for n in order([n for n,p in ep.items() if len(p)>=2]):
        ps=ep[n]; rule=nc.get(net_to_class.get(n,'Default'),default)
        if not rule or 'clearance' not in rule or 'trace_width' not in rule:
            evidence_gaps.append({'net':n,'code':'TITS_UP_NETCLASS_RULES'});continue
        terminals=[endpoint_states(p,step) for p in ps]
        if any(not t for t in terminals):fails.append({'net':n,'code':'TITS_UP_PAD_COPPER_LAYER'});continue
        # Component courtyards are not copper keepouts. Keep only board bounds here; pad/body copper obstacles
        # require explicit keepout/copper evidence and are deliberately not invented.
        blocked=set()
        rr=route_tree(terminals,bounds,blocked,usage)
        if rr is None:fails.append({'net':n,'code':'TITS_UP_ROUTE_NO_PATH'});continue
        rec=[]
        for p in rr:
            for s in p:usage[s]=usage.get(s,0)+1
            rec.append({'path':compress_path(p),'metrics':route_metrics(p,step)})
        routes[n]={'netclass':rule.get('name','Default'),'trace_width_mm':rule['trace_width'],'clearance_mm':rule['clearance'],'branches':rec}
    conflicts=sorted([list(s)+[v] for s,v in usage.items() if v>1])
    total_len=round(sum(b['metrics']['length_mm'] for r in routes.values() for b in r['branches']),6)
    total_vias=sum(b['metrics']['via_count'] for r in routes.values() for b in r['branches'])
    result={'format':'VMBS-PHYSICAL-ROUTER-CP313','grid_mm':step,'copper_layers':list(COPPER),'bounds':list(bounds),
            'pad_count':len(ph['pads']),'candidate_net_count':sum(len(x)>=2 for x in ep.values()),'routed_net_count':len(routes),
            'failures':fails,'evidence_gaps':evidence_gaps,'shared_state_count':len(conflicts),'max_usage':max(usage.values(),default=0),
            'total_length_mm':total_len,'total_vias':total_vias,'route_digest':stable(routes),'routes':routes}
    Path(out).write_text(json.dumps(result,indent=2)+'\n');return result

if __name__=='__main__':
 import argparse
 a=argparse.ArgumentParser();a.add_argument('pcb');a.add_argument('geom');a.add_argument('out');a.add_argument('--step',type=float,default=1.27);z=a.parse_args()
 r=route_design(z.pcb,z.geom,z.out,z.step);print(json.dumps({k:r[k] for k in ('pad_count','candidate_net_count','routed_net_count','shared_state_count','max_usage','total_length_mm','total_vias','route_digest')},indent=2))
