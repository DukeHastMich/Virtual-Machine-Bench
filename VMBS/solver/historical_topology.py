#!/usr/bin/env python3
"""Deterministic reconstruction of explicit same-net historical copper topology.
CP389+: this is an audit/reuse layer, not evidence that unsupported copper fill or intent existed.
"""
from collections import deque
import hashlib,json,math
from existing_copper import disk_offsets,_line_cells,_point_in_poly
from physical_router import COPPER,mm_to_cell,in_bounds,rect_cells,endpoint_states,extract_physical,board_bounds

def stable(x): return hashlib.sha256(json.dumps(x,sort_keys=True,separators=(',',':')).encode()).hexdigest()

def net_copper_states(evidence,net,step,bounds,include_zones=True):
    out=set()
    for s in evidence.get('segments',()):
        if s.get('net')!=net or s.get('layer') not in COPPER: continue
        r=max(0,int(math.ceil((s['width_mm']/2)/step))); li=COPPER.index(s['layer'])
        for x,y in _line_cells(s['start'],s['end'],step):
            for dx,dy in disk_offsets(r):
                q=(x+dx,y+dy,li)
                if in_bounds(q,bounds): out.add(q)
    for v in evidence.get('vias',()):
        if v.get('net')!=net: continue
        r=max(0,int(math.ceil((v['size_mm']/2)/step))); cx,cy=mm_to_cell(v['at'][0],step),mm_to_cell(v['at'][1],step)
        for dx,dy in disk_offsets(r):
            for li in range(len(COPPER)):
                q=(cx+dx,cy+dy,li)
                if in_bounds(q,bounds): out.add(q)
    if include_zones:
        for z in evidence.get('zones',()):
            if z.get('net')!=net or z.get('layer') not in COPPER: continue
            li=COPPER.index(z['layer']); p=z['polygon_mm']; xs=[q[0] for q in p];ys=[q[1] for q in p]
            for x in range(math.floor(min(xs)/step),math.ceil(max(xs)/step)+1):
                for y in range(math.floor(min(ys)/step),math.ceil(max(ys)/step)+1):
                    q=(x,y,li)
                    if in_bounds(q,bounds) and _point_in_poly(x*step,y*step,p): out.add(q)
    return out

def pad_states(pad,step,bounds):
    if not pad.get('xy') or not pad.get('size_mm'): return set()
    out=set()
    for x,y in rect_cells(pad['xy'],pad['size_mm'],step,0):
        for layer in pad.get('copper_layers') or ():
            if layer in COPPER:
                q=(x,y,COPPER.index(layer))
                if in_bounds(q,bounds):out.add(q)
    return out

def adjacency_components(states):
    return components(states)

def components(states):
    unseen=set(states); out=[]
    while unseen:
        seed=min(unseen); unseen.remove(seed); comp={seed}; q=deque([seed])
        while q:
            x,y,l=q.popleft()
            for n in ((x-1,y,l),(x+1,y,l),(x,y-1,l),(x,y+1,l),(x,y,1-l)):
                if n in unseen: unseen.remove(n);comp.add(n);q.append(n)
        out.append(comp)
    out.sort(key=lambda c:(-len(c),min(c)))
    return out

def audit_net(net,evidence,pads,step,bounds,include_zones=True):
    copper=net_copper_states(evidence,net,step,bounds,include_zones)
    npads=[p for p in pads if p.get('net')==net]
    psets=[pad_states(p,step,bounds) for p in npads]
    combined=set(copper)
    for s in psets:combined |= s
    comps=components(combined)
    index={}
    for ci,c in enumerate(comps):
        for s in c:index[s]=ci
    contacts=[];gaps=[]
    for p,ps in zip(npads,psets):
        touched=sorted({index[s] for s in ps if s in index})
        copper_touch=bool(ps & copper)
        rec={'ref':p.get('ref'),'pad':p.get('pad'),'components':touched,'touches_explicit_copper':copper_touch}
        contacts.append(rec)
        if not ps:gaps.append({'code':'TITS_UP_PAD_GEOMETRY','net':net,'ref':p.get('ref'),'pad':p.get('pad')})
        elif not copper_touch:gaps.append({'code':'TITS_UP_PAD_NOT_CONNECTED_TO_EXPLICIT_COPPER','net':net,'ref':p.get('ref'),'pad':p.get('pad')})
    touched_components=sorted({c for r in contacts if r['touches_explicit_copper'] for c in r['components']})
    all_touch=bool(contacts) and all(r['touches_explicit_copper'] for r in contacts)
    connected=all_touch and len(touched_components)==1
    return {'net':net,'copper_state_count':len(copper),'component_count':len(comps),
            'pad_count':len(npads),'pads_touching_copper':sum(r['touches_explicit_copper'] for r in contacts),
            'touched_component_count':len(touched_components),'historical_pad_connectivity':'CONNECTED' if connected else 'UNRESOLVED',
            'contacts':contacts,'evidence_gaps':gaps}

def audit_board(pcb,geom,evidence,step=0.635):
    import json
    ph=extract_physical(pcb); g=json.load(open(geom)); bounds=board_bounds(g['outline']['polygon_mm'],step)
    nets=sorted({p['net'] for p in ph['pads'] if p.get('net_id') and p.get('net')})
    records=[audit_net(n,evidence,ph['pads'],step,bounds) for n in nets]
    serial={'grid_mm':step,'bounds':list(bounds),'net_count':len(records),
            'connected_net_count':sum(r['historical_pad_connectivity']=='CONNECTED' for r in records),
            'unresolved_net_count':sum(r['historical_pad_connectivity']!='CONNECTED' for r in records),
            'evidence_gap_count':sum(len(r['evidence_gaps']) for r in records),'nets':records}
    return {**serial,'state':'TITS_UP' if serial['evidence_gap_count'] else 'INSPECTED','digest':stable(serial)}

def net_topology_groups(net,evidence,pads,step,bounds,include_zones=True):
    """Return explicit same-net copper+pad islands. Islands are NOT assumed mutually connected."""
    copper=net_copper_states(evidence,net,step,bounds,include_zones)
    combined=set(copper)
    for p in pads:
        if p.get('net')==net: combined |= pad_states(p,step,bounds)
    return components(combined)
