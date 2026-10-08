#!/usr/bin/env python3
"""Derive backend-neutral glue operations from Construction IR.
No board names or part IDs are special-cased. Operations describe topology classes;
source backends decide how to implement each operation.
"""
import argparse,json,re
from pathlib import Path
ap=argparse.ArgumentParser(); ap.add_argument('construction_ir'); ap.add_argument('out'); a=ap.parse_args()
p=json.load(open(a.construction_ir,encoding='utf-8'))
if p.get('format')!='VMB-CONSTRUCTION-IR-1' or not p.get('ready'): raise SystemExit('construction IR not ready')
inst={x['id']:x for x in p['instances']}
def expand(spec):
 m=re.fullmatch(r'([A-Za-z_#]+)(\d+)\.\.\1?(\d+)',spec)
 if not m: return [spec]
 pre,lo,hi=m.group(1),int(m.group(2)),int(m.group(3)); step=1 if hi>=lo else -1
 return [f'{pre}{i}' for i in range(lo,hi+step,step)]
def binding(ep):
 if ep['kind']!='MODULE_PIN': return None
 for c in inst[ep['instance_id']].get('endpoints',[]):
  if c.get('interface')!=ep.get('interface'): continue
  if ep.get('signal') in sum((expand(s) for s in c.get('signals',[])),[]): return c.get('binding',{})
 return None
ops=[]; errors=[]; stats={}
for net in p['connections']:
 eps=[]
 for e in net['endpoints']:
  q=dict(e); q['binding']=binding(e); eps.append(q)
 mods=[e for e in eps if e['kind']=='MODULE_PIN']; plats=[e for e in eps if e['kind']=='PLATFORM_PIN']
 kinds=sorted(set((e.get('binding') or {}).get('kind','unbound') for e in mods))
 if any(k=='unbound' for k in kinds):
  errors.append({'code':'UNBOUND_MODULE_ENDPOINT','net':net['net'],'endpoints':eps}); op='unresolved'
 elif plats and mods: op='platform_net'
 elif len(mods)>=2: op='internal_net'
 elif len(mods)==1: op='terminal_net'
 else: op='passive_or_platform_net'
 rec={'id':net['id'],'net':net['net'],'operation':op,'binding_kinds':kinds,'endpoints':eps}
 ops.append(rec); stats[op]=stats.get(op,0)+1
out={'format':'VMB-GLUE-IR-1','principle':'Derived backend-neutral glue plan. Board Script remains canonical board IR.','source_format':p['format'],'operations':ops,'statistics':stats,'errors':errors,'ready':not errors}
Path(a.out).write_text(json.dumps(out,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'out':a.out,'operations':len(ops),'statistics':stats,'errors':len(errors),'ready':not errors},indent=2))
