#!/usr/bin/env python3
from ic_logic_models import comb,Counter192193,Shift194,SUPPORTED
for n in ('192','193','194','280','283'): assert 'VMB.LOGIC.74XX'+n in SUPPORTED
# 280 exhaustive parity relation
for v in range(512):
 p={f'I{i}':(v>>i)&1 for i in range(9)};o=comb('VMB.LOGIC.74XX280',p);assert o['EVEN']==(v.bit_count()&1) and o['ODD']==1-o['EVEN']
# 283 exhaustive arithmetic
for a in range(16):
 for q in range(16):
  for c in (0,1):
   p={**{f'A{i}':(a>>i)&1 for i in range(4)},**{f'B{i}':(q>>i)&1 for i in range(4)},'C0':c};o=comb('VMB.LOGIC.74XX283',p);s=sum(o[f'S{i}']<<i for i in range(4))|(o['C4']<<4);assert s==a+q+c
# 192 BCD rollover and async clear/load
c=Counter192193(True);c.step({'LOAD_N':0,'A':1,'D':1,'UP':1,'DOWN':1});c.step({'LOAD_N':1,'UP':0,'DOWN':1});o=c.step({'LOAD_N':1,'UP':1,'DOWN':1});assert all(o[n]==0 for n in ('QA','QB','QC','QD'))
c.step({'CLR':1,'UP':1,'DOWN':1});assert c.value==0
# 193 binary underflow
c=Counter192193(False);c.step({'LOAD_N':0,'UP':1,'DOWN':1});c.step({'LOAD_N':1,'UP':1,'DOWN':0});o=c.step({'LOAD_N':1,'UP':1,'DOWN':1});assert c.value==15
# 194 load, hold, right, left, clear
r=Shift194();r.step({'CLR_N':1,'CLK':0,'S1':1,'S0':1,'A':1,'B':0,'C':1,'D':0});o=r.step({'CLR_N':1,'CLK':1,'S1':1,'S0':1,'A':1,'B':0,'C':1,'D':0});assert [o[x] for x in ('QA','QB','QC','QD')]==[1,0,1,0]
r.step({'CLR_N':1,'CLK':0,'S1':0,'S0':1,'SR':0});o=r.step({'CLR_N':1,'CLK':1,'S1':0,'S0':1,'SR':0});assert [o[x] for x in ('QA','QB','QC','QD')]==[0,1,0,1]
r.step({'CLR_N':0,'CLK':1});assert r.q==[0]*4
print('CP55 arithmetic/parity/dual-clock counter/universal shift manual-backed tests: PASS')
