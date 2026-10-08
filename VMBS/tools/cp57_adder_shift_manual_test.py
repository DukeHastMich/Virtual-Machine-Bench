#!/usr/bin/env python3
from ic_logic_models import comb,Shift95B,SUPPORTED
for n in ('83A','95B'):assert 'VMB.LOGIC.74XX'+n in SUPPORTED
# LS83A must be functionally identical to LS283 across complete input space.
for a in range(16):
 for b in range(16):
  for c in (0,1):
   p={**{f'A{i}':(a>>i)&1 for i in range(4)},**{f'B{i}':(b>>i)&1 for i in range(4)},'C0':c}
   assert comb('VMB.LOGIC.74XX83A',p)==comb('VMB.LOGIC.74XX283',p)
# LS95B parallel load on falling CLK2, then right shift on falling CLK1.
x=Shift95B();x.step({'MODE':1,'CLK2':1,'A':1,'B':0,'C':1,'D':0});o=x.step({'MODE':1,'CLK2':0,'A':1,'B':0,'C':1,'D':0});assert [o[k] for k in ('QA','QB','QC','QD')]==[1,0,1,0]
x.step({'MODE':0,'CLK1':1,'SER':0});o=x.step({'MODE':0,'CLK1':0,'SER':0});assert [o[k] for k in ('QA','QB','QC','QD')]==[0,1,0,1]
print('CP57 adder/parallel-access-shift manual-backed tests: PASS')
