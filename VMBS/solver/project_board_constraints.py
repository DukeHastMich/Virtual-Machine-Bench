#!/usr/bin/env python3
"""CP207 board-specific constraint projection.
Joins Board Script topology/placement/programmed content to Component Store facts.
Does not infer missing electrical/timing semantics.
"""
import argparse,json,re
from pathlib import Path

def main():
 p=argparse.ArgumentParser(); p.add_argument('board'); p.add_argument('store'); p.add_argument('output'); a=p.parse_args()
 b=json.load(open(a.board,encoding='utf-8')); s=json.load(open(a.store,encoding='utf-8'))
 byid={r.get('part_id'):r for r in s.get('records',[])}
 out={'format':'VMBS-SOLVER-BOARD-CONSTRAINT-IR-1','board_id':(b.get('identity') or {}).get('name') or Path(a.board).stem,
      'mode':'AUTHORITATIVE' if (((b.get('authority') or {}).get('level') or '') if isinstance(b.get('authority'),dict) else (b.get('authority') or '')).lower() in ('authoritative','source') else 'REFERENCE',
      'constraints':[],'paths':[],'gaps':[]}
 seq=0
 def add(cls,kind,subject,value,hard=True,authority='authoritative',mutability='locked',source=None):
  nonlocal seq; seq+=1; out['constraints'].append({'id':f'BC{seq:05d}','class':cls,'kind':kind,'hard':hard,'subject':subject,'value':value,'authority':authority,'mutability':mutability,'source':source or {'board':a.board}})
 # board components: identity + observed placement
 for c in b.get('components',[]):
  ref=c.get('ref'); pid=c.get('part_id'); rec=byid.get(pid)
  if not rec:
   out['gaps'].append({'code':'PART_NOT_IN_STORE','subject':ref,'severity':'BLOCKS_PHYSICAL_SOLVE','detail':pid or 'no part_id'}); continue
  add('HARD_CORRECTNESS','component_identity',ref,{'part_id':pid,'display_name':rec.get('display_name')})
  if c.get('footprint'): add('HARD_PHYSICAL','selected_footprint',ref,c['footprint'])
  pl=c.get('placement') or {}
  if pl.get('at'): add('SOFT_HISTORICAL','observed_placement',ref,{'at':pl['at']},False,'verified_reconstruction','bounded')
  # component electrical/timing facts are attached to instance, still raw until typed normalization
  if rec.get('electrical'): add('HARD_ELECTRICAL','instance_electrical_evidence',ref,rec['electrical'],True,'supporting','bounded',{'part_id':pid})
  if rec.get('timing'): add('HARD_TIMING','instance_timing_evidence',ref,rec['timing'],True,'supporting','bounded',{'part_id':pid})
 # net topology becomes exact board-specific endpoint constraints
 for n in b.get('nets',[]):
  name=n.get('name',''); eps=n.get('endpoints') or []
  if name or eps: add('HARD_CORRECTNESS','net_topology',name or f"net:{n.get('id')}",{'net_id':n.get('id'),'endpoints':eps})
  # classify obvious rail nets only; names are evidence, not voltage inference
  if name.upper() in ('GND','VCC','+5V','+12V','-12V','+3V3','3V3'):
   add('HARD_ELECTRICAL','named_power_net',name,{'net_id':n.get('id'),'name':name},True,'verified_reconstruction','locked')
 # external connectors are hard interface mappings
 for x in b.get('external_interfaces',[]):
  add('HARD_CORRECTNESS','external_interface',x.get('ref'),x)
 # payloads must survive physical realization
 for pc in b.get('programmed_contents',[]):
  add('HARD_CORRECTNESS','programmed_content',pc.get('ref'),pc)
  if not pc.get('payload_id'):
   out['gaps'].append({'code':'PROGRAMMED_PAYLOAD_UNRESOLVED','subject':pc.get('ref','?'),'severity':'BLOCKS_VERIFICATION','detail':'Programmed device exists but payload_id is unresolved.'})
 # create board paths as topology objects; do NOT assign delay budgets yet
 for n in b.get('nets',[]):
  eps=n.get('endpoints') or []
  if len(eps)>=2:
   out['paths'].append({'id':f"NETPATH:{n.get('id')}",'net_id':n.get('id'),'net':n.get('name',''),'endpoints':eps,
      'timing_budget':None,'electrical_class':None,'status':'TOPOLOGY_ONLY'})
 # carry board readiness problems into solver gap language
 rd=b.get('readiness') or {}
 for k,v in rd.items():
  if v is False: out['gaps'].append({'code':'BOARD_READINESS_FALSE','subject':k,'severity':'BLOCKS_VERIFICATION','detail':'Board Script readiness flag is false.'})
 out['summary']={'components':len(b.get('components',[])),'nets':len(b.get('nets',[])),'constraints':len(out['constraints']),'paths':len(out['paths']),'gaps':len(out['gaps']),
                 'timed_paths':sum(1 for x in out['paths'] if x['timing_budget'] is not None)}
 Path(a.output).write_text(json.dumps(out,indent=2)+'\n',encoding='utf-8'); print(json.dumps(out['summary']))
if __name__=='__main__': main()
