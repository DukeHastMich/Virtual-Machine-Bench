from pathlib import Path
import json,sys
ROOT=Path(__file__).resolve().parents[1];sys.path.insert(0,str(ROOT/'tools'))
from at28c64b_model import At28c64b
s=json.load(open(ROOT/'compiler_db/component_store.json')); r=next(x for x in s['records'] if x.get('display_name')=='AT28C64B')
assert r['properties']['organization']=='8192 x 8' and r['properties']['page_bytes']==64
assert r['timing']['AT28C64B-15']['address_to_output_ns_max']==150
pins={x['number']:x['name'] for x in r['pins']}; assert pins[2]=='A12' and pins[20]=='CE#' and pins[22]=='OE#' and pins[27]=='WE#' and pins[28]=='VCC'
m=At28c64b(); assert m.eval_read(0,False,False,True)==0xff and m.eval_read(0,True,False,True) is None
assert m.byte_write(0x123,0x5a,0)==True
p=m.read(0x123,1); assert (p&0x80)==((~0x5a)&0x80) and m.read(0x123,10_000_001)==0x5a
m2=At28c64b(); assert m2.page_write([(0x40+i,i) for i in range(64)],0); assert m2.read(0x45,10_000_001)==5
try: m2.page_write([(0x3f,1),(0x40,2)],20_000_000); raise AssertionError('cross-page write accepted')
except ValueError: pass
assert r['programmed_state']['mutable_in_system'] is True
print('CP89 AT28C64B parallel EEPROM authenticity: PASS')
