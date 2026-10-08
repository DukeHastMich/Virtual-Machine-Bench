#!/usr/bin/env python3
import copy,json,subprocess,tempfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]; T=ROOT/"tools"; IR=ROOT/"compiler_db/ISA8_Ethernet.board_ir.cp18.json"; CS=ROOT/"compiler_db/code_store.json"
with tempfile.TemporaryDirectory() as td:
 td=Path(td); out=td/"real.json"
 r=subprocess.run(["/usr/bin/python3",str(T/"chimera_contract_validate.py"),str(IR),str(CS),str(out)])
 assert r.returncode==0 and json.load(open(out))["ready"]
 # Contract removal must fail generically, proving validation is not board-name magic.
 cs=json.load(open(CS)); target=next(x for x in cs["modules"] if x["module_id"]=="VMB.MEM.SST39SF040"); target.pop("composition",None)
 sp=td/"store.json"; sp.write_text(json.dumps(cs)); bad=td/"bad.json"
 assert subprocess.run(["/usr/bin/python3",str(T/"chimera_contract_validate.py"),str(IR),str(sp),str(bad)]).returncode==2
 q=json.load(open(bad)); assert any(x["code"]=="COMPOSITION_CONTRACT_MISSING" and x["module_id"]=="VMB.MEM.SST39SF040" for x in q["diagnostics"])
print("CP41 composition endpoint contracts PASS")
