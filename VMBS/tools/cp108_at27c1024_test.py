from pathlib import Path
import json
R=Path(__file__).resolve().parents[1]; s=json.load(open(R/'compiler_db/component_store.json')); c=json.load(open(R/'compiler_db/code_store.json'))
r=next(x for x in s['records'] if x.get('display_name')=='AT27C1024'); assert r['properties']['organization']=='65536 x 16'
assert r['physical']['physical_pin_map_complete'] is False
assert any(x['module_id']=='VMB.MEM.EPROM_AT27C1024_X16' for x in c['modules']); print('CP108 AT27C1024: PASS')
