#!/usr/bin/env python3
import json
from pathlib import Path
root=Path(__file__).resolve().parents[2]
s=(root/'MotherboardDevices.vb').read_text(errors='ignore')
start=s.index('Public Class CmosRtc'); end=s.find('Public Class ',start+20); x=s[start:end if end>=0 else len(s)]
for needle in ['UpdateCyclePicoseconds As Long = 1984000000L','UpdateWarningPicoseconds As Long = 244000000L','PeriodicRateHz','AlarmMatches','EncodeHour','DecodeHour','SetEventFlag','UpdateIrqLine','UpdateInProgress','DividerChainRunningInBed','Case &HC','_registerC = 0']:
    assert needle in x,needle
store=json.load(open(Path(__file__).resolve().parents[1]/'compiler_db/component_store.json'))['records']
r=next(r for r in store if r.get('manufacturer_part_number')=='MC146818A')
assert len(r['pins'])==24
assert r['properties']['general_ram_bytes']==50 and r['properties']['clock_control_bytes']==14
assert r['timing']['update_cycle_us']['32768_hz']==1984
assert r['timing']['uip_minimum_warning_us']==244
assert r['electrical']['supply_range_v']==[3.0,6.0]
assert r['properties']['interface_boundary_note'].startswith('Store truth')
print('CP73 Motorola MC146818A Store/VMB boundary + source audit: PASS')
