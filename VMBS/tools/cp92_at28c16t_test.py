from pathlib import Path
import json,sys
ROOT=Path(__file__).resolve().parents[1];sys.path.insert(0,str(ROOT/'tools'));from at28c16t_model import At28c16t
s=json.load(open(ROOT/'compiler_db/component_store.json'));r=next(x for x in s['records'] if x.get('display_name')=='AT28C16-T');p={x['number']:x['name'] for x in r['pins']};assert p[8]=='RDY/BUSY#' and p[28]=='A10' and p[27]=='CE#';m=At28c16t();assert m.ready_busy_n(0);assert m.byte_write(17,0x35,0);assert not m.ready_busy_n(1);assert (m.read(17,1)&0x80)==((~0x35)&0x80);assert m.read(17,1000001)==0x35 and m.ready_busy_n(1000001);print('CP92 AT28C16-T attribute EEPROM authenticity: PASS')
