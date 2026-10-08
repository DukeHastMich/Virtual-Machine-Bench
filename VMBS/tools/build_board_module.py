#!/usr/bin/env python3
"""End-to-end VMBS board-module constructor: KiCad -> BS -> Analyzer -> Constructor -> Board Code -> VB.NET."""
import argparse,subprocess,sys,json,hashlib
from pathlib import Path
ap=argparse.ArgumentParser(); ap.add_argument('kicad'); ap.add_argument('--outdir',required=True); ap.add_argument('--component-store',default='compiler_db/component_store.json'); ap.add_argument('--component-map',default='compiler_db/ISA8_Ethernet.component_map.json'); ap.add_argument('--code-store',default='compiler_db/code_store.json'); ap.add_argument('--platform-store',default='compiler_db/platform_store.json'); ap.add_argument('--class-name',default='GeneratedBoardModule'); a=ap.parse_args()
ROOT=Path(__file__).resolve().parents[1]; out=Path(a.outdir); out.mkdir(parents=True,exist_ok=True)
def P(x):
 p=Path(x); return p if p.is_absolute() else ROOT/p
def run(*xs): subprocess.check_call([sys.executable,*map(str,xs)])
bs=out/'board.bs.json'; ir=out/'board.ir.json'; plan=out/'construction.ir.json'; code=out/'board.code.json'; runtime=out/'reference.runtime.json'; vb=out/'GeneratedBoard.vb'
run(ROOT/'tools/bs_complete.py','import',P(a.kicad),bs)
run(ROOT/'tools/board_analyzer.py',bs,P(a.component_store),P(a.component_map),P(a.code_store),P(a.platform_store),ir)
run(ROOT/'tools/board_constructor.py',ir,P(a.code_store),plan)
run(ROOT/'tools/construction_lower.py',plan,code)
run(ROOT/'tools/board_code_validate.py',code)
run(ROOT/'tools/reference_board_runtime.py',code,runtime)
run(ROOT/'tools/vb_board_emit.py',code,vb,'--class-name',a.class_name)
summary={'format':'VMBS-END-TO-END-1','source':str(P(a.kicad)),'artifacts':{x.name:hashlib.sha256(x.read_bytes()).hexdigest() for x in [bs,ir,plan,code,runtime,vb]},'board_ir':json.load(open(ir))['analysis_summary'],'constructor':json.load(open(plan))['summary'],'reference_runtime_valid':json.load(open(runtime))['valid'],'vb_lines':len(vb.read_text().splitlines())}
(out/'build_summary.json').write_text(json.dumps(summary,indent=2)+'\n'); print(json.dumps(summary,indent=2))
