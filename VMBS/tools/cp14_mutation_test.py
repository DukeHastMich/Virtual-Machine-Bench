#!/usr/bin/env python3
"""Anti-smuggling regression: alter specimen topology, re-import BS, require Analyzer output to change."""
import re,subprocess,json,tempfile,sys
from pathlib import Path
root=Path(__file__).resolve().parents[1]
src=(root/'upstream/isa8_eth/KiCad/ISA8_Ethernet.kicad_pcb').read_text()
# Scope mutation to SW3 footprint, then detach pad 1 from RN2 and tie it to GND.
start=src.find('(module My_Components:Switch_DIP_4_Positions')
if start<0: raise SystemExit('SW3 footprint not found')
end=src.find('\n  (module ',start+1)
block=src[start:end]
if '(fp_text reference SW3' not in block: raise SystemExit('expected SW3 at selected footprint')
pat=r'(\(pad 1 [\s\S]*?\(net )117( "Net-\(RN2-Pad8\)"\)\))'
newblock,n=re.subn(pat,r'\g<1>32 "GND"))',block,count=1)
if n!=1: raise SystemExit('SW3 pad1 topology mutation failed')
mut=src[:start]+newblock+src[end:]
with tempfile.TemporaryDirectory() as td:
 td=Path(td); cad=td/'mut.kicad_pcb'; bs=td/'mut.bs.json'; ir=td/'mut.ir.json'; cad.write_text(mut)
 subprocess.run([sys.executable,str(root/'tools/bs_complete.py'),'import',str(cad),str(bs)],check=True,stdout=subprocess.DEVNULL)
 subprocess.run([sys.executable,str(root/'tools/board_analyzer.py'),str(bs),str(root/'compiler_db/component_store.json'),str(root/'compiler_db/ISA8_Ethernet.component_map.json'),str(root/'compiler_db/code_store.json'),str(root/'compiler_db/platform_store.json'),str(ir)],check=True,stdout=subprocess.DEVNULL)
 base=json.load(open(root/'compiler_db/ISA8_Ethernet.board_ir.cp14.json')); m=json.load(open(ir))
 def ios0(d):
  u=next(x for x in d['module_configuration'] if x['ref']=='U1'); return next(x for x in u['pin_configuration'] if x['pin']=='IOS0')['source']
 before,after=ios0(base),ios0(m)
 ok=before.get('kind')=='USER_CONTROL' and after.get('kind')!='USER_CONTROL'
 out={'test':'TOPOLOGY_MUTATION_CHANGES_DERIVATION','mutation':'SW3 pad1 detached from RN2 and tied to GND','before':before,'after':after,'pass':ok}
 (root/'compiler_db/cp14_mutation_test.json').write_text(json.dumps(out,indent=2)+'\n')
 print(json.dumps(out,indent=2))
 if not ok: raise SystemExit(1)
