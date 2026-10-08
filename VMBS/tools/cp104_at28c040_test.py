from pathlib import Path
import json
R=Path(__file__).resolve().parents[1]; s=json.load(open(R/'compiler_db/component_store.json')); c=json.load(open(R/'compiler_db/code_store.json'))
r=next(x for x in s['records'] if x.get('display_name')=='AT28C040'); assert r['properties']['organization']=='524288 x 8'
assert r['physical']['physical_pin_map_complete'] is False
assert any(x['module_id']=='VMB.MEM.EEPROM_AT28C040_X8' for x in c['modules']); print('CP104 AT28C040: PASS')
