#!/usr/bin/env python3
import json,sys
store=json.load(open(sys.argv[1],encoding='utf-8'))
code=json.load(open(sys.argv[2],encoding='utf-8'))
errors=[]; audited=[]
required={'parallel_flash','serial_eeprom','otp_eprom','uv_eprom','slave_microcontroller','eprom_slave_microcontroller','parallel_eeprom'}
for r in store.get('records',[]):
    if r.get('class') not in required: continue
    ps=r.get('programmed_state')
    if not isinstance(ps,dict): errors.append([r.get('display_name'),'PROGRAMMED_STATE_MISSING']); continue
    for k in ('kind','simulation_consumption','reverse_delivery','physical_programming'):
        if not ps.get(k): errors.append([r.get('display_name'),'PROGRAMMED_STATE_FIELD_MISSING',k])
    audited.append(r.get('display_name'))
mods={m.get('module_id'):m for m in code.get('modules',[])}
for r in store.get('records',[]):
    ps=r.get('programmed_state'); vb=r.get('vmb_binding') or {}
    if not ps or not vb.get('module_id'): continue
    m=mods.get(vb['module_id'])
    if not m: errors.append([r['display_name'],'CODE_MODULE_MISSING',vb['module_id']]); continue
    pc=m.get('implementation',{}).get('payload_contract')
    if not pc: errors.append([r['display_name'],'CODE_PAYLOAD_CONTRACT_MISSING',vb['module_id']])
# Transitional KBC personality must be visibly non-authentic, never silently green.
k=mods.get('VMB.AT.KBC8042',{}).get('implementation',{}).get('payload_contract',{})
if k.get('simulation')!='BYPASSED_TRANSITIONAL_BEHAVIOR_MODEL' or not k.get('authenticity_exception'):
    errors.append(['VMB.AT.KBC8042','TRANSITIONAL_BYPASS_NOT_EXPLICIT'])
print(json.dumps({'valid':not errors,'audited':audited,'errors':errors},indent=2))
raise SystemExit(1 if errors else 0)
