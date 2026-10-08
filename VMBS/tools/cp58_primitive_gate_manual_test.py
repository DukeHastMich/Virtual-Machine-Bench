#!/usr/bin/env python3
from itertools import product
from ic_logic_models import comb,SUPPORTED
mods=('03','05','06','10','11','14','20','21','27','30')
for n in mods: assert 'VMB.LOGIC.74XX'+n in SUPPORTED
# open collector: asserted low or released, never actively high
for n in ('05','06'):
 m='VMB.LOGIC.74XX'+n
 assert comb(m,{'1A':1})['1Y']==0 and comb(m,{'1A':0})['1Y'] is None
m='VMB.LOGIC.74XX03'
for a,b in product((0,1),repeat=2):
 y=comb(m,{'1A':a,'1B':b})['1Y']; assert y==(0 if a and b else None)
# Schmitt part at digital resolution is an inverter; hysteresis itself is deliberately not fabricated.
for a in (0,1): assert comb('VMB.LOGIC.74XX14',{'1A':a})['1Y']==1-a
spec={'10':(3,'NAND'),'11':(3,'AND'),'20':(4,'NAND'),'21':(4,'AND'),'27':(3,'NOR'),'30':(8,'NAND')}
for n,(width,kind) in spec.items():
 for vals in product((0,1),repeat=width):
  pins={f'1{chr(65+i)}':v for i,v in enumerate(vals)}; y=comb('VMB.LOGIC.74XX'+n,pins)['1Y']
  base=all(vals) if kind in ('NAND','AND') else any(vals); expected=int(not base) if kind in ('NAND','NOR') else int(base)
  assert y==expected,(n,vals,y,expected)
print('CP58 primitive/open-collector/Schmitt gate manual-backed tests: PASS')
