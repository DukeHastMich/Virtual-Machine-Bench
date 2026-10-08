#!/usr/bin/env python3
import json,subprocess,sys,tempfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]; VMB=ROOT.parent; TOOLS=ROOT/"tools"; DB=ROOT/"compiler_db"
m=json.load(open(DB/"ISA8_Ethernet.chimera.cp42.json",encoding="utf-8"))
assert len(m["instances"])==3 and len(m["sources"])==3 and m["ready"]
assert not m["diagnostics"]
with tempfile.TemporaryDirectory() as td:
 td=Path(td); vr=td/"vmb"; vr.mkdir()
 (vr/"Shared.vb").write_text("Public Class SharedThing\nEnd Class\n",encoding="utf-8")
 (vr/"Other.vb").write_text("Public Class OtherThing\nEnd Class\n",encoding="utf-8")
 code={"format":"VMB-BOARD-CODE-1","instructions":[
   {"op":"ALLOC_MODULE","id":"X7","module_id":"TEST.A","part_id":"A"},
   {"op":"ALLOC_MODULE","id":"Z2","module_id":"TEST.B","part_id":"B"},
   {"op":"ALLOC_MODULE","id":"Q9","module_id":"TEST.C","part_id":"C"},
   {"op":"ATTACH_PLATFORM","id":"P","platform":"ISA8","signals":[]},
   {"op":"FINALIZE_BOARD","hardware_tits":0,"execution_tits":0,"default_execution_tits":0}]}
 cs={"modules":[
   {"module_id":"TEST.A","implementation":{"language":"VB.NET","status":"implemented","compile_allowed":True,"source":"Shared.vb","class":"SharedThing"}},
   {"module_id":"TEST.B","implementation":{"language":"VB.NET","status":"implemented","compile_allowed":True,"source":"Shared.vb","class":"SharedThing"}},
   {"module_id":"TEST.C","implementation":{"language":"VB.NET","status":"implemented","compile_allowed":True,"source":"Other.vb","class":"OtherThing"}}]}
 cp=td/"code.json"; sp=td/"store.json"; mp=td/"manifest.json"
 cp.write_text(json.dumps(code)); sp.write_text(json.dumps(cs))
 subprocess.check_call([sys.executable,str(TOOLS/"chimera_plan.py"),str(cp),str(sp),str(vr),str(mp)])
 q=json.load(open(mp)); assert q["ready"] and len(q["instances"])==3 and len(q["sources"])==2
 out=td/"stage"
 subprocess.check_call([sys.executable,str(TOOLS/"chimera_emit_project.py"),str(cp),str(mp),str(vr),str(out),"--board-id","ARBITRARY_TEST"])
 lock=json.load(open(out/"chimera.lock.json"))
 assert len(lock["instances"])==3 and len(lock["sources"])==2
 assert len(list((out/"modules").glob("*.vb")))==2
print("CP39 generic chimera planner/stager PASS")
