#!/usr/bin/env python3
import json, subprocess, tempfile, pathlib, sys
ROOT=pathlib.Path(__file__).resolve().parents[1]
ir=json.load(open(ROOT/'compiler_db/ISA8_Ethernet.board_ir.cp18.json'))
# Delete the physical IRQ5 semantic link but leave the decoded selector domain unchanged.
ir['interface_links']=[x for x in ir['interface_links'] if not any(e.get('kind')=='PLATFORM_PIN' and e.get('signal')=='IRQ5' for e in x['endpoints'])]
with tempfile.TemporaryDirectory() as td:
 p=pathlib.Path(td); src=p/'ir.json'; out=p/'plan.json'; src.write_text(json.dumps(ir))
 subprocess.run([sys.executable,str(ROOT/'tools/board_constructor.py'),str(src),str(ROOT/'compiler_db/code_store.json'),str(out)],check=True,stdout=subprocess.DEVNULL)
 plan=json.load(open(out)); hits=[d for d in plan['diagnostics'] if d.get('code')=='DECODED_IRQ_HAS_NO_PHYSICAL_PATH']
 r={'test':'remove physical IRQ5 path while decode still permits IRQ5','diagnostic':hits,'hardware_tits':plan['substrates']['VMB_HARDWARE']['tits'],'pass':bool(hits) and plan['substrates']['VMB_HARDWARE']['tits']==1}
 (ROOT/'compiler_db/cp18_irq_mutation_test.json').write_text(json.dumps(r,indent=2)+'\n'); print(json.dumps(r,indent=2)); raise SystemExit(0 if r['pass'] else 1)
