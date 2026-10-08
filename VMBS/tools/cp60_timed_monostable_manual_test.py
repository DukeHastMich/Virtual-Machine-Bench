#!/usr/bin/env python3
from ic_logic_models import Monostable123,Monostable221,SUPPORTED
assert {'VMB.LOGIC.74XX123','VMB.LOGIC.74XX221'} <= SUPPORTED
idle={'1A':1,'1B':1,'1CLR_N':1,'2A':1,'2B':0,'2CLR_N':1}
def trig(m,t):
 m.step_at(t,idle); p=dict(idle);p['1A']=0;return m.step_at(t+1,p)
# 123 retriggers and extends the pulse.
a=Monostable123(100); assert trig(a,0)['1Q']==1
p=dict(idle);p['1A']=0;a.step_at(50,idle); assert a.step_at(51,p)['1Q']==1
assert a.step_at(120,p)['1Q']==1 and a.step_at(152,p)['1Q']==0
# 221 ignores retriggers while active.
b=Monostable221(100); assert trig(b,0)['1Q']==1
b.step_at(50,idle); assert b.step_at(51,p)['1Q']==1
assert b.step_at(102,p)['1Q']==0
# overriding clear terminates either immediately.
c=Monostable123(100);trig(c,0); q=dict(idle);q['1CLR_N']=0;assert c.step_at(10,q)['1Q']==0
# positive B transition with A held low also triggers.
d=Monostable123(100); low=dict(idle);low['1A']=0;low['1B']=0;d.step_at(0,low);low['1B']=1;assert d.step_at(1,low)['1Q']==1
print('CP60 timed monostable manual-backed tests: PASS')
