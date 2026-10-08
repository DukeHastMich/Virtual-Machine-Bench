#!/usr/bin/env python3
"""Build a language-neutral construction plan from solved Board Code + Code Store composition metadata."""
import argparse,json,hashlib,re
from pathlib import Path
ap=argparse.ArgumentParser(); ap.add_argument('board_code'); ap.add_argument('code_store'); ap.add_argument('out'); a=ap.parse_args()
code=json.load(open(a.board_code,encoding='utf-8')); store=json.load(open(a.code_store,encoding='utf-8'))
mods={m['module_id']:m for m in store['modules']}
instances=[]; errors=[]; links=[]; platform=None; controls=[]; config=[]; props=[]; slots=[]; final=None
for x in code['instructions']:
 op=x['op']
 if op=='ALLOC_MODULE':
  rec=mods.get(x['module_id']); comp=(rec or {}).get('composition'); impl=(rec or {}).get('implementation',{})
  if not rec: errors.append({'code':'MODULE_UNKNOWN','instance':x['id'],'module_id':x['module_id']}); continue
  if not comp: errors.append({'code':'COMPOSITION_CONTRACT_MISSING','instance':x['id'],'module_id':x['module_id']}); continue
  instances.append({'id':x['id'],'module_id':x['module_id'],'part_id':x['part_id'],'kind':rec.get('kind'),'implementation':{'language':impl.get('language'),'source':impl.get('source'),'class':impl.get('class')},'construction':comp.get('construction',{}),'endpoints':comp.get('endpoints',[]),'host_services':comp.get('host_services',[]),'state':comp.get('state')})
 elif op=='LINK': links.append({'id':x['id'],'net':x['net'],'endpoints':x['endpoints']})
 elif op=='ATTACH_PLATFORM': platform={'id':x['id'],'platform':x['platform'],'signals':x['signals']}
 elif op=='CREATE_CONTROL': controls.append({k:v for k,v in x.items() if k!='op'})
 elif op=='BIND_CONFIG_PIN': config.append({k:v for k,v in x.items() if k!='op'})
 elif op=='DEFINE_PROPERTY': props.append({k:v for k,v in x.items() if k!='op'})
 elif op=='CREATE_DEVICE_SLOT': slots.append({k:v for k,v in x.items() if k!='op'})
 elif op=='FINALIZE_BOARD': final={k:v for k,v in x.items() if k!='op'}
# Connections are intentionally preserved as semantic endpoint graphs; source backends choose adapters.
plan={'format':'VMB-CONSTRUCTION-IR-1','principle':'Language-neutral solved construction plan. Board Script remains canonical board IR; this is derived compiler state, not an authoring format.','source':{'board_code_sha256':hashlib.sha256(Path(a.board_code).read_bytes()).hexdigest()},'instances':instances,'platform':platform,'controls':controls,'configuration_bindings':config,'properties':props,'connections':links,'device_slots':slots,'final':final,'errors':errors,'ready':not errors}
Path(a.out).write_text(json.dumps(plan,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'out':a.out,'instances':len(instances),'connections':len(links),'ready':plan['ready'],'errors':len(errors)},indent=2))
