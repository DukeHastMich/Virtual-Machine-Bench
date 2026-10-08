#!/usr/bin/env python3
import copy,hashlib,json,subprocess,sys,tempfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]; TOOLS=ROOT/"tools"; BS=ROOT/"bs_native/ISA8_Ethernet.bs.json"; IR=ROOT/"compiler_db/ISA8_Ethernet.board_ir.cp18.json"
with tempfile.TemporaryDirectory() as td:
 td=Path(td); out=td/"v2.json"
 subprocess.check_call([sys.executable,str(TOOLS/"board_v2_lift.py"),str(BS),str(IR),str(out)])
 v2=json.load(open(out)); v1=json.load(open(BS))
 assert v2["format"]=="VMBS-BOARD-2"
 assert v2["source_facets"][0]["payload"]==v1
 assert v2["source_facets"][0]["sha256"]==hashlib.sha256(BS.read_bytes()).hexdigest()
 assert v2["readiness"]["roundtrip_source"]["ready"]
 assert v2["readiness"]["structural"]["ready"]
 assert v2["readiness"]["vmb_execution"]["ready"]
 for k in ("electrical","physical","routing","manufacturing","evidence"): assert not v2["readiness"][k]["ready"]
 # Unknown identity may remain preservable/round-trippable, but cannot claim structural green.
 ir=json.load(open(IR)); bad=copy.deepcopy(ir); bad["devices"][0]["part_id"]=None
 bp=td/"bad_ir.json"; bp.write_text(json.dumps(bad)); bo=td/"bad_v2.json"
 subprocess.check_call([sys.executable,str(TOOLS/"board_v2_lift.py"),str(BS),str(bp),str(bo)])
 q=json.load(open(bo)); assert q["readiness"]["roundtrip_source"]["ready"] and not q["readiness"]["structural"]["ready"] and not q["readiness"]["vmb_execution"]["ready"]
print("CP40 VMBS-BOARD-2 multi-destination lift PASS")
