#!/usr/bin/env python3
import json
from pathlib import Path
root=Path(__file__).resolve().parents[1]
s=json.load(open(root/'compiler_db/component_store.json'))
a=next(x for x in s['records'] if x['display_name']=='8042AH')
b=next(x for x in s['records'] if x['display_name']=='8742AH')
assert a['part_id'] != b['part_id']
# Runtime board-facing DIP pin map is the same, but reverse-build programming identity is not.
assert [(x['number'],x['name'],x['direction']) for x in a['pins']] == [(x['number'],x['name'],x['direction']) for x in b['pins']]
assert a['programmed_state']['physical_programming']=='factory_mask_rom'
assert b['programmed_state']['physical_programming']=='external_device_programmer'
assert b['programmed_state']['reverse_delivery']=='required'
assert b['vmb_binding'] is None
print('CP76 UPI-42 identity/payload boundary: PASS')
