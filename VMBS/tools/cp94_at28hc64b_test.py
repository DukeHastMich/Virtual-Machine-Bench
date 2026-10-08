from pathlib import Path
import json
R=Path(__file__).resolve().parents[1];s=json.load(open(R/'compiler_db/component_store.json'));c=json.load(open(R/'compiler_db/code_store.json'));r=next(x for x in s['records'] if x.get('display_name')=='AT28HC64B');assert r['timing']['AT28HC64B']['address_to_output_ns_max']==70 and r['properties']['page_bytes']==64;assert any(x['module_id']=='VMB.MEM.EEPROM_AT28HC64B_X8' for x in c['modules']);print('CP94 AT28HC64B high-speed EEPROM identity: PASS')
