#!/usr/bin/env python3
import argparse,json,re
from pathlib import Path
BOUNDARY={"edge_connector","pin_header","connector_magnetic_rj45","connector"}
PASSIVE_PREFIX=("resistor","capacitor","crystal","mounting","logo")
def main():
 ap=argparse.ArgumentParser(); ap.add_argument("endpoint_json"); ap.add_argument("board"); ap.add_argument("store"); ap.add_argument("output"); a=ap.parse_args()
 d=json.load(open(a.endpoint_json)); b=json.load(open(a.board)); s=json.load(open(a.store))
 rec={r["part_id"]:r for r in s["records"]}; inst={c["ref"]:c for c in b["components"]}; buses={}
 for p in d["paths"]:
  typed=[]; boundary=[]; passive=[]; unresolved=[]
  for e in p["endpoints"]:
   x=dict(e); c=inst.get(e["ref"]); r=rec.get(c["part_id"],{}) if c else {}; cls=(r.get("class") or "").lower()
   if x.get("direction"): typed.append(x)
   elif cls in BOUNDARY: x["semantic_kind"]="external_boundary"; x["typing_source"]="component_class_boundary"; boundary.append(x)
   elif cls.startswith(PASSIVE_PREFIX): x["semantic_kind"]="passive"; x["typing_source"]="component_class_passive"; passive.append(x)
   else: unresolved.append(x)
  dirs={x.get("direction") for x in typed}
  if "output" in dirs and "input" in dirs: status="DRIVER_RECEIVER"
  elif "bidirectional" in dirs: status="BIDIRECTIONAL"
  elif typed and dirs <= {"input"} and boundary and not unresolved: status="EXTERNAL_SOURCE_TO_RECEIVERS"
  elif typed and dirs <= {"output"} and boundary and not unresolved: status="DRIVERS_TO_EXTERNAL_SINK"
  elif typed and not unresolved and (boundary or passive): status="TYPED_WITH_BOUNDARY_OR_PASSIVE"
  else: status="AMBIGUOUS_DIRECTION"
  p["cp209"]={"status":status,"boundary_endpoints":boundary,"passive_endpoints":passive,"unresolved_endpoints":unresolved}
  m=re.fullmatch(r"/?(A|D)(\d+)",p["net"])
  if m: buses.setdefault(m.group(1),[]).append({"net":p["net"],"bit":int(m.group(2)),"path_id":p["id"]})
 for v in buses.values(): v.sort(key=lambda q:q["bit"])
 d["interface_groups"]=[{"id":"ISA_ADDRESS","kind":"parallel_address","members":buses.get("A",[])},
                        {"id":"ISA_DATA","kind":"parallel_data","members":buses.get("D",[])}]
 counts={}; unresolved=0
 for p in d["paths"]:
  st=p["cp209"]["status"]; counts[st]=counts.get(st,0)+1; unresolved+=len(p["cp209"]["unresolved_endpoints"])
 d["cp209_summary"]={"path_status":counts,"unresolved_endpoints_after_classification":unresolved,
                     "interface_groups":len([g for g in d["interface_groups"] if g["members"]])}
 Path(a.output).write_text(json.dumps(d,indent=2)+"\n",encoding="utf-8"); print(json.dumps(d["cp209_summary"]))
if __name__=="__main__": main()
