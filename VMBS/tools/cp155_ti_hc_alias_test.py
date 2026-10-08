#!/usr/bin/env python3
from ic_logic_models import comb, SUPPORTED
from pathlib import Path
parts=['00','02','04','08','10','11','14','20','21','27','30','32','42','85','86','125','126','138','139','148','151','153','157','244','245']
# Broad pin stimulus deliberately includes names used across gate/decoder/mux/bus families.
pins={}
for i in range(1,9):
    pins[f'{i}A']=i&1; pins[f'{i}B']=(i>>1)&1; pins[f'A{i}']=i&1; pins[f'B{i}']=(i>>1)&1
for i in range(8): pins[f'I{i}_N']=1; pins[f'D{i}']=i&1; pins[f'P{i}']=i&1; pins[f'Q{i}']=i&1
pins.update({'A':1,'B':0,'C':1,'D':0,'G1':1,'G2A_N':0,'G2B_N':0,'G_N':0,'EI_N':0,'S':1,'E_N':0,'OE_N':0,'OE1_N':0,'OE2_N':0,'DIR':1,'I_GT':0,'I_EQ':1,'I_LT':0})
for sec in (1,2):
    pins[f'{sec}G_N']=0
    for j in range(4): pins[f'{sec}C{j}']=j&1
for g in range(1,4):
    for c in 'ABCDEFGH': pins[f'{g}{c}']=(g+ord(c))&1
for n in parts:
    alias=f'VMB.LOGIC.TI_SN74HC{n}'; parent=f'VMB.LOGIC.74XX{n}'
    assert alias in SUPPORTED, alias
    assert comb(alias,pins)==comb(parent,pins),(alias,comb(alias,pins),comb(parent,pins))
vb=(Path(__file__).parents[2]/'Hardware/IC Modules/DigitalLogicIc.vb').read_text()
for n in parts: assert f'VMB.LOGIC.TI_SN74HC{n}' in vb
print('CP155 PASS: 25 TI SN74HC identities preserve exact module identity and delegate only verified digital behavior')
