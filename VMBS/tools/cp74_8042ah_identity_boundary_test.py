#!/usr/bin/env python3
import json
from pathlib import Path
root=Path(__file__).resolve().parents[1]
store=json.load(open(root/'compiler_db/component_store.json'))['records']
r=next(r for r in store if r.get('manufacturer_part_number')=='8042AH')
assert len(r['pins'])==40
assert r['properties']['program_memory']=='2048 x 8 mask ROM'
assert r['properties']['data_ram']=='256 x 8'
assert r['properties']['programmable_io_pins']==18
assert r['vmb_binding'] is None and r['implementation']['vmb_binding'] is None
assert any(x.get('kind')=='FIRMWARE_PERSONALITY' and x.get('required') for x in r['support_requirements'])
code=json.load(open(root/'compiler_db/code_store.json'))['modules']
m=next(m for m in code if m['module_id']=='VMB.AT.KBC8042')
assert m['kind']=='keyboard_controller_firmware_personality'
assert m['implementation']['physical_identity_binding'] is None
assert m['implementation']['exact_mask_rom_payload_known'] is False
assert 'not the generic Intel 8042AH' in m['implementation']['description']
print('CP74 8042AH silicon vs AT keyboard-firmware personality boundary: PASS')
