#!/usr/bin/env python3
"""Exact analytic contact graph for explicit KiCad copper evidence, CP414+.
No grid coincidence is used to establish historical connectivity.
"""
import math,hashlib,json
from physical_router import COPPER

EPS=1e-9
def stable(x): return hashlib.sha256(json.dumps(x,sort_keys=True,separators=(',',':')).encode()).hexdigest()
def d2(a,b): return (a[0]-b[0])**2+(a[1]-b[1])**2
def point_seg_d2(p,a,b):
    vx=b[0]-a[0];vy=b[1]-a[1];wx=p[0]-a[0];wy=p[1]-a[1];vv=vx*vx+vy*vy
    if vv<=EPS:return d2(p,a)
    t=max(0.0,min(1.0,(wx*vx+wy*vy)/vv));q=(a[0]+t*vx,a[1]+t*vy);return d2(p,q)
def orient(a,b,c): return (b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])
def onseg(a,b,p): return min(a[0],b[0])-EPS<=p[0]<=max(a[0],b[0])+EPS and min(a[1],b[1])-EPS<=p[1]<=max(a[1],b[1])+EPS and abs(orient(a,b,p))<=EPS
def seg_intersect(a,b,c,d):
    o1,o2,o3,o4=orient(a,b,c),orient(a,b,d),orient(c,d,a),orient(c,d,b)
    if ((o1>EPS and o2<-EPS) or (o1<-EPS and o2>EPS)) and ((o3>EPS and o4<-EPS) or (o3<-EPS and o4>EPS)):return True
    return any((abs(o)<=EPS and onseg(x,y,p)) for o,x,y,p in ((o1,a,b,c),(o2,a,b,d),(o3,c,d,a),(o4,c,d,b)))
def seg_seg_d2(a,b,c,d):
    if seg_intersect(a,b,c,d):return 0.0
    return min(point_seg_d2(a,c,d),point_seg_d2(b,c,d),point_seg_d2(c,a,b),point_seg_d2(d,a,b))
def point_in_poly(p,poly):
    x,y=p;inside=False;j=len(poly)-1
    for i,(xi,yi) in enumerate(poly):
        xj,yj=poly[j]
        if onseg((xi,yi),(xj,yj),p):return True
        if ((yi>y)!=(yj>y)) and x < (xj-xi)*(y-yi)/(yj-yi)+xi:inside=not inside
        j=i
    return inside
def seg_poly_contact(a,b,poly,r=0.0):
    if point_in_poly(a,poly) or point_in_poly(b,poly):return True
    return any(seg_seg_d2(a,b,poly[i-1],poly[i]) <= r*r+EPS for i in range(len(poly)))
def circle_poly_contact(c,r,poly):
    if point_in_poly(c,poly):return True
    return any(point_seg_d2(c,poly[i-1],poly[i]) <= r*r+EPS for i in range(len(poly)))

def to_pad_local(p,q):
    c=p['xy'];a=math.radians(-(p.get('rotation') or 0.0));dx=q[0]-c[0];dy=q[1]-c[1]
    return (dx*math.cos(a)-dy*math.sin(a),dx*math.sin(a)+dy*math.cos(a))
def local_seg(p,a,b): return to_pad_local(p,a),to_pad_local(p,b)

def pad_half(p):
    s=p.get('size_mm');return (s[0]/2,s[1]/2) if s else None
def pad_shape_supported(p): return p.get('pad_shape') in ('rect','circle','oval','roundrect') and (p.get('pad_shape')!='roundrect' or p.get('roundrect_rratio') is not None)
def pad_point_d2(p,q):
    h=pad_half(p);c=(0.0,0.0);q=to_pad_local(p,q);shape=p.get('pad_shape')
    if not h:return float('inf')
    if shape=='circle':
        r=min(h);return max(math.hypot(q[0],q[1])-r,0.0)**2
    if shape=='roundrect':
        rr=min(h)*2*p['roundrect_rratio']; ix=max(h[0]-rr,0);iy=max(h[1]-rr,0)
        dx=max(abs(q[0])-ix,0);dy=max(abs(q[1])-iy,0)
        return max(math.hypot(dx,dy)-rr,0.0)**2
    if shape=='oval':
        if h[0]>=h[1]: a=(-(h[0]-h[1]),0);bb=((h[0]-h[1]),0);r=h[1]
        else:a=(0,-(h[1]-h[0]));bb=(0,(h[1]-h[0]));r=h[0]
        return max(math.sqrt(point_seg_d2(q,a,bb))-r,0.0)**2
    return point_rect_d2(q,c,h)

def point_rect_d2(q,c,h):
    dx=max(abs(q[0]-c[0])-h[0],0);dy=max(abs(q[1]-c[1])-h[1],0);return dx*dx+dy*dy
