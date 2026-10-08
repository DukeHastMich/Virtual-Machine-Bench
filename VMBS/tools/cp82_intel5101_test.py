#!/usr/bin/env python3
import json
from pathlib import Path
from sram5101_model import Intel5101
r=Path(__file__).resolve().parents[1];s=json.load(open(r/'compiler_db/component_store.json'));x=next(z for z in s['records'] if z.get('display_name')=='5101')
assert x['properties']['organization']=='256 x 4' and len(x['pins'])==22
p={q['number']:q['name'] for q in x['pins']}; assert p[17]=='CE2' and p[19]=='CE1' and p[18]=='OD' and p[20]=='R/W'
m=Intel5101();m.cycle(5,False,True,False,False,0xA);assert m.cycle(5,False,True,True,False)==0xA
assert m.cycle(5,True,True,True,False) is None and m.cycle(5,False,False,True,False) is None and m.cycle(5,False,True,True,True) is None
print('CP82 Intel 5101 manual + behavior: PASS')
