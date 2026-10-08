#!/usr/bin/env python3
import json,hashlib
from pathlib import Path
r=Path(__file__).resolve().parents[1]
lib=r/'Library/CT2960/Images'; m=json.load(open(lib/'manifest.json')); assert len(m['files'])==6
for x in m['files']:
 p=lib/x['file'];assert p.exists() and hashlib.sha256(p.read_bytes()).hexdigest()==x['sha256']
s=json.load(open(r/'compiler_db/component_store.json'));t=next(x for x in s['records'] if x.get('display_name')=='TEA2025B')
assert len(t['pins'])==16 and t['properties']['supply_voltage_v_absolute_max']==15.0 and t['vmb_binding'] is None
assert (r/'Component Store/Creative/CT2505-TDQ2/provenance.json').exists()
ct=(r/'Component Store/Creative/CT2505-TDQ2/README.md').read_text();assert 'TITS UP' in ct and 'No primary Creative CT2505 pin-level datasheet' in ct
print('CP86 CT2960 Library + TEA2025B evidence boundary: PASS')
