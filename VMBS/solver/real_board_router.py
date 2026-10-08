#!/usr/bin/env python3
"""Deterministic real-board routing evidence adapter. No AI/ML; unknowns remain explicit."""
import math,re,heapq,hashlib,json
from pathlib import Path

def stable(o): return hashlib.sha256(json.dumps(o,sort_keys=True,separators=(',',':')).encode()).hexdigest()
def sexpr_tokens(s): return re.findall(r'\(|\)|"(?:\\.|[^"\\])*"|[^\s()]+',s)
def parse(tokens):
 i=0
 def one():
  nonlocal i
  if tokens[i]!='(': v=tokens[i].strip('"');i+=1;return v
  i+=1;a=[]
  while i<len(tokens) and tokens[i]!=')':a.append(one())
  if i>=len(tokens): raise ValueError('unclosed s-expression')
  i+=1;return a
 out=[]
 while i<len(tokens):out.append(one())
 return out

def child(n,k):
 for x in n[1:]:
  if isinstance(x,list) and x and x[0]==k:return x
 return None
def children(n,k):return [x for x in n[1:] if isinstance(x,list) and x and x[0]==k]
def f(x):return float(x)
def transform(px,py,ox,oy,deg):
 r=math.radians(-deg);return (ox+px*math.cos(r)-py*math.sin(r),oy+px*math.sin(r)+py*math.cos(r))
def extract(path):
 roots=parse(sexpr_tokens(Path(path).read_text(errors='replace'))); root=roots[0]
 nets={int(x[1]):x[2] for x in children(root,'net') if len(x)>=3}; pads=[]
 for m in children(root,'module')+children(root,'footprint'):
  at=child(m,'at'); ox,oy=f(at[1]),f(at[2]); rot=f(at[3]) if len(at)>3 else 0.0
  fp=child(m,'fp_text'); ref=fp[2] if fp and len(fp)>2 and fp[1]=='reference' else '?'
  for p in children(m,'pad'):
   pa=child(p,'at'); nn=child(p,'net')
   if not pa or not nn:continue
   px,py=f(pa[1]),f(pa[2]); pr=f(pa[3]) if len(pa)>3 else 0.0
   x,y=transform(px,py,ox,oy,rot);pads.append({'ref':ref,'pad':p[1],'net_id':int(nn[1]),'net':nn[2] if len(nn)>2 else nets.get(int(nn[1]),''),'xy':[round(x,6),round(y,6)],'rotation':pr%360})
 return {'nets':nets,'pads':pads}
def endpoints(ex):
 d={}
 for p in ex['pads']:
  if p['net_id']!=0:d.setdefault(p['net'],[]).append(p)
 for k in d:d[k].sort(key=lambda p:(p['ref'],str(p['pad']),p['xy']))
 return dict(sorted(d.items()))
def snap(pt,step):return (round(pt[0]/step),round(pt[1]/step))
def raster_rect(poly,step):
 xs=[p[0] for p in poly];ys=[p[1] for p in poly];return {(x,y) for x in range(math.floor(min(xs)/step),math.ceil(max(xs)/step)+1) for y in range(math.floor(min(ys)/step),math.ceil(max(ys)/step)+1)}
def occupancy(locked,step,exclude_refs=()):
 o=set(); ex=set(exclude_refs)
 for x in locked:
  if x['ref'] not in ex:o |= raster_rect(x['courtyard_corners_mm'],step)
 return o
def astar(a,b,blocked,bounds,usage=None):
 usage=usage or {}; minx,miny,maxx,maxy=bounds;q=[(abs(a[0]-b[0])+abs(a[1]-b[1]),0,a)];prev={a:None};cost={a:0}
 while q:
  _,g,u=heapq.heappop(q)
  if u==b:break
  if g!=cost[u]:continue
  for v in ((u[0]-1,u[1]),(u[0],u[1]-1),(u[0],u[1]+1),(u[0]+1,u[1])):
   if not(minx<=v[0]<=maxx and miny<=v[1]<=maxy) or (v in blocked and v not in (a,b)):continue
   ng=g+1+usage.get(v,0)*8
   if ng<cost.get(v,10**18):cost[v]=ng;prev[v]=u;heapq.heappush(q,(ng+abs(v[0]-b[0])+abs(v[1]-b[1]),ng,v))
 if b not in prev:return None
 p=[];u=b
 while u is not None:p.append(u);u=prev[u]
 return p[::-1]
def route_net(points,blocked,bounds,usage):
 if len(points)<2:return []
 tree={points[0]};paths=[]
 for target in points[1:]:
  candidates=[]
  for src in sorted(tree):
   p=astar(src,target,blocked,bounds,usage)
   if p:candidates.append((len(p),p))
  if not candidates:return None
  p=min(candidates,key=lambda z:(z[0],z[1]))[1];paths.append(p);tree.update(p)
 return paths
def main(pcb,geom,out,step=.635):
 ex=extract(pcb);eps=endpoints(ex);g=json.load(open(geom));locked=g['locked_poses'];poly=g['outline']['polygon_mm'];xs=[x for x,y in poly];ys=[y for x,y in poly]
 bounds=(math.floor(min(xs)/step),math.floor(min(ys)/step),math.ceil(max(xs)/step),math.ceil(max(ys)/step));usage={};routes={};fails=[]
 # deterministic: clocks/control first via existing explicit order policy, then name
 from route_order import order
 names=order([n for n,e in eps.items() if len(e)>=2])
 for n in names:
  ps=eps[n];refs={p['ref'] for p in ps};blocked=occupancy(locked,step,refs);pts=[]
  for p in ps:
   q=snap(p['xy'],step)
   if q not in pts:pts.append(q)
  rr=route_net(pts,blocked,bounds,usage)
  if rr is None:fails.append({'net':n,'code':'TITS_UP_ROUTE_NO_PATH','endpoint_count':len(ps)});continue
  routes[n]=rr
  for path in rr:
   for c in path:usage[c]=usage.get(c,0)+1
 over=sorted([list(c)+[v] for c,v in usage.items() if v>1])
 result={'format':'VMBS-REAL-ROUTER-CP288','grid_mm':step,'pad_count':len(ex['pads']),'routable_net_count':len(names),'routed_net_count':len(routes),'failures':fails,'congestion_cells':over,'max_usage':max(usage.values(),default=0),'route_digest':stable(routes),'endpoint_digest':stable(eps),'routes':routes}
 Path(out).write_text(json.dumps(result,indent=2)+'\n');return result
if __name__=='__main__':
 import argparse;a=argparse.ArgumentParser();a.add_argument('pcb');a.add_argument('geom');a.add_argument('out');a.add_argument('--step',type=float,default=.635);z=a.parse_args();r=main(z.pcb,z.geom,z.out,z.step);print(json.dumps({k:r[k] for k in ('pad_count','routable_net_count','routed_net_count','max_usage','route_digest')},indent=2))
