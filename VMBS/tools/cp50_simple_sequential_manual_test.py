#!/usr/bin/env python3
from ic_logic_models import DualDff74, Register273, comb
# 138: disabled => all high; enabled selects one active-low output.
o=comb('VMB.LOGIC.74XX138',{'G1':1,'G2A_N':0,'G2B_N':0,'A':1,'B':0,'C':1})
assert o['Y5_N']==0 and sum(v==0 for v in o.values())==1
o=comb('VMB.LOGIC.74XX138',{'G1':0,'G2A_N':0,'G2B_N':0,'A':1,'B':0,'C':1})
assert all(v==1 for v in o.values())
# 157: enable high forces all outputs low; enable low selects A/B by S.
assert comb('VMB.LOGIC.74XX157',{'E_N':1,'S':0})['1Y']==0
assert comb('VMB.LOGIC.74XX157',{'E_N':0,'S':0,'1A':1,'1B':0})['1Y']==1
assert comb('VMB.LOGIC.74XX157',{'E_N':0,'S':1,'1A':1,'1B':0})['1Y']==0
# 74A: async clear/preset and forbidden both-low output state.
d=DualDff74(); assert d.step({'1PRE_N':1,'1CLR_N':0})['1Q']==0
assert d.step({'1PRE_N':0,'1CLR_N':1})['1Q']==1
o=d.step({'1PRE_N':0,'1CLR_N':0}); assert o['1Q']==1 and o['1Q_N']==1
# 273: direct clear and positive-edge capture.
r=Register273(); assert r.step({'CLR_N':0,'CLK':0})['Q1']==0
r.step({'CLR_N':1,'CLK':0,'D1':1}); assert r.step({'CLR_N':1,'CLK':1,'D1':1})['Q1']==1
print('CP50 PASS: 74LS138/157/74A/273 behavior checked; 74LS74A forbidden PRE#=CLR#=LOW state corrected to Q=Qbar=HIGH.')
