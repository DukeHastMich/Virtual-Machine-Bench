from pathlib import Path
import json,sys
ROOT=Path(__file__).resolve().parents[1];sys.path.insert(0,str(ROOT/'tools'))
from at28c010_model import At28c010
s=json.load(open(ROOT/'compiler_db/component_store.json'));r=next(x for x in s['records'] if x.get('display_name')=='AT28C010')
assert r['properties']['organization']=='131072 x 8' and r['properties']['page_bytes']==128
assert r['timing']['AT28C010-12']['address_to_output_ns_max']==120
p={x['number']:x['name'] for x in r['pins']};assert p[2]=='A16' and p[22]=='CE#' and p[24]=='OE#' and p[31]=='WE#'
m=At28c010();assert m.eval_read(0,False,False,True)==255;assert m.eval_read(0,True,False,True) is None
assert m.byte_write(0x12345,0x5a,0);assert (m.read(0x12345,1)&0x80)==0x80;assert m.read(0x12345,10000001)==0x5a
m=At28c010();assert m.page_write([(0x1ff80+i,i) for i in range(128)],0);assert m.read(0x1ff85,10000001)==5
try:m.page_write([(0x7f,1),(0x80,2)],20000000);raise AssertionError
except ValueError:pass
print('CP91 AT28C010 parallel EEPROM authenticity: PASS')
