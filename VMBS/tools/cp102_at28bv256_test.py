from pathlib import Path
import json
R=Path(__file__).resolve().parents[1]
s=json.load(open(R/'compiler_db/component_store.json')); c=json.load(open(R/'compiler_db/code_store.json'))
r=next(x for x in s['records'] if x.get('display_name')=='AT28BV256')
assert r['implementation']['vmb_binding']['module_id']=='VMB.MEM.EEPROM_AT28BV256_X8'
assert any(x['module_id']=='VMB.MEM.EEPROM_AT28BV256_X8' and x['implementation']['compile_allowed'] for x in c['modules'])
print('CP102 AT28BV256: PASS')