def seg_rect_contact(a,b,c,h,r=0.0):
    # inflated axis-aligned rectangle; conservative only by the explicit radius requested
    H=(h[0]+r,h[1]+r); x0,y0=c[0]-H[0],c[1]-H[1];x1,y1=c[0]+H[0],c[1]+H[1]
    if x0-EPS<=a[0]<=x1+EPS and y0-EPS<=a[1]<=y1+EPS:return True
    if x0-EPS<=b[0]<=x1+EPS and y0-EPS<=b[1]<=y1+EPS:return True
    e=[((x0,y0),(x1,y0)),((x1,y0),(x1,y1)),((x1,y1),(x0,y1)),((x0,y1),(x0,y0))]
    return any(seg_intersect(a,b,u,v) for u,v in e)
def pad_layer_set(p): return set(p.get('copper_layers') or ())
def pad_pad_contact(a,b):
    common=pad_layer_set(a)&pad_layer_set(b)
    if not common:return False
    # exact supported-shape overlap via center distance tests for circle, otherwise use mutual boundary/center distance conservative analytic tests
    ha,hb=pad_half(a),pad_half(b)
    if not ha or not hb or not pad_shape_supported(a) or not pad_shape_supported(b):return False
    if a.get('pad_shape')=='circle' and b.get('pad_shape')=='circle':
        return d2(a['xy'],b['xy']) <= (min(ha)+min(hb))**2+EPS
    # AABB rejection then center-in-shape checks; overlapping exotic boundaries not asserted without proof
    if abs(a['xy'][0]-b['xy'][0])>ha[0]+hb[0]+EPS or abs(a['xy'][1]-b['xy'][1])>ha[1]+hb[1]+EPS:return False
    return pad_point_d2(a,b['xy'])<=EPS or pad_point_d2(b,a['xy'])<=EPS

def pad_seg_contact(p,s):
    h=pad_half(p)
    if not h or not pad_shape_supported(p) or s['layer'] not in pad_layer_set(p):return False
    shape=p.get('pad_shape');a,bb=local_seg(p,s['start'],s['end']);tr=s['width_mm']/2
    if shape=='circle': return point_seg_d2((0,0),a,bb) <= (min(h)+tr)**2+EPS
    if shape=='oval':
        if h[0]>=h[1]:u=(-(h[0]-h[1]),0);v=((h[0]-h[1]),0);r=h[1]
        else:u=(0,-(h[1]-h[0]));v=(0,(h[1]-h[0]));r=h[0]
        return seg_seg_d2(u,v,a,bb) <= (r+tr)**2+EPS
    if shape=='roundrect':
        rr=min(h)*2*p['roundrect_rratio'];ix=max(h[0]-rr,0);iy=max(h[1]-rr,0)
        if seg_rect_contact(a,bb,(0,0),(h[0]-rr,h[1]),tr) or seg_rect_contact(a,bb,(0,0),(h[0],h[1]-rr),tr):return True
        return any(point_seg_d2((sx*ix,sy*iy),a,bb) <= (rr+tr)**2+EPS for sx in (-1,1) for sy in (-1,1))
    return seg_rect_contact(a,bb,(0,0),h,tr)

def pad_via_contact(p,v):
    h=pad_half(p)
    return bool(h and pad_layer_set(p) and pad_point_d2(p,v['at']) <= (v['size_mm']/2)**2+EPS)
def zone_regions(z): return z.get('filled_polygons_mm') or []
def pad_zone_contact(p,z):
    h=pad_half(p)
    if not h or z['layer'] not in pad_layer_set(p):return False
    regions=zone_regions(z)
    if not regions:return False
    c=p['xy'];corners=[(c[0]-h[0],c[1]-h[1]),(c[0]+h[0],c[1]-h[1]),(c[0]+h[0],c[1]+h[1]),(c[0]-h[0],c[1]+h[1])]
    for poly in regions:
        if point_in_poly(c,poly) or any(point_in_poly(q,poly) for q in corners):return True
        if any(seg_intersect(corners[i-1],corners[i],poly[j-1],poly[j]) for i in range(4) for j in range(len(poly))):return True
    return False
def seg_seg_contact(a,b): return a['layer']==b['layer'] and seg_seg_d2(a['start'],a['end'],b['start'],b['end']) <= ((a['width_mm']+b['width_mm'])/2)**2+EPS
def seg_via_contact(s,v): return point_seg_d2(v['at'],s['start'],s['end']) <= ((s['width_mm']+v['size_mm'])/2)**2+EPS
def seg_zone_contact(s,z): return s['layer']==z['layer'] and any(seg_poly_contact(s['start'],s['end'],poly,s['width_mm']/2) for poly in zone_regions(z))
def via_via_contact(a,b): return d2(a['at'],b['at']) <= ((a['size_mm']+b['size_mm'])/2)**2+EPS
def via_zone_contact(v,z): return any(circle_poly_contact(v['at'],v['size_mm']/2,poly) for poly in zone_regions(z))
def poly_poly_contact(pa,pb):
    return point_in_poly(pa[0],pb) or point_in_poly(pb[0],pa) or any(seg_intersect(pa[i-1],pa[i],pb[j-1],pb[j]) for i in range(len(pa)) for j in range(len(pb)))
