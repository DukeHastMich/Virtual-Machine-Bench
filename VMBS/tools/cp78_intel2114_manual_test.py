#!/usr/bin/env python3
import json
from pathlib import Path
from sram2114_model import Intel2114
root=Path(__file__).resolve().parents[1]
s=json.load(open(root/'compiler_db/component_store.json')); r=next(x for x in s['records'] if x.get('display_name')=='2114')
p={x['number']:x['name'] for x in r['pins']}
assert len(r['pins'])==18 and p[8]=='CS#' and p[10]=='WE#' and p[18]=='VCC'
assert r['properties']['refresh_required'] is False and r['properties']['clock_required'] is False
m=Intel2114()
for a in [0,1,511,1023]:
 for v in range(16):
  assert m.cycle(a,False,False,v) is None
  assert m.cycle(a,False,True)==v
  assert m.cycle(a,True,True) is None
# Four-bit device must discard upper input bits.
m.cycle(7,False,False,0xAB); assert m.cycle(7,False,True)==0xB
print('CP78 Intel 2114 manual-backed SRAM behavior: PASS')
