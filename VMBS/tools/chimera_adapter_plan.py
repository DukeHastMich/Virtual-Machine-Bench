#!/usr/bin/env python3
"""Lower Glue IR into backend-neutral semantic adapter groups.
Adapters are inferred from interface+binding metadata, never board or part names.
"""
import argparse,json
from pathlib import Path
ap=argparse.ArgumentParser(); ap.add_argument('glue_ir'); ap.add_argument('out'); a=ap.parse_args()
p=json.load(open(a.glue_ir)); assert p['format']=='VMB-GLUE-IR-1' and p['ready']
groups={}; netrefs={}
# Learn the adapter vocabulary for each module interface from any endpoint that declares it.
interface_adapter={}
for n in p['operations']:
 for e in n['endpoints']:
  if e['kind']=='MODULE_PIN' and (e.get('binding') or {}).get('adapter'):
   interface_adapter[(e['instance_id'],e['interface'])]=(e.get('binding') or {})['adapter']
for n in p['operations']:
 for e in n['endpoints']:
  if e['kind']!='MODULE_PIN': continue
  b=e.get('binding') or {}; adapter=b.get('adapter') or interface_adapter.get((e['instance_id'],e['interface']))
  if not adapter: continue
  key=(e['instance_id'],e['interface'],adapter)
  g=groups.setdefault(key,{'instance_id':e['instance_id'],'interface':e['interface'],'adapter':adapter,'binding_kinds':set(),'signals':{},'nets':set()})
  g['binding_kinds'].add(b.get('kind','')); g['signals'].setdefault(e['signal'],[]).append(n['net']); g['nets'].add(n['net'])
  netrefs.setdefault(n['net'],set()).add(key)
out=[]
for key,g in sorted(groups.items()):
 g['binding_kinds']=sorted(g['binding_kinds']); g['nets']=sorted(g['nets']); out.append(g)
links=[]
for net,keys in sorted(netrefs.items()):
 if len(keys)>1: links.append({'net':net,'adapter_groups':[{'instance_id':k[0],'interface':k[1],'adapter':k[2]} for k in sorted(keys)]})
r={'format':'VMB-ADAPTER-IR-1','source_format':p['format'],'principle':'Semantic adapters derived from interface contracts; no board/part special cases.','adapter_groups':out,'cross_adapter_nets':links,'statistics':{'adapter_groups':len(out),'cross_adapter_nets':len(links)},'ready':True}
Path(a.out).write_text(json.dumps(r,indent=2)+'\n'); print(json.dumps(r['statistics'],indent=2))
