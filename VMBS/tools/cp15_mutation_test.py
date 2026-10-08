#!/usr/bin/env python3
"""Anti-smuggling regression for interface derivation: rewire EEPROM CS and require the semantic link to disappear."""
import re,subprocess,json,tempfile,sys
from pathlib import Path
root=Path(__file__).resolve().parents[1]
src=(root/'upstream/isa8_eth/KiCad/ISA8_Ethernet.kicad_pcb').read_text()
# Locate the footprint by its reference, not by a board-global byte offset.
mods=list(re.finditer(r'(?m)^  \(module ',src))
span=None
for i,m in enumerate(mods):
 end=mods[i+1].start() if i+1<len(mods) else len(src)
 block=src[m.start():end]
 if '(fp_text reference U3' in block:
  span=(m.start(),end,block);break
if not span: raise SystemExit('U3 footprint not found')
start,end,block=span
# U3 pad 1 is CS by component semantics. Deliberately rewire that physical pad from /EECS to GND.
pat=r'(\(pad 1 [\s\S]*?\(net )91( /EECS\)\))'
newblock,n=re.subn(pat,r'\g<1>32 GND))',block,count=1)
if n!=1: raise SystemExit('U3 CS mutation failed')
mut=src[:start]+newblock+src[end:]
with tempfile.TemporaryDirectory() as td:
 td=Path(td); cad=td/'mut.kicad_pcb'; bs=td/'mut.bs.json'; ir=td/'mut.ir.json'; cad.write_text(mut)
 subprocess.run([sys.executable,str(root/'tools/bs_complete.py'),'import',str(cad),str(bs)],check=True,stdout=subprocess.DEVNULL)
 subprocess.run([sys.executable,str(root/'tools/board_analyzer.py'),str(bs),str(root/'compiler_db/component_store.json'),str(root/'compiler_db/ISA8_Ethernet.component_map.json'),str(root/'compiler_db/code_store.json'),str(root/'compiler_db/platform_store.json'),str(ir)],check=True,stdout=subprocess.DEVNULL)
 base=json.load(open(root/'compiler_db/ISA8_Ethernet.board_ir.cp15.json')); changed=json.load(open(ir))
 def has_cs_link(d):
  for l in d.get('interface_links',[]):
   u1=any(e.get('ref')=='U1' and e.get('interface')=='MICROWIRE_3WIRE' and e.get('signal')=='CS' for e in l['endpoints'])
   u3=any(e.get('ref')=='U3' and e.get('interface')=='MICROWIRE_3WIRE' and e.get('signal')=='CS' for e in l['endpoints'])
   if u1 and u3:return True
  return False
 before,after=has_cs_link(base),has_cs_link(changed)
 ok=before and not after
 out={'test':'INTERFACE_LINK_FOLLOWS_TOPOLOGY','mutation':'U3 CS rewired from /EECS to GND','before_u1_u3_microwire_cs_link':before,'after_u1_u3_microwire_cs_link':after,'pass':ok}
 (root/'compiler_db/cp15_mutation_test.json').write_text(json.dumps(out,indent=2)+'\n');print(json.dumps(out,indent=2))
 if not ok: raise SystemExit(1)
