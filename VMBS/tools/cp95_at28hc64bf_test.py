from pathlib import Path
import json
R=Path(__file__).resolve().parents[1];s=json.load(open(R/'compiler_db/component_store.json'));r=next(x for x in s['records'] if x.get('display_name')=='AT28HC64BF');assert r['timing']['AT28HC64BF-12']['address_to_output_ns_max']==120 and r['timing']['AT28HC64BF-12']['write_cycle_ms_max']==2 and 'TSOP-28' not in r['physical']['alternate_packages'];print('CP95 AT28HC64BF fast-write EEPROM identity: PASS')
