#!/usr/bin/env python3
import json
from pathlib import Path
from sram2142_model import Intel2142
r=Path(__file__).resolve().parents[1];s=json.load(open(r/'compiler_db/component_store.json'));x=next(z for z in s['records'] if z.get('display_name')=='2142')
assert x['properties']['organization']=='1024 x 4' and len(x['pins'])==20
m=Intel2142();m.cycle(99,False,True,False,False,0xD);assert m.cycle(99,False,True,True,False)==0xD
assert m.cycle(99,True,True,True,False) is None and m.cycle(99,False,False,True,False) is None and m.cycle(99,False,True,True,True) is None
print('CP84 Intel 2142 manual + behavior: PASS')
