#!/usr/bin/env python3
import re,json,math,argparse
from pathlib import Path
TOL=0.02
def P(x,y): return (float(x),float(y))
def near(a,b): return math.hypot(a[0]-b[0],a[1]-b[1]) <= TOL
def primitives(txt):
 out=[]
 for ln in txt.splitlines():
  if "Edge.Cuts" not in ln: continue
  m=re.search(r'\(gr_line \(start ([-.\d]+) ([-.\d]+)\) \(end ([-.\d]+) ([-.\d]+)\)',ln)
  if m: out.append({"kind":"line","start":P(m[1],m[2]),"end":P(m[3],m[4])}); continue
  m=re.search(r'\(gr_arc \(start ([-.\d]+) ([-.\d]+)\) \(end ([-.\d]+) ([-.\d]+)\) \(angle ([-.\d]+)\)',ln)
  if m:
   c=P(m[1],m[2]); st=P(m[3],m[4]); ang=float(m[5])
   # Legacy KiCad: gr_arc start=center, end=startpoint; board coordinates are Y-down.
   a0=math.atan2(st[1]-c[1],st[0]-c[0]); rr=math.hypot(st[0]-c[0],st[1]-c[1])
   en=(c[0]+rr*math.cos(a0+math.radians(ang)), c[1]+rr*math.sin(a0+math.radians(ang)))
   out.append({"kind":"arc","center":c,"start":st,"end":en,"angle":ang})
 return out
def poly_for(e,forward=True,step=5.0):
 if e["kind"]=="line": q=[e["start"],e["end"]]
 else:
  c=e["center"]; st=e["start"]; ang=e["angle"]; a0=math.atan2(st[1]-c[1],st[0]-c[0]); rr=math.hypot(st[0]-c[0],st[1]-c[1])
  n=max(1,math.ceil(abs(ang)/step))
  q=[(c[0]+rr*math.cos(a0+math.radians(ang)*i/n),c[1]+rr*math.sin(a0+math.radians(ang)*i/n)) for i in range(n+1)]
 return q if forward else list(reversed(q))
def contour(edges):
 unused=list(range(len(edges))); first=unused.pop(0); pts=poly_for(edges[first]); cur=pts[-1]
 while unused:
  found=None
  for pos,i in enumerate(unused):
   e=edges[i]
   if near(cur,e["start"]): found=(pos,i,True); break
   if near(cur,e["end"]): found=(pos,i,False); break
  if found is None: raise ValueError(f"DISCONNECTED_EDGE_CUTS_AT {cur}; remaining={unused}")
  pos,i,fwd=found; unused.pop(pos); q=poly_for(edges[i],fwd); pts.extend(q[1:]); cur=pts[-1]
 if not near(pts[-1],pts[0]): raise ValueError(f"OPEN_EDGE_CUTS {pts[-1]} != {pts[0]}")
 pts[-1]=pts[0]
 return pts[:-1]
def inside(p,poly):
 x,y=p;c=False
 for i,a in enumerate(poly):
  b=poly[(i+1)%len(poly)]
  if ((a[1]>y)!=(b[1]>y)) and x < (b[0]-a[0])*(y-a[1])/(b[1]-a[1])+a[0]: c=not c
 return c
def rot(x,y,d):
 a=math.radians(d);return(x*math.cos(a)-y*math.sin(a),x*math.sin(a)+y*math.cos(a))
def corners(bb,at,d):
 x0,y0,x1,y1=bb
 return [(at[0]+u,at[1]+v) for u,v in [rot(x0,y0,d),rot(x1,y0,d),rot(x1,y1,d),rot(x0,y1,d)]]
def main():
 ap=argparse.ArgumentParser();ap.add_argument("pcb");ap.add_argument("physical");ap.add_argument("out");a=ap.parse_args()
 edges=primitives(Path(a.pcb).read_text(errors="replace")); poly=contour(edges); ph=json.load(open(a.physical))
 locked=[];gaps=[]
 for c in ph["components"]:
  bb=c["courtyard_local_bbox_mm"]
  if not bb:gaps.append({"ref":c["ref"],"reason":"NO_COURTYARD_COLLISION_ENVELOPE"});continue
  cs=corners(bb,c["at_mm"][:2],c["at_mm"][2]); locked.append({"ref":c["ref"],"pose_mm":c["at_mm"],"courtyard_corners_mm":cs,
   "all_corners_inside_outline":all(inside(x,poly) for x in cs)})
 out={"format":"VMBS-GEOMETRY-DOMAIN-1","mode":"RECONSTRUCTION",
  "outline":{"polygon_mm":poly,"source":"KICAD_EDGE_CUTS","primitive_count":len(edges),"arc_chord_max_degrees":5.0},
  "locked_poses":locked,"gaps":gaps,
  "novel_domain_policy":{"status":"NOT_YET_IMPLEMENTED","input":"TargetMechanicalProfile","method":"finite deterministic pose-domain generation"}}
 Path(a.out).write_text(json.dumps(out,indent=2)+"\n")
 print(json.dumps({"edge_primitives":len(edges),"outline_vertices":len(poly),"collision_envelopes":len(locked),"gaps":len(gaps)},indent=2))
if __name__=="__main__":main()
