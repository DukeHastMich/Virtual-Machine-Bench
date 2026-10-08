#!/usr/bin/env python3
"""Deterministic extraction/projection of existing KiCad copper for VMBS CP339+.
Only explicit board evidence is consumed. Unsupported/missing geometry is surfaced as TITS_UP.
"""
from pathlib import Path
import math
from physical_router import sexpr_tokens,parse,children,child,COPPER,mm_to_cell,in_bounds

def disk_offsets(radius):
    r=max(0,int(radius)); return tuple((x,y) for x in range(-r,r+1) for y in range(-r,r+1) if x*x+y*y<=r*r)

def _pt(n,key):
    q=child(n,key)
    return (float(q[1]),float(q[2])) if q and len(q)>=3 else None

def _num(n,key):
    q=child(n,key)
    try:return float(q[1]) if q and len(q)>=2 else None
    except:return None

def _layer(n):
    q=child(n,'layer')
    if not q or len(q)<2:return None
    x=q[1].strip('"');return {'Front':'F.Cu','Back':'B.Cu'}.get(x,x)

def _net(n):
    q=child(n,'net');return int(q[1]) if q and len(q)>=2 else None

def extract_existing_copper(path):
    root=parse(sexpr_tokens(Path(path).read_text(errors='replace')))[0]
    nets={int(n[1]):n[2] for n in children(root,'net') if len(n)>=3}
    seg=[];vias=[];zones=[];gaps=[]
    for s in children(root,'segment'):
        a,b=_pt(s,'start'),_pt(s,'end');w=_num(s,'width');ly=_layer(s);ni=_net(s)
        if a and b and w is not None and ly in COPPER and ni is not None:seg.append({'start':a,'end':b,'width_mm':w,'layer':ly,'net_id':ni,'net':nets.get(ni)})
        else:gaps.append({'kind':'segment','code':'TITS_UP_EXISTING_SEGMENT_GEOMETRY'})
    for v in children(root,'via'):
        at=_pt(v,'at');sz=_num(v,'size');dr=_num(v,'drill');ni=_net(v)
        if at and sz is not None and ni is not None:vias.append({'at':at,'size_mm':sz,'drill_mm':dr,'net_id':ni,'net':nets.get(ni)})
        else:gaps.append({'kind':'via','code':'TITS_UP_EXISTING_VIA_GEOMETRY'})
    for z in children(root,'zone'):
        ni=_net(z);ly=_layer(z);poly=child(z,'polygon');pts=[]
        if poly:
            ps=child(poly,'pts')
            if ps:
                for xy in children(ps,'xy'):
                    if len(xy)>=3:pts.append((float(xy[1]),float(xy[2])))
        fills=[]
        for fp in children(z,'filled_polygon'):
            fps=child(fp,'pts'); fpts=[]
            if fps:
                for xy in children(fps,'xy'):
                    if len(xy)>=3:fpts.append((float(xy[1]),float(xy[2])))
            if len(fpts)>=3:fills.append(fpts)
            else:gaps.append({'kind':'zone_fill','code':'TITS_UP_EXISTING_ZONE_FILL_GEOMETRY','net_id':ni,'layer':ly})
        fill=child(z,'fill'); cp=child(z,'connect_pads')
        def subnum(n,key):
            q=child(n,key) if n else None
            try:return float(q[1]) if q and len(q)>=2 else None
            except:return None
        zr={'net_id':ni,'net':nets.get(ni),'layer':ly,'polygon_mm':pts,'filled_polygons_mm':fills,
            'fill_present':bool(fill),'thermal_gap_mm':subnum(fill,'thermal_gap'),'thermal_bridge_width_mm':subnum(fill,'thermal_bridge_width'),
            'connect_pad_clearance_mm':subnum(cp,'clearance')}
        if ni is not None and ly in COPPER and len(pts)>=3:zones.append(zr)
        else:gaps.append({'kind':'zone','code':'TITS_UP_EXISTING_ZONE_GEOMETRY'})
    return {'segments':seg,'vias':vias,'zones':zones,'gaps':gaps,'nets':nets}

def _line_cells(a,b,step):
    x0,y0=mm_to_cell(a[0],step),mm_to_cell(a[1],step);x1,y1=mm_to_cell(b[0],step),mm_to_cell(b[1],step)
    dx=abs(x1-x0);dy=-abs(y1-y0);sx=1 if x0<x1 else -1;sy=1 if y0<y1 else -1;err=dx+dy;out=[]
    while True:
        out.append((x0,y0))
        if (x0,y0)==(x1,y1):break
        e=2*err
        if e>=dy:err+=dy;x0+=sx
        if e<=dx:err+=dx;y0+=sy
    return tuple(out)

def _point_in_poly(x,y,p):
    inside=False;j=len(p)-1
    for i in range(len(p)):
        xi,yi=p[i];xj,yj=p[j]
        if ((yi>y)!=(yj>y)) and x < (xj-xi)*(y-yi)/(yj-yi)+xi:inside=not inside
        j=i
    return inside

def existing_obstacles(ev,current_net,step,clearance,bounds):
    out=set()
    for s in ev['segments']:
        if s['net']==current_net:continue
        r=max(0,int(math.ceil((s['width_mm']/2+clearance)/step)));li=COPPER.index(s['layer'])
        for x,y in _line_cells(s['start'],s['end'],step):
            for dx,dy in disk_offsets(r):
                q=(x+dx,y+dy,li)
                if in_bounds(q,bounds):out.add(q)
    for v in ev['vias']:
        if v['net']==current_net:continue
        r=max(0,int(math.ceil((v['size_mm']/2+clearance)/step)));cx,cy=mm_to_cell(v['at'][0],step),mm_to_cell(v['at'][1],step)
        for dx,dy in disk_offsets(r):
            for li in (0,1):
                q=(cx+dx,cy+dy,li)
                if in_bounds(q,bounds):out.add(q)
    for z in ev['zones']:
        if z['net']==current_net:continue
        li=COPPER.index(z['layer']);xs=[p[0] for p in z['polygon_mm']];ys=[p[1] for p in z['polygon_mm']]
        # Zone fill is not recoverable from polygon alone at trace-clearance fidelity; block explicit polygon interior only.
        for x in range(math.floor(min(xs)/step),math.ceil(max(xs)/step)+1):
            for y in range(math.floor(min(ys)/step),math.ceil(max(ys)/step)+1):
                if _point_in_poly(x*step,y*step,z['polygon_mm']) and in_bounds((x,y,li),bounds):out.add((x,y,li))
    return out
