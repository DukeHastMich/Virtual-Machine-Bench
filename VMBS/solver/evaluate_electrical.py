#!/usr/bin/env python3
import argparse,json,re
from pathlib import Path

def supply_window(e):
    if not e: return None
    for k in ('supply_voltage_v','vcc_v','read_vcc_v'):
        v=e.get(k)
        if isinstance(v,dict):
            lo=v.get('min',v.get('minimum')); hi=v.get('max',v.get('maximum'))
            if lo is not None and hi is not None:return [float(lo),float(hi)]
            n=v.get('nominal'); t=v.get('tolerance_percent')
            if n is not None and t is not None:return [n*(1-t/100),n*(1+t/100)]
    for k in ('normal_vcc_v','normal_read_vcc_v','normal_supply_v'):
        if isinstance(e.get(k),(int,float)):
            n=float(e[k]); t=float(e.get('normal_supply_tolerance_percent',10)); return [n*(1-t/100),n*(1+t/100)]
    if isinstance(e.get('supply_range_v'),list) and len(e['supply_range_v'])==2:return list(map(float,e['supply_range_v']))
    if 'vcc_min_v' in e and 'vcc_max_v' in e:return [float(e['vcc_min_v']),float(e['vcc_max_v'])]
    m=re.search(r'(\d+(?:\.\d+)?)\s*to\s*(\d+(?:\.\d+)?)\s*V',str(e.get('recommended_supply_v','')),re.I)
    if m:return [float(m.group(1)),float(m.group(2))]
    return None

def main():
 ap=argparse.ArgumentParser();ap.add_argument('interfaces');ap.add_argument('board');ap.add_argument('store');ap.add_argument('output');a=ap.parse_args()
 d=json.load(open(a.interfaces)); b=json.load(open(a.board)); s=json.load(open(a.store)); rec={r['part_id']:r for r in s['records']}; inst={c['ref']:c for c in b['components']}
 checks=[];gaps=[];conflicts=[]; supply=[]
 for ref,c in inst.items():
  r=rec.get(c['part_id'],{}); w=supply_window(r.get('electrical') or {})
  if w:supply.append({'ref':ref,'part_id':c['part_id'],'window_v':w,'source':'component_store.electrical'})
 for p in d['paths']:
  typed=[e for e in p['endpoints'] if e.get('direction')]
  outs=[e for e in typed if e.get('direction')=='output']
  bid=[e for e in typed if e.get('direction')=='bidirectional']
  if len(outs)>1:
   conflicts.append({'code':'MULTIPLE_PUSH_PULL_DRIVERS_POSSIBLE','net':p['net'],'path_id':p['id'],'drivers':outs,'status':'NEEDS_OUTPUT_STAGE_TYPING'})
  if bid:
   gaps.append({'code':'BIDIRECTIONAL_ENABLE_SEMANTICS_REQUIRED','net':p['net'],'path_id':p['id'],'count':len(bid)})
  if p.get('cp209',{}).get('status')=='AMBIGUOUS_DIRECTION':
   gaps.append({'code':'DIRECTION_UNRESOLVED','net':p['net'],'path_id':p['id']})
 checks.append({'check':'SUPPLY_WINDOWS_EXTRACTED','status':'PASS_WITH_GAPS','count':len(supply),'detail':'Only explicit numeric/range Store evidence is normalized.'})
 checks.append({'check':'DRIVER_CONTENTION_SCREEN','status':'PASS_WITH_REVIEW' if conflicts else 'PASS','candidates':len(conflicts),'detail':'Multiple known outputs are candidates, not proven contention, until output-stage/enable semantics are typed.'})
 result='TITS_UP_MISSING_EVIDENCE' if gaps else ('UNSAT' if conflicts else 'PASS')
 o={'format':'VMBS-ELECTRICAL-FEASIBILITY-1','board':d.get('board_id') or 'ISA8_Ethernet','result':result,'checks':checks,'supply_requirements':supply,'conflict_candidates':conflicts,'gaps':gaps,'summary':{'supply_windows':len(supply),'contention_candidates':len(conflicts),'gaps':len(gaps)}}
 Path(a.output).write_text(json.dumps(o,indent=2)+'\n');print(json.dumps(o['summary']))
if __name__=='__main__':main()