def zone_zone_contact(a,b):
    if a['layer']!=b['layer']:return False
    return any(poly_poly_contact(pa,pb) for pa in zone_regions(a) for pb in zone_regions(b))

def build_net_graph(net,evidence,pads):
    nodes=[]; gaps=[]
    for i,p in enumerate(pads):
        if p.get('net')==net:
            if not p.get('xy') or not p.get('size_mm') or not p.get('copper_layers'):gaps.append({'code':'TITS_UP_PAD_GEOMETRY','ref':p.get('ref'),'pad':p.get('pad')})
            elif not pad_shape_supported(p):gaps.append({'code':'TITS_UP_PAD_SHAPE_UNSUPPORTED','ref':p.get('ref'),'pad':p.get('pad'),'shape':p.get('pad_shape')})
            nodes.append(('pad',i,p))
    for kind,key in (('segment','segments'),('via','vias'),('zone','zones')):
        for i,x in enumerate(evidence.get(key,())):
            if x.get('net')!=net:continue
            if kind=='zone':
                fills=x.get('filled_polygons_mm') or []
                if not fills:gaps.append({'code':'TITS_UP_ZONE_FILL_NOT_EXPLICIT','net':net,'layer':x.get('layer')})
                for fi,poly in enumerate(fills):
                    nodes.append(('zone_fill',(i,fi),{**x,'filled_polygons_mm':[poly],'zone_index':i,'fill_index':fi}))
            else:nodes.append((kind,i,x))
    adj=[set() for _ in nodes]; edges=[]
    def contact(A,B):
        ka,_,a=A;kb,_,b=B
        ka='zone' if ka=='zone_fill' else ka;kb='zone' if kb=='zone_fill' else kb
        if ka=='pad' and kb=='pad':return pad_pad_contact(a,b)
        if ka=='pad' and kb=='segment':return pad_seg_contact(a,b)
        if ka=='pad' and kb=='via':return pad_via_contact(a,b)
        if ka=='pad' and kb=='zone':return pad_zone_contact(a,b)
        if ka=='segment' and kb=='segment':return seg_seg_contact(a,b)
        if ka=='segment' and kb=='via':return seg_via_contact(a,b)
        if ka=='segment' and kb=='zone':return seg_zone_contact(a,b)
        if ka=='via' and kb=='via':return via_via_contact(a,b)
        if ka=='via' and kb=='zone':return via_zone_contact(a,b)
        if ka=='zone' and kb=='zone':return zone_zone_contact(a,b)
        reverse={('segment','pad'),('via','pad'),('zone','pad'),('via','segment'),('zone','segment'),('zone','via')}
        if (ka,kb) in reverse:return contact(B,A)
        return False
    for i in range(len(nodes)):
        for j in range(i+1,len(nodes)):
            if contact(nodes[i],nodes[j]):adj[i].add(j);adj[j].add(i);edges.append((i,j))
    comps=[];un=set(range(len(nodes)))
    while un:
        q=[min(un)];un.remove(q[0]);c=set(q)
        while q:
            u=q.pop(0)
            for v in sorted(adj[u]):
                if v in un:un.remove(v);c.add(v);q.append(v)
        comps.append(sorted(c))
    comps.sort(key=lambda c:(-len(c),c))
    idx={u:i for i,c in enumerate(comps) for u in c}
    contacts=[];pad_comp=[]
    for ni,(k,_,p) in enumerate(nodes):
        if k=='pad':
            degree=len(adj[ni]);rec={'ref':p.get('ref'),'pad':p.get('pad'),'component':idx[ni],'degree':degree,'touches_explicit_copper':degree>0}
            contacts.append(rec);pad_comp.append(idx[ni])
            if degree==0:gaps.append({'code':'TITS_UP_PAD_NOT_CONNECTED_TO_EXPLICIT_COPPER','ref':p.get('ref'),'pad':p.get('pad')})
    connected=bool(contacts) and all(r['touches_explicit_copper'] for r in contacts) and len(set(pad_comp))==1
    serial={'net':net,'node_count':len(nodes),'edge_count':len(edges),'component_count':len(comps),'pad_count':len(contacts),
            'pads_touching_copper':sum(r['touches_explicit_copper'] for r in contacts),'historical_pad_connectivity':'CONNECTED' if connected else 'UNRESOLVED',
            'contacts':contacts,'evidence_gaps':gaps}
    return {**serial,'digest':stable(serial)}
