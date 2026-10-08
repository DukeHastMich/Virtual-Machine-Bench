#!/usr/bin/env python3
from ic_logic_models import comb, Latch373, Register374, Counter161, Counter393

# LS688 identity comparator: disabled => inactive HIGH; enabled equal => active LOW.
p={"G_N":0}; p.update({f"P{i}":(i&1) for i in range(8)}); p.update({f"Q{i}":(i&1) for i in range(8)})
assert comb('VMB.LOGIC.74XX688',p)['P_EQ_Q_N']==0
p['Q7'] ^= 1; assert comb('VMB.LOGIC.74XX688',p)['P_EQ_Q_N']==1
p['G_N']=1; assert comb('VMB.LOGIC.74XX688',p)['P_EQ_Q_N']==1

# LS244: each active-low OE controls four non-inverting 3-state outputs.
o=comb('VMB.LOGIC.74XX244',{'OE1_N':0,'OE2_N':1,'1A1':1,'1A2':0,'2A1':1})
assert o['1Y1']==1 and o['1Y2']==0 and o['2Y1'] is None

# LS245: OE isolates; DIR chooses which bus drives the other.
o=comb('VMB.LOGIC.74XX245',{'OE_N':0,'DIR':1,'A1':1,'B1':0}); assert o['B1']==1 and o['A1'] is None
o=comb('VMB.LOGIC.74XX245',{'OE_N':0,'DIR':0,'A1':1,'B1':0}); assert o['A1']==0 and o['B1'] is None
o=comb('VMB.LOGIC.74XX245',{'OE_N':1,'DIR':1,'A1':1,'B1':0}); assert o['A1'] is None and o['B1'] is None

# LS373: transparent while LE high; OE only controls pins, not stored state.
l=Latch373(); assert l.step({'LE':1,'OE_N':0,'D1':1})['Q1']==1
assert l.step({'LE':1,'OE_N':1,'D1':0})['Q1'] is None
assert l.step({'LE':0,'OE_N':0,'D1':1})['Q1']==0

# LS374: only positive clock edge captures; OE does not stop internal capture.
r=Register374(); r.step({'CLK':0,'OE_N':1,'D1':1}); assert r.step({'CLK':1,'OE_N':1,'D1':1})['Q1'] is None
assert r.step({'CLK':1,'OE_N':0,'D1':0})['Q1']==1
r.step({'CLK':0,'OE_N':0,'D1':0}); assert r.step({'CLK':1,'OE_N':0,'D1':0})['Q1']==0

# LS161A asynchronous clear, synchronous load/count.
c=Counter161(synchronous_clear=False)
c.step({'CLK':0,'CLR_N':1,'LOAD_N':0,'A':1,'B':0,'C':1,'D':0}); assert c.step({'CLK':1,'CLR_N':1,'LOAD_N':0,'A':1,'B':0,'C':1,'D':0})['QA']==1
assert c.step({'CLK':1,'CLR_N':0})['QA']==0  # no clock edge needed
c.step({'CLK':0,'CLR_N':1,'LOAD_N':1,'ENP':1,'ENT':1}); assert c.step({'CLK':1,'CLR_N':1,'LOAD_N':1,'ENP':1,'ENT':1})['QA']==1

# LS163A clear is synchronous: LOW clear alone does not clear until rising edge.
s=Counter161(synchronous_clear=True)
s.step({'CLK':0,'CLR_N':1,'LOAD_N':0,'A':1}); s.step({'CLK':1,'CLR_N':1,'LOAD_N':0,'A':1})
assert s.step({'CLK':1,'CLR_N':0})['QA']==1
s.step({'CLK':0,'CLR_N':0}); assert s.step({'CLK':1,'CLR_N':0})['QA']==0

# LS393: independent direct clears and ripple count transition model.
k=Counter393(); k.step({'1CLR':0,'1CLK':1}); assert k.step({'1CLR':0,'1CLK':0})['1QA']==1
assert k.step({'1CLR':1,'1CLK':0})['1QA']==0
print('CP51 PASS: 688/244/245/373/374/161A/163A/393 manual-backed behavior checked.')
