#!/usr/bin/env python3
import json,re,sys
from pathlib import Path
root=Path(__file__).resolve().parents[1]
a=json.load(open(root/'compiler_db/ISA8_Ethernet.adapters.cp46.json'))
assert a['format']=='VMB-ADAPTER-IR-1' and a['ready']
g={(x['instance_id'],x['interface']):x for x in a['adapter_groups']}
assert set(g)=={('U1','BOOT_ROM'),('U1','MICROWIRE_3WIRE'),('U2','PARALLEL_X8_MEMORY'),('U3','MICROWIRE_3WIRE')}
assert set(g[('U2','PARALLEL_X8_MEMORY')]['signals']) >= {f'A{i}' for i in range(19)}|{f'D{i}' for i in range(8)}|{'CE#','OE#','WE#','VDD','VSS'}
assert set(g[('U3','MICROWIRE_3WIRE')]['signals'])=={'CS','CLK','DI','DO'}
assert len(a['cross_adapter_nets'])==15
src=(root.parent/'Expansion Cards/Memory/Microwire93Lc46c.vb').read_text()
for token in ['Function SetSerialPins','Property SerialDataOut','ClockSerialBit','Case 2 \' READ','Case 1 \' WRITE','Case 3 \' ERASE']:
 assert token in src, token
cs=json.load(open(root/'compiler_db/code_store.json'))
m=next(x for x in cs['modules'] if x['module_id']=='VMB.MEM.MICROWIRE_93LC46C')
b=next(x['binding'] for x in m['composition']['endpoints'] if x['interface']=='MICROWIRE_3WIRE')
assert b['drive_method']=='SetSerialPins' and b['read_property']=='SerialDataOut'
print('CP46 PASS: generic adapter grouping + source-backed 93LC46 x16 serial pin seam')
