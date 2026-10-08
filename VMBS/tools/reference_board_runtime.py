#!/usr/bin/env python3
"""Reference executor for VMB-BOARD-CODE-1. Validates construction semantics without emulating hardware."""
import argparse,json
ap=argparse.ArgumentParser(); ap.add_argument('code'); ap.add_argument('out'); a=ap.parse_args(); c=json.load(open(a.code))
state={'modules':{},'platforms':{},'controls':{},'config':[],'properties':[],'links':{},'slots':{},'final':None}; errors=[]
for i,x in enumerate(c['instructions']):
 op=x['op']
 if op=='ALLOC_MODULE': state['modules'][x['id']]={'module_id':x['module_id'],'part_id':x['part_id']}
 elif op=='ATTACH_PLATFORM': state['platforms'][x['id']]={'platform':x['platform'],'signals':x['signals']}
 elif op=='CREATE_CONTROL': state['controls'][x['id']]={k:v for k,v in x.items() if k not in ('op','id')}
 elif op=='BIND_CONFIG_PIN': state['config'].append({k:v for k,v in x.items() if k!='op'})
 elif op=='DEFINE_PROPERTY': state['properties'].append({k:v for k,v in x.items() if k!='op'})
 elif op=='LINK': state['links'][x['id']]={'net':x['net'],'endpoints':x['endpoints']}
 elif op=='CREATE_DEVICE_SLOT': state['slots'][x['slot_id']]={k:v for k,v in x.items() if k not in ('op','slot_id')}
 elif op=='FINALIZE_BOARD': state['final']={'hardware_tits':x['hardware_tits'],'execution_tits':x['execution_tits'],'default_execution_tits':x.get('default_execution_tits',x['execution_tits'])}
# Semantic postconditions independent of specimen identity.
for b in state['config']:
 if b['module'] not in state['modules']: errors.append({'code':'CONFIG_MODULE_MISSING','module':b['module']})
 for q in b['source'].get('bindings',[]):
  if q['control'] not in state['controls']: errors.append({'code':'CONFIG_CONTROL_MISSING','control':q['control']})
for lid,l in state['links'].items():
 for e in l['endpoints']:
  if e['kind']=='MODULE_PIN' and e['instance_id'] not in state['modules']: errors.append({'code':'LINK_MODULE_MISSING','link':lid,'module':e['instance_id']})
  if e['kind']=='PLATFORM_PIN' and e['instance_id'] not in state['platforms']: errors.append({'code':'LINK_PLATFORM_MISSING','link':lid,'platform':e['instance_id']})
for sid,s in state['slots'].items():
 if s['instance_id'] not in state['modules']: errors.append({'code':'SLOT_MODULE_MISSING','slot':sid})
state['errors']=errors; state['valid']=not errors
open(a.out,'w').write(json.dumps(state,indent=2)+'\n'); print(json.dumps({'valid':state['valid'],'modules':len(state['modules']),'platforms':len(state['platforms']),'controls':len(state['controls']),'links':len(state['links']),'slots':len(state['slots']),'final':state['final']},indent=2)); raise SystemExit(0 if state['valid'] else 1)
