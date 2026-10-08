#!/usr/bin/env python3
"""Lift VMBS-BOARD-1 + analyzed IR into normalized multi-destination VMBS-BOARD-2.
The complete v1 semantic capsule is embedded unchanged as a KiCad source facet.
Only analyzer-proven facts are promoted; unknown stays unknown.
"""
import argparse,copy,hashlib,json
from pathlib import Path
ap=argparse.ArgumentParser(); ap.add_argument("board_v1"); ap.add_argument("board_ir"); ap.add_argument("out"); a=ap.parse_args()
v1=json.load(open(a.board_v1,encoding="utf-8")); ir=json.load(open(a.board_ir,encoding="utf-8"))
if v1.get("format")!="VMBS-BOARD-1": raise SystemExit("expected VMBS-BOARD-1")
if ir.get("format")!="VMBS-BOARD-IR-1": raise SystemExit("expected VMBS-BOARD-IR-1")
unknown=[d["ref"] for d in ir.get("devices",[]) if not d.get("part_id")]
unresolved_modules=[b["ref"] for b in ir.get("module_bindings",[]) if not b.get("resolved")]
errors=[d for d in ir.get("diagnostics",[]) if d.get("severity")=="error"]
components=[]
for d in ir.get("devices",[]):
    components.append({"ref":d.get("ref"),"part_id":d.get("part_id"),"value":d.get("value"),
                       "footprint":d.get("footprint"),"placement":{"at":d.get("at")},
                       "resolution":"resolved" if d.get("part_id") else "unknown"})
nets=[{"id":x.get("net_id"),"name":x.get("name"),"endpoints":copy.deepcopy(x.get("endpoints",[]))} for x in ir.get("connections",[])]
readiness={
 "roundtrip_source":{"ready":True,"reason":"complete VMBS-BOARD-1 KiCad semantic capsule preserved"},
 "structural":{"ready":not unknown and not errors,"blockers":{"unknown_components":unknown,"analysis_errors":len(errors)}},
 "vmb_execution":{"ready":not unknown and not unresolved_modules and not errors,"blockers":{"unknown_components":unknown,"unresolved_modules":unresolved_modules,"analysis_errors":len(errors)}},
 "electrical":{"ready":False,"reason":"not yet proven by BOARD-1/IR lift"},
 "physical":{"ready":False,"reason":"package/body/footprint/mechanics completeness not yet proven"},
 "routing":{"ready":False,"reason":"normalized routing constraints not yet represented"},
 "manufacturing":{"ready":False,"reason":"physical/electrical/routing readiness not proven"},
 "evidence":{"ready":False,"reason":"board-fact provenance threshold not yet represented in BOARD-1"}
}
out={"format":"VMBS-BOARD-2","schema_version":2,
 "authority":{"board_specific":True,"component_reusable_facts_external":"Component Store"},
 "identity":{},
 "components":components,"nets":nets,
 "configuration":{"controls":copy.deepcopy(ir.get("controls",[])),"module_configuration":copy.deepcopy(ir.get("module_configuration",[])),"decoded_properties":copy.deepcopy(ir.get("configuration_decode",[]))},
 "programmed_contents":copy.deepcopy(ir.get("programmed_contents",[])),
 "external_interfaces":copy.deepcopy(ir.get("buses",[])),
 "mechanics":{},"electrical":{},"routing":{},"evidence":[],
 "readiness":readiness,
 "source_facets":[{"kind":"KICAD_SEMANTIC_CAPSULE","format":"VMBS-BOARD-1","sha256":hashlib.sha256(Path(a.board_v1).read_bytes()).hexdigest(),"payload":v1}],
 "analysis_provenance":{"board_ir_sha256":hashlib.sha256(Path(a.board_ir).read_bytes()).hexdigest()}}
Path(a.out).write_text(json.dumps(out,indent=2)+"\n",encoding="utf-8")
print(json.dumps({"components":len(components),"nets":len(nets),"unknown_components":len(unknown),"readiness":{k:v["ready"] for k,v in readiness.items()}},indent=2))
