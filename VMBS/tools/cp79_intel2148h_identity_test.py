#!/usr/bin/env python3
import json
from pathlib import Path
from sram2148h_model import Intel2148H
root=Path(__file__).resolve().parents[1];s=json.load(open(root/'compiler_db/component_store.json'))
a=next(x for x in s['records'] if x.get('display_name')=='2114');b=next(x for x in s['records'] if x.get('display_name')=='2148H')
assert [(p['number'],p['name'],p['direction']) for p in a['pins']]==[(p['number'],p['name'],p['direction']) for p in b['pins']]
# Keep identity/module distinct: timing/power guarantees are not symmetric even though board wiring and guest-visible storage behavior match.
assert a['part_id']!=b['part_id'] and a['vmb_binding']['module_id']!=b['vmb_binding']['module_id']
assert b['properties']['automatic_cs_power_down'] is True and b['timing']['access_time_ns_by_variant']['2148H-2']==45
m=Intel2148H(); assert m.standby is True
m.cycle(3,False,False,9); assert m.standby is False and m.cycle(3,False,True)==9
assert m.cycle(3,True,True) is None and m.standby is True
print('CP79 Intel 2148H identity + behavior: PASS')
