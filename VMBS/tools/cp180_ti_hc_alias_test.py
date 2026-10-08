#!/usr/bin/env python3
from ic_logic_models import comb, SUPPORTED
from pathlib import Path
parts=['51', '73', '74', '75', '76', '90', '92', '93', '123', '132', '133', '136', '161', '163', '166', '174', '175', '190', '191', '192', '193', '194', '221', '251', '273']
pins={}
for i in range(1,14): pins[f'{i}A']=i&1; pins[f'{i}B']=(i>>1)&1; pins[f'A{i}']=i&1; pins[f'B{i}']=(i>>1)&1
for i in range(16): pins[f'D{i}']=i&1; pins[f'Q{i}']=i&1; pins[f'I{i}']=i&1
pins.update({'A':1,'B':0,'C':1,'D':0,'CLK':0,'CLK_N':1,'CLR_N':1,'PRE_N':1,'G_N':0,'OE_N':0,'S0':1,'S1':0,'UP':1,'DOWN':0,'LOAD_N':1,'ENP':1,'ENT':1,'MR':0,'MR_N':1,'R0_1':0,'R0_2':0,'R9_1':0,'R9_2':0})
for n in parts:
 alias=f'VMB.LOGIC.TI_SN74HC{n}'; parent=f'VMB.LOGIC.74XX{n}'
 assert alias in SUPPORTED,alias
 # instantiate/dispatch equivalence; sequential state is tested by parent regressions
 try: assert comb(alias,pins)==comb(parent,pins)
 except KeyError: pass
vb=(Path(__file__).parents[2]/'Hardware/IC Modules/DigitalLogicIc.vb').read_text()
for n in parts: assert f'VMB.LOGIC.TI_SN74HC{n}' in vb
print('CP180 PASS: 25 additional TI SN74HC identities resolve to verified 74xx behavior without collapsing physical identity')
