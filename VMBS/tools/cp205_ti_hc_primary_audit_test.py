#!/usr/bin/env python3
import json,sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).parent)); import ic_logic_models as m
root=Path(__file__).parents[2]
s=json.load(open(root/'VMBS/compiler_db/component_store.json')); c=json.load(open(root/'VMBS/compiler_db/code_store.json'))
R={r.get('manufacturer_part_number'):r for r in s['records']}; M={x['module_id']:x for x in c['modules']}
new=['SN74HC03', 'SN74HC05', 'SN74HC373', 'SN74HC374', 'SN74HC393', 'SN74HC688', 'CD74HC280', 'CD74HC283']
aud={'SN74HC00': 14, 'SN74HC02': 14, 'SN74HC04': 14, 'SN74HC08': 14, 'SN74HC10': 14, 'SN74HC11': 14, 'SN74HC14': 14, 'SN74HC20': 14, 'SN74HC27': 14, 'SN74HC32': 14, 'SN74HC74': 14, 'SN74HC86': 14, 'SN74HC125': 14, 'SN74HC138': 16, 'SN74HC139': 16, 'SN74HC244': 20, 'SN74HC245': 20}
for part in new:
 r=R[part]; mid=r['vmb_binding']['module_id']; assert mid in M and mid in m.SUPPORTED; assert r['physical']['physical_pin_map_complete'] is False; assert r['physical']['package_pin_count']>0
for part,n in aud.items():
 r=R[part]; assert r['physical']['package_pin_count']==n; assert r['physical']['physical_pin_map_complete'] is False; assert r['evidence']['package_count_verified_from_ti_product_page'] is True
print('CP205 PASS: 8 new primary-TI HC identities + 17 primary package-count audits; no pin maps invented')
