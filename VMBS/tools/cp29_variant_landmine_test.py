#!/usr/bin/env python3
import json,subprocess,sys,tempfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
with tempfile.TemporaryDirectory() as td:
 td=Path(td); ir=td/'board.ir.json'; out=td/'construction.ir.json'
 ir.write_text(json.dumps({'format':'VMB-BOARD-IR-1','module_bindings':[{'ref':'U666','part_id':'VARIANT_STUB:74LS245','module_id':'VMB.STUB.DIPSHIT_BLACKHOLE','resolved':True}]}))
 p=subprocess.run([sys.executable,str(ROOT/'tools/board_constructor.py'),str(ir),str(ROOT/'compiler_db/code_store.json'),str(out)],text=True,capture_output=True)
 text=(p.stdout or '')+(p.stderr or '')
 assert p.returncode!=0
 assert "You didn't write the code for this dipshit!" in text,text
 print("PASS: dipshit blackhole detonated exactly as intended.")
