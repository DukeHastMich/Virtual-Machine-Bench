#!/usr/bin/env python3
import json
from pathlib import Path
from sram2101a_model import Intel2101A
r=Path(__file__).resolve().parents[1];s=json.load(open(r/'compiler_db/component_store.json'));a=next(z for z in s['records'] if z.get('display_name')=='2101A');b=next(z for z in s['records'] if z.get('display_name')=='5101')
assert [(p['number'],p['name'],p['direction']) for p in a['pins']]==[(p['number'],p['name'],p['direction']) for p in b['pins']]
assert a['part_id']!=b['part_id'] and a['vmb_binding']['module_id']!=b['vmb_binding']['module_id'] and a['properties']['technology']!=b['properties']['technology']
m=Intel2101A();m.cycle(1,False,True,False,False,7);assert m.cycle(1,False,True,True,False)==7
print('CP83 Intel 2101A identity + behavior: PASS')
