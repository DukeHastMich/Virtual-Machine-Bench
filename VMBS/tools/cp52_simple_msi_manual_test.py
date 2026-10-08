#!/usr/bin/env python3
from ic_logic_models import comb,Register174,Shift166,SUPPORTED
for m in ('139','148','151','153','166','174'): assert f'VMB.LOGIC.74XX{m}' in SUPPORTED
# 139: section enabled, select BA=10 -> Y2 low
assert comb('VMB.LOGIC.74XX139',{'1G_N':0,'1A':0,'1B':1})['1Y2_N']==0
# 151 select 5, enabled; complementary outputs
x=comb('VMB.LOGIC.74XX151',{'G_N':0,'A':1,'B':0,'C':1,'D5':1}); assert x['Y']==1 and x['W']==0
assert comb('VMB.LOGIC.74XX151',{'G_N':1})['Y']==0
# 153 independent strobes
x=comb('VMB.LOGIC.74XX153',{'A':1,'B':0,'1G_N':0,'1C1':1,'2G_N':1}); assert x['1Y']==1 and x['2Y']==0
# 148: I6 outranks I2, active-low binary output for 6 = 110 => complements 001
x=comb('VMB.LOGIC.74XX148',{'EI_N':0,'I2_N':0,'I6_N':0}); assert (x['A2_N'],x['A1_N'],x['A0_N'])==(0,0,1) and x['GS_N']==0
r=Register174(); r.step({'CLR_N':0,'CLK':0}); r.step({'CLR_N':1,'CLK':0}); x=r.step({'CLR_N':1,'CLK':1,'D0':1}); assert x['Q0']==1
s=Shift166(); s.step({'CLR_N':0,'CLK':0}); s.step({'CLR_N':1,'CLK':0,'CLK_INH':0,'SH_LD_N':0}); x=s.step({'CLR_N':1,'CLK':1,'CLK_INH':0,'SH_LD_N':0,'H':1}); assert x['QH']==1
print('CP52 simple MSI manual-backed tests: PASS')
