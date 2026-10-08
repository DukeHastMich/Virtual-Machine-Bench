#!/usr/bin/env python3
"""Anti-smuggling regression for generic component decode rules.
Alter one PCB selector path, re-import BS, and require decoded VMB configuration domain to change.
"""
import re,subprocess,json,tempfile,sys
from pathlib import Path
root=Path(__file__).resolve().parents[1]
src=(root/'upstream/isa8_eth/KiCad/ISA8_Ethernet.kicad_pcb').read_text()
# Detach SW3 element 1 from IOS0's pull-up path by tying that switch contact to GND.
start=src.find('(module My_Components:Switch_DIP_4_Positions')
if start<0: raise SystemExit('SW3 footprint not found')
end=src.find('\n  (module ',start+1); block=src[start:end]
if '(fp_text reference SW3' not in block: raise SystemExit('expected SW3 at selected footprint')
pat=r'(\(pad 1 [\s\S]*?\(net )117( "Net-\(RN2-Pad8\)"\)\))'
newblock,n=re.subn(pat,r'\g<1>32 "GND"))',block,count=1)
if n!=1: raise SystemExit('SW3 pad1 topology mutation failed')
mut=src[:start]+newblock+src[end:]
with tempfile.TemporaryDirectory() as td:
 td=Path(td); cad=td/'mut.kicad_pcb'; bs=td/'mut.bs.json'; ir=td/'mut.ir.json'; cad.write_text(mut)
 subprocess.run([sys.executable,str(root/'tools/bs_complete.py'),'import',str(cad),str(bs)],check=True,stdout=subprocess.DEVNULL)
 subprocess.run([sys.executable,str(root/'tools/board_analyzer.py'),str(bs),str(root/'compiler_db/component_store.json'),str(root/'compiler_db/ISA8_Ethernet.component_map.json'),str(root/'compiler_db/code_store.json'),str(root/'compiler_db/platform_store.json'),str(ir)],check=True,stdout=subprocess.DEVNULL)
 base=json.load(open(root/'compiler_db/ISA8_Ethernet.board_ir.cp16.json')); m=json.load(open(ir))
 def prop(d,name): return next(x for x in d['configuration_decode'] if x['ref']=='U1' and x['property']==name)
 before,after=prop(base,'IO_BASE'),prop(m,'IO_BASE')
 bv=before['allowed_values']; av=after['allowed_values']
 ok=(len(bv)==16 and len(av)==8 and all((x & 0x20)==0 for x in av))
 out={'test':'DECODE_DOMAIN_FOLLOWS_SELECTOR_TOPOLOGY','mutation':'SW3 element 1 / IOS0 pull-up path removed; IOS0 falls to documented default 0','before_io_base_values':bv,'after_io_base_values':av,'pass':ok}
 (root/'compiler_db/cp16_mutation_test.json').write_text(json.dumps(out,indent=2)+'\n')
 print(json.dumps(out,indent=2))
 if not ok: raise SystemExit(1)
