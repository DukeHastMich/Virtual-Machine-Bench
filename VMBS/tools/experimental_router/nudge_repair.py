"""Bounded trace-only repair for KiCad edge and hole clearance violations.

Never moves footprints, pads or vias. Moves shared trace vertices together and
commits only after independent copper validation and upstream KiCad DRC.
"""
import argparse
from collections import Counter
from copy import deepcopy
import contextlib
import io
import json
import math
from pathlib import Path
import re
import time
from board_geometry import load, pads, xy, Point, LineString
from sexpr import child, children, atom, dump
from validate_board import validate, copper
from kicad_check import find_cli, run_check

TARGETS={'copper_edge_clearance','hole_clearance'}

def target_count(report):
    return sum(i.get('type') in TARGETS for i in report.get('findings',[]))

def other_issues(report):
    """Stable issue identities: KiCad generates new UUIDs for old-format tracks."""
    return Counter(json.dumps((i.get('type'),i.get('severity'),i.get('description'),
        sorted((re.sub(r', length .*','',x.get('description','')),
                x.get('pos',{}).get('x'),x.get('pos',{}).get('y')) for x in i.get('items',[]))),sort_keys=True)
        for i in report.get('findings',[]) if i.get('type') not in TARGETS)

def acceptable(before,after):
    return (after.get('status') in ('passed','review','failed')
            and target_count(after)<target_count(before)
            and not (other_issues(after)-other_issues(before))
            and after.get('excluded',0)==before.get('excluded',0)
            and after.get('ignored_checks',[])==before.get('ignored_checks',[]))

def groups(board,report):
    segments=children(board,'segment'); nets={int(n[1]):atom(n[2]) for n in children(board,'net')}
    result={}
    for issue in report.get('findings',[]):
        if issue.get('type') not in TARGETS:continue
        for item in issue.get('items',[]):
            desc=item.get('description','')
            if not desc.startswith('Track ['):continue
            pos=item['pos']; point=(pos['x'],pos['y'])
            matches=[i for i,s in enumerate(segments)
                     if math.dist(xy(s,'start'),point)<.0001
                     and f"Track [{nets[int(child(s,'net')[1])]}]" in desc
                     and f"on {atom(child(s,'layer')[1])}," in desc]
            if len(matches)==1:
                s=segments[matches[0]]
                key=(issue['type'],child(s,'net')[1],atom(child(s,'layer')[1]))
                result.setdefault(key,set()).add(matches[0])
    return list(result.values())

def move_vertices(board,indices,dx,dy):
    segments=children(board,'segment'); selected=[segments[i] for i in indices]
    net=child(selected[0],'net')[1];layer=child(selected[0],'layer')[1]
    layer_id=0 if atom(layer) in ('Front','F.Cu') else 1
    anchors=[p['geom'] for p in pads(board) if layer_id in p['layers']]
    anchors += [Point(xy(v,'at')).buffer(float(child(v,'size')[1])/2) for v in children(board,'via')]
    vertices={xy(s,k) for s in selected for k in ('start','end')}
    vertices={v for v in vertices if not any(g.buffer(.00001).covers(Point(v)) for g in anchors)}
    if not vertices:return None,[]
    candidate=deepcopy(board); changed=[]
    for i,s in enumerate(children(candidate,'segment')):
        if child(s,'net')[1]!=net or child(s,'layer')[1]!=layer:continue
        moved=False
        for key in ('start','end'):
            x,y=xy(s,key)
            if (x,y) in vertices:
                child(s,key)[1:3]=[f'{x+dx:.6f}',f'{y+dy:.6f}'];moved=True
        if moved:changed.append(i)
    return candidate,changed

def quick_clear(board,changed):
    """Reject new copper collisions cheaply before invoking the full checkers."""
    items=copper(board); changed_labels={f'trace{i}' for i in changed}
    for p in items:
        if p['label'] not in changed_labels:continue
        if p['geom'].area<1e-9:return False
        for q in items:
            if p['net']==q['net'] or not set(p['layers'])&set(q['layers']):continue
            if p['geom'].distance(q['geom'])<.198:return False
    return True

