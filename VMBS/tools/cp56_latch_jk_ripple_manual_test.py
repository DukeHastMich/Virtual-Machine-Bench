#!/usr/bin/env python3
from ic_logic_models import DualJk73,DualJk76,Latch75,Ripple90x,SUPPORTED
for n in ('73','75','76','90','92','93'):assert 'VMB.LOGIC.74XX'+n in SUPPORTED
# 73 negative edge: J set, K reset, J=K toggle; async clear
x=DualJk73();x.step({'1CLK':1,'1J':1});o=x.step({'1CLK':0,'1J':1});assert o['1Q']==1
x.step({'1CLK':1,'1K':1});o=x.step({'1CLK':0,'1K':1});assert o['1Q']==0
x.step({'1CLK':1,'1J':1,'1K':1});o=x.step({'1CLK':0,'1J':1,'1K':1});assert o['1Q']==1
assert x.step({'1CLR_N':0})['1Q']==0
# 76 async preset/clear and forbidden both-low output state
x=DualJk76();assert x.step({'1PRE_N':0,'1CLR_N':1})['1Q']==1
assert x.step({'1PRE_N':1,'1CLR_N':0})['1Q']==0
o=x.step({'1PRE_N':0,'1CLR_N':0});assert o['1Q']==o['1Q_N']==1
# 75 transparency and hold
x=Latch75();o=x.step({'C1':1,'D0':1,'D1':0});assert o['Q0']==1 and o['Q1']==0
o=x.step({'C1':0,'D0':0,'D1':1});assert o['Q0']==1 and o['Q1']==0
# independent ripple sections; verify documented section moduli
for kind,mod in ((90,5),(92,6),(93,8)):
 x=Ripple90x(kind)
 for _ in range(mod):x.step({'CKB':1});o=x.step({'CKB':0})
 assert x.section==0,(kind,x.section)
 x.step({'CKA':1});o=x.step({'CKA':0});assert o['QA']==1
 o=x.step({'R0_1':1,'R0_2':1});assert [o[k] for k in ('QA','QB','QC','QD')]==[0,0,0,0]
# LS90 gated set-nine => 1001 when QA is /2 stage and section=4
x=Ripple90x(90);o=x.step({'R9_1':1,'R9_2':1});assert [o[k] for k in ('QA','QB','QC','QD')]==[1,0,0,1]
print('CP56 latch/J-K/ripple-counter manual-backed tests: PASS')
