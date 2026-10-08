#!/usr/bin/env python3
import json
from pathlib import Path
root=Path(__file__).resolve().parents[1]
vmb=root.parent
src=(vmb/'Expansion Cards/Network Cards/RTL8019AS/Rtl8019As.vb').read_text()
for token in ['Event MicrowirePinsChanged','Property EepromChipSelect','Property EepromClock','Property EepromDataIn','Sub SetEepromDataOut','Function Read9346Control','Sub Write9346Control','Case 3']:
 assert token in src, token
store=json.load(open(root/'compiler_db/code_store.json'))
m=next(x for x in store['modules'] if x['module_id']=='VMB.NET.RTL8019AS')
b=next(x['binding'] for x in m['composition']['endpoints'] if x['interface']=='MICROWIRE_3WIRE')
assert b['drive_event']=='MicrowirePinsChanged'
assert b['sample_method']=='SetEepromDataOut'
assert b['cs_property']=='EepromChipSelect' and b['clock_property']=='EepromClock' and b['di_property']=='EepromDataIn'
manual=json.load(open(root/'compiler_db/manual_sources_cp47.json'))
subjects={x['subject'] for x in manual['sources']}
assert {'RTL8019AS','93LC46C','SST39SF040','Spartan-3A'} <= subjects
notes=(root/'MANUAL_EVIDENCE_CP47.md').read_text()
for fact in ['BCSB','EECS','EESK','EEDI','EEDO','EWEN','DS20001749L','DS20005022']:
 assert fact in notes
print('CP47 PASS: manual evidence indexed + RTL8019AS source-backed Microwire controller seam')
