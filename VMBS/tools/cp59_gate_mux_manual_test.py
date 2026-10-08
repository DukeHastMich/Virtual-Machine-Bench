#!/usr/bin/env python3
from itertools import product
from ic_logic_models import comb,SUPPORTED
for n in ('01','132','133','136','251'):assert 'VMB.LOGIC.74XX'+n in SUPPORTED
for a,b in product((0,1),repeat=2):
 assert comb('VMB.LOGIC.74XX01',{'1A':a,'1B':b})['1Y']==(0 if a and b else None)
 assert comb('VMB.LOGIC.74XX132',{'1A':a,'1B':b})['1Y']==int(not(a and b))
 assert comb('VMB.LOGIC.74XX136',{'1A':a,'1B':b})['1Y']==(0 if not(a^b) else None)
for vals in product((0,1),repeat=13):
 p={chr(65+i):v for i,v in enumerate(vals)};assert comb('VMB.LOGIC.74XX133',p)['Y']==int(not all(vals))
for sel in range(8):
 p={'G_N':0,'A':sel&1,'B':(sel>>1)&1,'C':(sel>>2)&1,**{f'D{i}':i&1 for i in range(8)}};o=comb('VMB.LOGIC.74XX251',p);assert o['Y']==(sel&1) and o['W']==1-(sel&1)
assert comb('VMB.LOGIC.74XX251',{'G_N':1})=={'Y':None,'W':None}
print('CP59 simple gate / tri-state mux manual-backed tests: PASS')
