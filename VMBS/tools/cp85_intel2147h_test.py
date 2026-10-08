#!/usr/bin/env python3
import json
from pathlib import Path
from sram2147h_model import Intel2147H
r=Path(__file__).resolve().parents[1];s=json.load(open(r/'compiler_db/component_store.json'));x=next(z for z in s['records'] if z.get('display_name')=='2147H')
assert x['properties']['organization']=='4096 x 1' and len(x['pins'])==18 and x['properties']['automatic_cs_power_down']
m=Intel2147H();assert m.standby;m.cycle(4095,False,False,1);assert not m.standby and m.cycle(4095,False,True)==1;assert m.cycle(1,True,True) is None and m.standby
print('CP85 Intel 2147H manual + behavior: PASS')
