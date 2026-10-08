#!/usr/bin/env python3
import copy,json,subprocess,sys,tempfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]; DB=ROOT/"compiler_db"; TOOLS=ROOT/"tools"
store=json.load(open(DB/"component_store.json",encoding="utf-8"))
assert store["schema_version"]==3
assert len(store["records"])>=23
families=store["variant_stub_policy"]["families"]
assert len(families)==2581
assert sum(len(x.get("variants",[])) for x in families)==10867

# Empty migrated meshes remain valid.
subprocess.check_call([sys.executable,str(TOOLS/"component_store_v3_validate.py"),str(DB/"component_store.json")])

# Synthetic semantic mesh: prove generic roles/evidence and alternatives validate
# without teaching the store any specimen motherboard recipe.
s=copy.deepcopy(store)
r=copy.deepcopy(s["records"][0])
r["support_mesh"]={
 "requirements":[{
   "id":"req.clock","relation":"requires",
   "subject":{"kind":"clock","name":"CORE_CLOCK"},
   "object":{"kind":"capability","name":"LEGAL_CLOCK_SOURCE"},
   "strength":"mandatory","scope":"part",
   "constraints":{"frequency_hz":{"min":1000000,"max":25000000}},
   "evidence":[{"kind":"manufacturer_datasheet","ref":"synthetic-test"}]
 }],
 "provides":[{
   "id":"prov.bus","relation":"provides",
   "subject":{"kind":"interface","name":"LOCAL_BUS"},
   "strength":"reference","scope":"interface",
   "evidence":[{"kind":"manufacturer_datasheet","ref":"synthetic-test"}]
 }],
 "alternatives":[{
   "id":"alt.clock","relation":"alternative",
   "subject":{"kind":"component_class","name":"CLOCK_GENERATOR"},
   "object":{"kind":"component_class","name":"CHIPSET_CLOCK_OUTPUT"},
   "strength":"inferred","scope":"family","reason":"synthetic alternative test"
 }],
 "patterns":[{
   "id":"pattern.clock-feed","name":"Clock feed","purpose":"satisfy processor clock input",
   "participants":[{"role":"consumer","kind":"clock"},{"role":"source","kind":"component_class"}],
   "edges":[{"from":"source","to":"consumer","relation":"drives"}],
   "constraints":{},"strength":"reference",
   "evidence":[{"kind":"manufacturer_reference_design","ref":"synthetic-test"}]
 }]
}
s["records"][0]=r
with tempfile.TemporaryDirectory() as td:
 p=Path(td)/"good.json"; p.write_text(json.dumps(s),encoding="utf-8")
 subprocess.check_call([sys.executable,str(TOOLS/"component_store_v3_validate.py"),str(p)])
 # A specimen refdes masquerading as a reusable participant must fail.
 bad=copy.deepcopy(s); bad["records"][0]["support_mesh"]["patterns"][0]["participants"][0]["role"]="U17"
 q=Path(td)/"bad.json"; q.write_text(json.dumps(bad),encoding="utf-8")
 rc=subprocess.run([sys.executable,str(TOOLS/"component_store_v3_validate.py"),str(q)],capture_output=True,text=True)
 assert rc.returncode!=0 and "reference-designator" in rc.stdout
print("CP38 support mesh contract PASS")
