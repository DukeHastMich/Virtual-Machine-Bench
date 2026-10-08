#!/usr/bin/env python3
import json,subprocess,sys
from pathlib import Path
root=Path(__file__).resolve().parents[1]
p=subprocess.run([sys.executable,str(root/'tools/programmed_state_validate.py'),str(root/'compiler_db/component_store.json'),str(root/'compiler_db/code_store.json')],capture_output=True,text=True)
assert p.returncode==0,p.stdout+p.stderr
r=json.loads(p.stdout)
assert {'SST39SF040','93LC46C','AT27C256R','8042AH'} <= set(r['audited'])
s=json.load(open(root/'compiler_db/component_store.json'))
e=next(x for x in s['records'] if x['display_name']=='AT27C256R')
assert len(e['pins'])==28 and e['programmed_state']['capacity_bytes']==32768
assert e['vmb_binding']['module_id']=='VMB.MEM.EPROM_27256_X8'
# Exact 28-PDIP interface anchors from DS20006674A.
pins={x['number']:x['name'] for x in e['pins']}
assert pins[1]=='VPP' and pins[14]=='GND' and pins[20]=='CE#' and pins[22]=='OE#' and pins[28]=='VCC'
fs=json.load(open(root/'compiler_db/firmware_store.json'))
assert 'reverse_delivery' in fs['policy'] and 'bypass_rule' in fs['policy']
print('CP75 programmed-state authenticity: PASS')
