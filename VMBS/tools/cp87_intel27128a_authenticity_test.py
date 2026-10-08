from pathlib import Path
import json,sys
ROOT=Path(__file__).resolve().parents[1]; sys.path.insert(0,str(ROOT/'tools'))
from eprom27128_model import Eprom27128
s=json.load(open(ROOT/'compiler_db/component_store.json')); r=next(x for x in s['records'] if x.get('display_name')=='27128A')
assert len(r['pins'])==28 and {p['name'] for p in r['pins']} >= {'VPP','PGM#','CE#','OE#','A13','O7','VCC','GND'}
assert r['properties']['organization']=='16384 x 8'
assert r['electrical']['read_vpp_v']['nominal']==5.0
assert r['electrical']['read_vpp_v']['min']==3.8
assert r['electrical']['active_current_ma_max']==100
assert r['electrical']['standby_current_ma_max']==40
assert r['timing']['27128A-1']['address_to_output_ns_max']==150
assert r['timing']['27128A-2']['address_to_output_ns_max']==200
assert r['timing']['27128A-20_25']['address_to_output_ns_max']==250
m=Eprom27128(bytes((i&255 for i in range(16384))))
assert m.eval_read(0x123,False,False,True)==0x23
assert m.eval_read(0x123,True,False,True) is None
assert m.eval_read(0x123,False,True,True) is None
assert m.eval_read(0x123,False,False,False) is None # PGM low is not documented normal-read mode
assert r['record_status']=='researched-interface-read-authentic'
print('CP87 Intel 27128A interface/read authenticity: PASS')
