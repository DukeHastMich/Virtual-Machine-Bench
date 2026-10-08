#!/usr/bin/env python3
import json,subprocess,sys,tempfile
from pathlib import Path
R=Path(__file__).resolve().parents[1]; vmb=R.parent
plan=R/'compiler_db/ISA8_Ethernet.construction.cp43.json'; static=R/'generated_vmb/cp43_static/GeneratedBoard.Static.vb'
p=json.load(open(plan)); assert p['format']=='VMB-CONSTRUCTION-IR-1' and p['ready']; assert len(p['instances'])==3 and len(p['connections'])==56
assert [x['implementation']['class'] for x in p['instances']]==['Rtl8019As','Sst39Sf040','Microwire93Lc46c']
s=static.read_text(); assert 'New Rtl8019As()' in s and 'New Sst39Sf040()' in s and 'New Microwire93Lc46c()' in s
assert 'AllocateModule' not in s and 'runtime.Link' not in s
assert 'ExecutableWiringComplete As Boolean = False' in s
arch=(R/'BOARD_SCRIPT_ARCHITECTURE_CP43.md').read_text(); assert 'canonical, human-readable, editable board intermediate representation' in arch
sample=(vmb/'Board Scripts/Samples/BOARD-SCRIPT-PROTOTYPE-01.bs').read_text();
for word in ['clock CPUCLK','power VCC5','program U_ROM','constraint ISA_TRACE']: assert word in sample
print('CP43 language-neutral Construction IR + direct static instantiation + BS architecture freeze PASS')
