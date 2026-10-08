from pathlib import Path
import json,sys
ROOT=Path(__file__).resolve().parents[1];sys.path.insert(0,str(ROOT/'tools'))
from at28c256_model import At28c256
s=json.load(open(ROOT/'compiler_db/component_store.json')); r=next(x for x in s['records'] if x.get('display_name')=='AT28C256')
assert r['properties']['organization']=='32768 x 8' and r['properties']['page_bytes']==64
assert r['timing']['AT28C256-15']['address_to_output_ns_max']==150
pins={x['number']:x['name'] for x in r['pins']}; assert pins[1]=='A14' and pins[20]=='CE#' and pins[22]=='OE#' and pins[26]=='A13' and pins[27]=='WE#' and pins[28]=='VCC'
m=At28c256(); assert m.eval_read(0,False,False,True)==0xff and m.eval_read(0,True,False,True) is None
assert m.byte_write(0x7123,0x5a,0); assert (m.read(0x7123,1)&0x80)==((~0x5a)&0x80); assert m.read(0x7123,10_000_001)==0x5a
m2=At28c256(); assert m2.page_write([(0x7f40+i,i) for i in range(64)],0); assert m2.read(0x7f45,10_000_001)==5
try: m2.page_write([(0x3f,1),(0x40,2)],20_000_000); raise AssertionError('cross-page write accepted')
except ValueError: pass
assert r['programmed_state']['mutable_in_system'] is True
print('CP90 AT28C256 paged parallel EEPROM authenticity: PASS')
