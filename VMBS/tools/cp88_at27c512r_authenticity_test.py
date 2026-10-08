from pathlib import Path
import json,sys
ROOT=Path(__file__).resolve().parents[1];sys.path.insert(0,str(ROOT/'tools'))
from eprom27512_model import Eprom27512
s=json.load(open(ROOT/'compiler_db/component_store.json'));r=next(x for x in s['records'] if x.get('display_name')=='AT27C512R')
assert r['properties']['organization']=='65536 x 8' and r['properties']['read_access_ns_max_best']==45
assert 'read_access_ns_min' not in r['properties']
assert r['electrical']['read_vcc_v']=={'nominal':5.0,'tolerance_percent':10}
assert r['electrical']['active_current_ma_max_at_5mhz']==20
assert r['electrical']['standby_current_ua_max_cmos']==100
assert r['timing']['-45']['address_to_output_ns_max']==45 and r['timing']['-70']['address_to_output_ns_max']==70
assert len(r['pins'])==28
plcc=r['physical']['package_pin_maps']['PLCC-32']; assert plcc['NC']==[1,12,17,26] and plcc['VCC']==32 and plcc['GND']==16
m=Eprom27512(bytes((i&255 for i in range(65536))))
assert m.eval_read(0x1234,False,False)==0x34 and m.eval_read(0,True,False) is None and m.eval_read(0,False,True) is None
assert r['record_status']=='researched-interface-read-authentic'
print('CP88 AT27C512R interface/read/package authenticity: PASS')
