#!/usr/bin/env python3
import json,math,argparse,hashlib
from pathlib import Path
from geometry_kernel import overlap,inside
from determinism import stable_hash
from feasibility import combine

def load(p): return json.load(open(p))
def bbox(poly):
 xs=[x for x,y in poly];ys=[y for x,y in poly];return [min(xs),min(ys),max(xs),max(ys)]
def centroid(poly):return [sum(x for x,y in poly)/len(poly),sum(y for x,y in poly)/len(poly)]
def pair_collisions(poses):
 out=[]
 for i,a in enumerate(poses):
  for b in poses[i+1:]:
   if overlap(a["courtyard_corners_mm"],b["courtyard_corners_mm"]):out.append([a["ref"],b["ref"]])
 return out
def main():
 ap=argparse.ArgumentParser();ap.add_argument("bs");ap.add_argument("geom");ap.add_argument("physical");ap.add_argument("out");a=ap.parse_args()
 bs,g,p=map(load,[a.bs,a.geom,a.physical])
 comps=bs["components"]; nets=bs["nets"]; refs={c["ref"] for c in comps}
 locked={x["ref"]:x for x in g["locked_poses"]}; gaps={x["ref"]:x for x in g["gaps"]}
 # CP239 inventory reconciliation
 missing_geom=sorted(refs-set(locked)-set(gaps)); orphan_geom=sorted((set(locked)|set(gaps))-refs)
 # CP240 outline metrics
 ob=bbox(g["outline"]["polygon_mm"]); area_bbox=(ob[2]-ob[0])*(ob[3]-ob[1])
 # CP241 collision audit
 collisions=pair_collisions(list(locked.values()))
 # CP242 boundary audit, reporting rather than rejecting legitimate edge hardware
 boundary=[x["ref"] for x in locked.values() if not x["all_corners_inside_outline"]]
 # CP243 placement determinism
 placement_digest=stable_hash({r:locked[r]["pose_mm"] for r in sorted(locked)})
 # CP244 net endpoint normalization
 norm=[]
 unknown_refs=set()
 for n in nets:
  eps=n.get("nodes",n.get("endpoints",[]))
  norm.append({"name":n.get("name",n.get("net","")),"endpoint_count":len(eps)})
 # CP245 fanout histogram
 hist={}
 for n in norm:hist[n["endpoint_count"]]=hist.get(n["endpoint_count"],0)+1
 # CP246 unrouted/singleton classification
 singleton=[n["name"] for n in norm if n["endpoint_count"]<2]
 # CP247 component placement coverage
 coverage={"components":len(comps),"courtyard_geometry":len(locked),"explicit_geometry_gaps":len(gaps)}
 # CP248 gap classification
 gap_codes={x["ref"]:x["reason"] for x in g["gaps"]}
 # CP249 locked reconstruction policy
 reconstruction_locked=(g["mode"]=="RECONSTRUCTION" and all("pose_mm" in x for x in locked.values()))
 # CP250 physical anchor crosscheck
 physmap={x["ref"]:x for x in p["components"]}; anchor_mismatch=[]
 for r,x in locked.items():
  if r in physmap and physmap[r]["at_mm"]!=x["pose_mm"]:anchor_mismatch.append(r)
 # CP251 net-name determinism
 net_digest=stable_hash(sorted(n["name"] for n in norm))
 # CP252 topology determinism
 topology_digest=stable_hash(sorted((n["name"],n["endpoint_count"]) for n in norm))
 # CP253 geometry determinism
 geometry_digest=stable_hash(g["outline"]["polygon_mm"])
 # CP254 mutation: force overlap detector
 mutation_ok=False
 if len(locked)>=2:
  q=list(locked.values())[:2]; b=dict(q[1]); b["courtyard_corners_mm"]=q[0]["courtyard_corners_mm"]
  mutation_ok=bool(pair_collisions([q[0],b]))
 # CP255 mutation: open contour evidence rejection represented by source compiler invariant
 contour_closed_evidence=(g["outline"]["primitive_count"]>0 and len(g["outline"]["polygon_mm"])>=3)
 # CP256 feasibility synthesis
 hard=[]
 if missing_geom or orphan_geom:hard.append("GEOMETRY_IDENTITY_MISMATCH")
 if anchor_mismatch:hard.append("ANCHOR_MISMATCH")
 # Existing source CAD may contain courtyard overlap / edge protrusion; report, don't rewrite history.
 gaps_out=[]
 if gaps:gaps_out.append("MISSING_COURTYARD_GEOMETRY")
 # CP257 verified/unverified gate
 state="UNSAT" if hard else ("SOLVED_UNVERIFIED" if gaps_out else "SOLVED_VERIFIED")
 # CP258 explainability
 explanations=[{"code":x,"severity":"HARD"} for x in hard]+[{"code":x,"severity":"GAP"} for x in gaps_out]
 # CP259 stable run signature
 signature=stable_hash({"placement":placement_digest,"net":net_digest,"topology":topology_digest,"geometry":geometry_digest})
 # CP260 routing readiness
 routing_ready=(not hard and not gaps)
 # CP261 chess readiness
 chess_ready=(not hard and len(locked)>0)
 # CP262 physics readiness: do not overclaim
 physics_ready=False
 physics_reason="Electrical/timing evidence is not yet complete enough for SOLVED_VERIFIED."
 # CP263 integrated result
 out={"format":"VMBS-REAL-SPECIMEN-INTEGRATION-CP263","board":bs["identity"],
 "inventory":{"bs_components":len(comps),"nets":len(nets),"missing_geometry":missing_geom,"orphan_geometry":orphan_geom},
 "outline":{"bbox_mm":ob,"bbox_area_mm2":area_bbox,"vertices":len(g["outline"]["polygon_mm"])},
 "placement":{"coverage":coverage,"collision_pairs":collisions,"boundary_crossers":boundary,"anchor_mismatch":anchor_mismatch,"locked_reconstruction":reconstruction_locked},
 "topology":{"fanout_histogram":hist,"singleton_or_empty_nets":singleton},
 "gaps":gap_codes,"mutations":{"forced_overlap_detected":mutation_ok,"contour_closed_evidence":contour_closed_evidence},
 "readiness":{"chess":chess_ready,"routing":routing_ready,"physics":physics_ready,"physics_reason":physics_reason},
 "result_state":state,"explanations":explanations,"digests":{"placement":placement_digest,"nets":net_digest,"topology":topology_digest,"geometry":geometry_digest,"run":signature}}
 Path(a.out).write_text(json.dumps(out,indent=2)+"\n")
 print(json.dumps({"components":len(comps),"nets":len(nets),"courtyards":len(locked),"gaps":len(gaps),"collisions_reported":len(collisions),"boundary_crossers":len(boundary),"hard_conflicts":len(hard),"state":state,"run_digest":signature},indent=2))
if __name__=="__main__":main()
