"""Independent two-layer copper connectivity and clearance audit for the experiment."""
import json
import sys
from collections import Counter, defaultdict
from pathlib import Path
from board_geometry import pads, outline, load, xy, Point, LineString
from shapely.strtree import STRtree
from sexpr import children, child, atom


def copper(board):
    """Treat plated pads/vias as connected across layers; keep SMD pads layer-specific."""
    items=[]
    for p in pads(board):
        items.append(dict(net=p['net'],geom=p['geom'],layers=p['layers'],
                          kind='pad',label=p['ref']+'-'+p['pin'],pin=True))
    aliases={'Front':0,'F.Cu':0,'Back':1,'B.Cu':1}
    for i,s in enumerate(children(board,'segment')):
        line=LineString([xy(s,'start'),xy(s,'end')]); width=float(child(s,'width')[1])
        items.append(dict(net=int(child(s,'net')[1]),geom=line.buffer(width/2,quad_segs=16),
                          layers=[aliases[atom(child(s,'layer')[1])]],kind='trace',label=f'trace{i}',pin=False))
    for i,v in enumerate(children(board,'via')):
        items.append(dict(net=int(child(v,'net')[1]),geom=Point(xy(v,'at')).buffer(float(child(v,'size')[1])/2,quad_segs=32),
                          layers=[0,1],kind='via',label=f'via{i}',pin=False))
    return items


def validate(path, output=None):
    """Audit copper geometry independently of the router; not a complete manufacturing DRC."""
    board=load(path); items=copper(board); boundary=outline(board)
    parent=list(range(len(items)))
    def root(i):
        while parent[i]!=i:
            parent[i]=parent[parent[i]];i=parent[i]
        return i
    def union(i,j):
        parent[root(i)]=root(j)
    tree=STRtree([p['geom'] for p in items]); conflicts=[]; shorts=[]
    clearance=0.2; tolerance=0.002
    for i,p in enumerate(items):
        for j in tree.query(p['geom'].buffer(clearance)):
            if j<=i: continue
            q=items[j]
            if not set(p['layers']) & set(q['layers']):continue
            distance=p['geom'].distance(q['geom'])
            if p['net']==q['net'] and p['net']:
                if distance<=0.00001:union(i,int(j))
            elif distance < clearance-tolerance:
                record=dict(a=p['label'],b=q['label'],nets=[p['net'],q['net']],
                            kind=p['kind']+'-'+q['kind'],distance_mm=round(distance,6))
                conflicts.append(record)
                if p['geom'].intersection(q['geom']).area>1e-8:shorts.append(record)
    groups=defaultdict(lambda:defaultdict(list))
    for i,p in enumerate(items):
        if p['pin'] and p['net']:groups[p['net']][root(i)].append(p['label'])
    disconnected={str(net):list(cs.values()) for net,cs in groups.items() if len(cs)>1}
    outside=[p['label'] for p in items if p['kind']!='pad' and not boundary.buffer(tolerance).covers(p['geom'])]
    report=dict(file=str(path),items=dict(Counter(p['kind'] for p in items)),
                disconnected_nets=len(disconnected),missing_links=sum(len(cs)-1 for cs in disconnected.values()),
                clearance_conflicts=len(conflicts),physical_short_pairs=len(shorts),outside_copper=outside,
                disconnected=disconnected,conflicts=conflicts,shorts=shorts,
                limitations='2um clearance tolerance; curve polygon approximation; no hole-to-hole, mask, or fabrication checks')
    if output:Path(output).write_text(json.dumps(report,indent=2))
    print({k:v for k,v in report.items() if k not in ('conflicts','shorts','disconnected','outside_copper')})
    return report


if __name__=='__main__':validate(sys.argv[1],sys.argv[2] if len(sys.argv)>2 else None)
