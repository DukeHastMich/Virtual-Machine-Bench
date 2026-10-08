#!/usr/bin/env python3
import json
from pathlib import Path
from sram2102a_model import Intel2102A
r=Path(__file__).resolve().parents[1]; s=json.load(open(r/'compiler_db/component_store.json'))
x=next(z for z in s['records'] if z.get('display_name')=='2102A')
assert x['properties']['organization']=='1024 x 1' and len(x['pins'])==16
p={q['number']:q['name'] for q in x['pins']}; assert p=={1:'A6',2:'A5',3:'R/W',4:'A1',5:'A2',6:'A3',7:'A4',8:'A0',9:'GND',10:'VCC',11:'DIN',12:'DOUT',13:'CE',14:'A9',15:'A8',16:'A7'}
m=Intel2102A(); assert m.cycle(17,True,True) is None; m.cycle(17,False,False,1); assert m.cycle(17,False,True)==1; m.cycle(17,False,False,0); assert m.cycle(17,False,True)==0
print('CP81 Intel 2102A manual + behavior: PASS')