def dogleg(board,indices,dx,dy):
    """Offset the interior of one segment, retaining both connection points."""
    if len(indices)!=1:return None,[]
    index=next(iter(indices));s=children(board,'segment')[index]
    a=xy(s,'start');b=xy(s,'end');length=math.dist(a,b)
    offset=math.hypot(dx,dy)
    if length<4*offset or length<.5:return None,[]
    ux,uy=(b[0]-a[0])/length,(b[1]-a[1])/length
    # Only lateral movements, preserving sensible end approaches.
    if abs(ux*dx+uy*dy)>1e-6:return None,[]
    trim=max(offset,.15)
    points=[a,(a[0]+ux*trim+dx,a[1]+uy*trim+dy),
            (b[0]-ux*trim+dx,b[1]-uy*trim+dy),b]
    holes=[p for p in pads(board) if p['kind']=='np_thru_hole']
    if holes:
        line=LineString([a,b]);hole=min(holes,key=lambda p:line.distance(Point(p['x'],p['y'])))
        center=line.project(Point(hole['x'],hole['y']))
        span=max(hole['width'],hole['height'])/2+.4
        lo,hi=center-span,center+span
        if lo>trim and hi<length-trim:
            def at(t,shift=False):return (a[0]+ux*t+(dx if shift else 0),a[1]+uy*t+(dy if shift else 0))
            points=[a,at(lo-trim),at(lo,True),at(hi,True),at(hi+trim),b]
    candidate=deepcopy(board);old=children(candidate,'segment')[index]
    insertion=next(i for i,n in enumerate(candidate) if n is old)
    pieces=[]
    for start,end in zip(points,points[1:]):
        part=deepcopy(s)
        # Legacy imported tracks have no UUID; drop identifiers if supplied.
        part[:]=[n for n in part if not isinstance(n,list) or n[0] not in ('uuid','tstamp')]
        for key,pos in (('start',start),('end',end)):
            child(part,key)[1:3]=[f'{v:.6f}' for v in pos]
        pieces.append(part)
    candidate[insertion:insertion+1]=pieces
    general=child(candidate,'general',[]);tracks=child(general,'tracks')
    if tracks:tracks[1]=str(len(children(candidate,'segment'))+len(children(candidate,'via')))
    return candidate,list(range(index,index+len(pieces)))

def repair(source,output,cli=None,initial=None,max_candidates=160,seconds=180):
    source=Path(source).resolve();output=Path(output).resolve()
    output.mkdir(parents=True,exist_ok=False)
    executable=find_cli(cli); baseline=initial or run_check(executable,'drc',source,output)
    result=dict(source=str(source),status='not_needed',initial_targets=target_count(baseline),
                remaining_targets=target_count(baseline),accepted=[],attempts=0,board=str(source))
    def finish():
        (output/'repair.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
        return result
    if not executable or baseline.get('status') not in ('passed','review','failed'):
        result.update(status='unavailable',reason='A working KiCad DRC is required.');return finish()
    if not target_count(baseline):return finish()
    with contextlib.redirect_stdout(io.StringIO()): audit=validate(source)
    if any(audit[k] for k in ('missing_links','clearance_conflicts','physical_short_pairs','outside_copper')):
        result.update(status='skipped',reason='Input must pass copper connectivity/clearance audit first.');return finish()
    board=load(source);current=baseline;deadline=time.monotonic()+seconds
    # Millimetres. Smallest successful displacement wins; bounded to 0.4 mm.
    offsets=[(d*x,d*y) for d in (.05,.1,.2,.3,.4)
             for x,y in ((1,0),(-1,0),(0,1),(0,-1),(1,1),(1,-1),(-1,1),(-1,-1))]
    while target_count(current) and result['attempts']<max_candidates and time.monotonic()<deadline:
        committed=False
        for group in groups(board,current):
            options=[(method,dx,dy) for method in (dogleg,move_vertices) for dx,dy in offsets]
            for method,dx,dy in options:
                if result['attempts']>=max_candidates or time.monotonic()>=deadline:break
                result['attempts']+=1
                candidate,changed=method(board,group,dx,dy)
                if candidate is None or not quick_clear(candidate,changed):continue
                attempt=output/f"attempt-{result['attempts']:03d}";attempt.mkdir()
                path=attempt/'candidate.kicad_pcb';path.write_text(dump(candidate),encoding='utf-8')
                with contextlib.redirect_stdout(io.StringIO()): audit=validate(path,attempt/'copper.json')
                if any(audit[k] for k in ('missing_links','clearance_conflicts','physical_short_pairs','outside_copper')):continue
                remaining=deadline-time.monotonic()
                if remaining<=0:break
                checked=run_check(executable,'drc',path,attempt,timeout=max(1,min(120,remaining)))
                if not acceptable(current,checked):continue
                board=candidate;current=checked;committed=True
                result['accepted'].append(dict(method=method.__name__,dx=dx,dy=dy,changed_segments=changed,
                                               remaining_targets=target_count(current),evidence=str(attempt)))
                result['board']=str(path)
                break
            if committed:break
        if not committed:break
    result['remaining_targets']=target_count(current)
    result['status']='repaired' if not target_count(current) else 'partial' if result['accepted'] else 'unresolved'
    if result['accepted']:
        final=output/'repaired.kicad_pcb';final.write_text(dump(board),encoding='utf-8');result['board']=str(final)
    result['final_drc']=current
    return finish()

if __name__=='__main__':
    ap=argparse.ArgumentParser(description=__doc__);ap.add_argument('source');ap.add_argument('output')
    ap.add_argument('--kicad-cli');ap.add_argument('--seconds',type=int,default=180)
    a=ap.parse_args();r=repair(a.source,a.output,a.kicad_cli,seconds=a.seconds)
    print(json.dumps({k:v for k,v in r.items() if k!='final_drc'},indent=2))
    raise SystemExit(0 if r['status'] in ('repaired','not_needed') else 1)
