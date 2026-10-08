#!/usr/bin/env python3
"""CP206 conservative Component Store -> solver constraint audit."""
import argparse,json
from pathlib import Path
def main():
 p=argparse.ArgumentParser(); p.add_argument("component_store"); p.add_argument("output"); a=p.parse_args()
 s=json.load(open(a.component_store,encoding="utf-8"))
 o={"format":"VMBS-SOLVER-CONSTRAINT-IR-1","board_id":None,"mode":"NOVEL","constraints":[],"gaps":[]}
 for r in s.get("records",[]):
  pid=r.get("part_id") or r.get("display_name","UNKNOWN"); n=0
  src={"component_part_id":pid,"display_name":r.get("display_name"),"evidence":r.get("evidence",{})}
  def add(cls,kind,value,hard=True,notes=None):
   nonlocal n; n+=1
   o["constraints"].append({"id":f"{pid}:{kind}:{n}","class":cls,"kind":kind,"hard":hard,
    "subject":pid,"value":value,"authority":"supporting","mutability":"bounded","source":src,"notes":notes})
  ph=r.get("physical") or {}; pkg=ph.get("package") or r.get("package")
  if pkg: add("HARD_PHYSICAL","package_identity",pkg)
  else: o["gaps"].append({"code":"MISSING_PACKAGE","subject":pid,"severity":"BLOCKS_PHYSICAL_SOLVE","detail":"No package identity recorded."})
  if ph.get("package_pin_maps"): add("HARD_CORRECTNESS","package_pin_map",ph["package_pin_maps"])
  elif ph.get("physical_pin_map_complete") is False:
   o["gaps"].append({"code":"INCOMPLETE_PHYSICAL_PIN_MAP","subject":pid,"severity":"BLOCKS_PHYSICAL_SOLVE","detail":"Store explicitly marks physical pin map incomplete."})
  if r.get("electrical"): add("HARD_ELECTRICAL","component_electrical",r["electrical"])
  if r.get("timing"): add("HARD_TIMING","component_timing_evidence",r["timing"],notes="Requires later normalization into typed path constraints.")
  for q in r.get("support_requirements") or []: add("HARD_ELECTRICAL","support_requirement",q)
  for q in (r.get("support_mesh") or {}).get("requirements") or []: add("HARD_ELECTRICAL","support_mesh_requirement",q)
 o["summary"]={"component_records":len(s.get("records",[])),"constraints":len(o["constraints"]),"gaps":len(o["gaps"])}
 Path(a.output).write_text(json.dumps(o,indent=2)+"\n",encoding="utf-8"); print(json.dumps(o["summary"]))
if __name__=="__main__": main()
