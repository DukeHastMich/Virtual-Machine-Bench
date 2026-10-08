#!/usr/bin/env python3
from ic_logic_models import comb,Register175,Counter19x,SUPPORTED
for m in ('42','85','175','190','191'): assert f'VMB.LOGIC.74XX{m}' in SUPPORTED
x=comb('VMB.LOGIC.74XX42',{'A':1,'B':0,'C':1,'D':0}); assert x['Y5_N']==0 and sum(v==0 for v in x.values())==1
x=comb('VMB.LOGIC.74XX42',{'A':0,'B':1,'C':0,'D':1}); assert all(v==1 for v in x.values()) # invalid BCD 10
x=comb('VMB.LOGIC.74XX85',{'A0':1,'B0':0}); assert x=={'A_GT_B':1,'A_EQ_B':0,'A_LT_B':0}
x=comb('VMB.LOGIC.74XX85',{'I_EQ':1}); assert x['A_EQ_B']==1
r=Register175();r.step({'CLR_N':0,'CLK':0});r.step({'CLR_N':1,'CLK':0});x=r.step({'CLR_N':1,'CLK':1,'D2':1});assert x['Q2']==1 and x['Q2_N']==0
c=Counter19x(False);c.step({'LOAD_N':0,'A':1,'B':1,'C':1,'D':1,'CLK':0});c.step({'LOAD_N':1,'CTEN_N':0,'D_U':0,'CLK':0});x=c.step({'LOAD_N':1,'CTEN_N':0,'D_U':0,'CLK':1});assert x['QA']==0 and x['QB']==0 # 15->0
c=Counter19x(True);c.step({'LOAD_N':0,'A':1,'D':1,'CLK':0});c.step({'LOAD_N':1,'CTEN_N':0,'D_U':0,'CLK':0});x=c.step({'LOAD_N':1,'CTEN_N':0,'D_U':0,'CLK':1});assert all(x[n]==0 for n in ('QA','QB','QC','QD')) # 9->0
print('CP54 decoder/comparator/register/counter manual-backed tests: PASS')
