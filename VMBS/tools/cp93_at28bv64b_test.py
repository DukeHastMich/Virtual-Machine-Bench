from pathlib import Path
import json
R=Path(__file__).resolve().parents[1];s=json.load(open(R/'compiler_db/component_store.json'));c=json.load(open(R/'compiler_db/code_store.json'));r=next(x for x in s['records'] if x.get('display_name')=='AT28BV64B');assert r['timing']['AT28BV64B']['address_to_output_ns_max']==200 and r['electrical']['vcc_v']['minimum']==2.7;assert any(x['module_id']=='VMB.MEM.EEPROM_AT28BV64B_X8' for x in c['modules']);print('CP93 AT28BV64B battery-voltage EEPROM identity: PASS')
