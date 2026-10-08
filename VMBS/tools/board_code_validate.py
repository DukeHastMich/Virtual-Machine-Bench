#!/usr/bin/env python3
"""Structural/dependency validator for VMB-BOARD-CODE-1."""
import argparse,json
ap=argparse.ArgumentParser(); ap.add_argument('code'); a=ap.parse_args(); c=json.load(open(a.code)); mods=set(); plats=set(); ctrls=set(); slots=set(); errors=[]; finalized=False
for i,x in enumerate(c['instructions']):
 op=x['op']
 def err(code,**kw): errors.append({'index':i,'code':code,**kw})
 if finalized: err('OP_AFTER_FINALIZE',op=op)
 if op=='ALLOC_MODULE':
  if x['id'] in mods: err('DUPLICATE_MODULE',id=x['id'])
  mods.add(x['id'])
 elif op=='ATTACH_PLATFORM': plats.add(x['id'])
 elif op=='CREATE_CONTROL': ctrls.add(x['id'])
 elif op=='BIND_CONFIG_PIN':
  if x['module'] not in mods: err('CONFIG_BEFORE_MODULE',module=x['module'])
  for b in x['source'].get('bindings',[]):
   if b['control'] not in ctrls: err('CONFIG_CONTROL_MISSING',control=b['control'])
 elif op=='DEFINE_PROPERTY':
  if x['module'] not in mods: err('PROPERTY_BEFORE_MODULE',module=x['module'])
 elif op=='LINK':
  for e in x['endpoints']:
   if e['kind']=='MODULE_PIN' and e['instance_id'] not in mods: err('LINK_MODULE_MISSING',module=e['instance_id'])
   if e['kind']=='PLATFORM_PIN' and e['instance_id'] not in plats: err('LINK_PLATFORM_MISSING',platform=e['instance_id'])
 elif op=='CREATE_DEVICE_SLOT':
  if x['instance_id'] not in mods: err('SLOT_MODULE_MISSING',module=x['instance_id'])
  if x['slot_id'] in slots: err('DUPLICATE_SLOT',slot=x['slot_id'])
  slots.add(x['slot_id'])
 elif op=='FINALIZE_BOARD': finalized=True
 else: err('UNKNOWN_OPCODE',op=op)
if not finalized: errors.append({'code':'FINALIZE_MISSING'})
r={'valid':not errors,'errors':errors,'modules':len(mods),'platforms':len(plats),'controls':len(ctrls),'slots':len(slots)}; print(json.dumps(r,indent=2)); raise SystemExit(0 if r['valid'] else 1)
