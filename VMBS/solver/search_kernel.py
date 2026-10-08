from dataclasses import dataclass
from heapq import heappush,heappop
from itertools import count
@dataclass(frozen=True,order=True)
class Pose: x:int; y:int; rot:int=0
@dataclass
class PlacementProblem:
 board_w:int; board_h:int; domains:dict; sizes:dict; locked:dict; adjacency:list; keepouts:list
def dims(s,r): return (s[1],s[0]) if r%180 else s
def rect(ref,p,q):
 w,h=dims(q.sizes[ref],p.rot); return (p.x,p.y,p.x+w,p.y+h)
def overlap(a,b): return a[0]<b[2] and b[0]<a[2] and a[1]<b[3] and b[1]<a[3]
def legal(ref,p,placed,q):
 r=rect(ref,p,q)
 if r[0]<0 or r[1]<0 or r[2]>q.board_w or r[3]>q.board_h:return False,"OUT_OF_BOUNDS"
 if any(overlap(r,k) for k in q.keepouts):return False,"KEEPOUT"
 if any(overlap(r,rect(o,op,q)) for o,op in placed.items()):return False,"OVERLAP"
 return True,None
def adjok(ref,p,placed,q):
 for a,b,lim in q.adjacency:
  if ref in (a,b):
   o=b if ref==a else a
   if o in placed and abs(p.x-placed[o].x)+abs(p.y-placed[o].y)>lim:return False
 return True
def score(pl,q):
 ad=sum(abs(pl[a].x-pl[b].x)+abs(pl[a].y-pl[b].y) for a,b,_ in q.adjacency if a in pl and b in pl)
 rs=[rect(r,p,q) for r,p in pl.items()]
 area=0 if not rs else (max(x[2] for x in rs)-min(x[0] for x in rs))*(max(x[3] for x in rs)-min(x[1] for x in rs))
 return (ad,area)
def solve_placement(q):
 pl=dict(q.locked); trace=[]; best=None; bs=None
 for r,p in list(pl.items()):
  ok,why=legal(r,p,{k:v for k,v in pl.items() if k!=r},q)
  if not ok:return {"result":"UNSAT","reason":"LOCKED_"+why,"trace":trace}
 refs=[r for r in q.domains if r not in pl]
 def choose(un):
  z=[]
  for r in un:
   vals=[p for p in sorted(set(q.domains[r])) if legal(r,p,pl,q)[0] and adjok(r,p,pl,q)]
   z.append((len(vals),r,vals))
  return min(z,key=lambda x:(x[0],x[1]))
 def dfs(un):
  nonlocal best,bs
  if not un:
   s=score(pl,q)
   if bs is None or s<bs:bs=s;best=dict(pl);trace.append(("BEST",s))
   return
  _,r,vals=choose(un)
  if not vals:trace.append(("BACKTRACK_NO_DOMAIN",r));return
  for p in vals:
   pl[r]=p;trace.append(("PLACE",r,p.x,p.y,p.rot));dfs([x for x in un if x!=r]);del pl[r];trace.append(("BACKTRACK",r))
 dfs(refs); return {"result":"SOLVED_VERIFIED" if best else "UNSAT","placement":best,"score":bs,"trace":trace}
def astar_route(w,h,start,goal,blocked=frozenset(),via_cost=8,layers=2):
 seq=count(); s=(start[0],start[1],0); goals={(goal[0],goal[1],l) for l in range(layers)}
 pq=[(0,0,next(seq),s)]; g={s:0}; prev={}; H=lambda a:abs(a[0]-goal[0])+abs(a[1]-goal[1])
 while pq:
  _,cost,_,s=heappop(pq)
  if cost!=g.get(s):continue
  if s in goals:
   path=[s]
   while s in prev:s=prev[s];path.append(s)
   return {"result":"ROUTED","cost":cost,"path":path[::-1]}
  x,y,l=s; moves=[(x-1,y,l,1),(x+1,y,l,1),(x,y-1,l,1),(x,y+1,l,1)]
  if layers>1:moves += [(x,y,nl,via_cost) for nl in range(layers) if nl!=l]
  for nx,ny,nl,c in moves:
   ns=(nx,ny,nl)
   if nx<0 or ny<0 or nx>=w or ny>=h or ns in blocked:continue
   ng=cost+c
   if ng<g.get(ns,10**18):g[ns]=ng;prev[ns]=s;heappush(pq,(ng+H(ns),ng,next(seq),ns))
 return {"result":"UNSAT","reason":"NO_ROUTE"}
