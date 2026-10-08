#!/usr/bin/env python3
"""CP208 endpoint semantic typing and conservative path-role resolution.
Uses only Component Store pin metadata plus BS topology. Never guesses a direction.
"""
import argparse,json
from pathlib import Path

INPUT={'input'}; OUTPUT={'output'}; BIDI={'bidirectional','bidir','inout'}
POWER_ROLES={'power','ground','supply','vcc','gnd','power_input','power_output'}

def norm_dir(v):
    if not v: return None
    v=str(v).strip().lower().replace('-','_')
    if v in INPUT: return 'input'
    if v in OUTPUT: return 'output'
    if v in BIDI: return 'bidirectional'
    if v in ('passive','power','ground','supply','nc','no_connect'): return v
    return None

def main():
    ap=argparse.ArgumentParser(); ap.add_argument('board'); ap.add_argument('store'); ap.add_argument('cp207'); ap.add_argument('output'); a=ap.parse_args()
    b=json.load(open(a.board,encoding='utf-8')); s=json.load(open(a.store,encoding='utf-8')); old=json.load(open(a.cp207,encoding='utf-8'))
    byid={r.get('part_id'):r for r in s.get('records',[])}; inst={c.get('ref'):c for c in b.get('components',[])}
    pinidx={}
    for ref,c in inst.items():
        rec=byid.get(c.get('part_id')) or {}
        for p in rec.get('pins') or []:
            pad=str(p.get('pad',''))
            if pad: pinidx[(ref,pad)]=(rec,p)
    paths=[]; gaps=list(old.get('gaps',[])); counts={'driver_receiver':0,'bidirectional':0,'passive_or_power':0,'ambiguous':0,'untyped_endpoints':0}
    for n in b.get('nets',[]):
        eps=n.get('endpoints') or []
        if len(eps)<2: continue
        typed=[]
        for e in eps:
            ref,pad=e.get('ref'),str(e.get('pad','')); recpin=pinidx.get((ref,pad))
            if recpin:
                rec,p=recpin; d=norm_dir(p.get('direction'))
                facets=p.get('interface_facets') or []
                typed.append({'ref':ref,'pad':pad,'part_id':rec.get('part_id'),'pin_name':p.get('name'),'direction':d,
                              'role':p.get('role'),'sampling':p.get('sampling'),'interface_facets':facets,'typing_source':'component_store_pin'})
            else:
                typed.append({'ref':ref,'pad':pad,'direction':None,'typing_source':'unresolved'})
                counts['untyped_endpoints']+=1
        dirs=[x.get('direction') for x in typed]
        outs=[x for x in typed if x.get('direction')=='output']; ins=[x for x in typed if x.get('direction')=='input']; bis=[x for x in typed if x.get('direction')=='bidirectional']
        passive=[x for x in typed if x.get('direction') in ('passive','power','ground','supply')]
        if outs and ins and not bis:
            status='DRIVER_RECEIVER_RESOLVED'; counts['driver_receiver']+=1
            role={'drivers':[{'ref':x['ref'],'pad':x['pad']} for x in outs],'receivers':[{'ref':x['ref'],'pad':x['pad']} for x in ins]}
        elif bis and not outs:
            status='BIDIRECTIONAL_BUS'; counts['bidirectional']+=1; role={'participants':[{'ref':x['ref'],'pad':x['pad']} for x in bis]}
        elif len(passive)==len(typed) and typed:
            status='PASSIVE_OR_POWER'; counts['passive_or_power']+=1; role={}
        else:
            status='AMBIGUOUS_DIRECTION'; counts['ambiguous']+=1; role={}
        paths.append({'id':f"NETPATH:{n.get('id')}",'net_id':n.get('id'),'net':n.get('name',''),'endpoints':typed,
                      'role_resolution':role,'status':status,'timing_budget':None,'timing_status':'NOT_DERIVED'})
    # Explicitly report unresolved endpoint semantics once per board, without making it a hard blocker yet.
    if counts['untyped_endpoints']:
        gaps.append({'code':'UNTYPED_ENDPOINTS_REMAIN','subject':old.get('board_id') or 'board','severity':'BLOCKS_VERIFICATION',
                     'detail':f"{counts['untyped_endpoints']} net endpoints lack Component Store pin direction metadata; no direction was inferred."})
    out={'format':'VMBS-SOLVER-ENDPOINT-IR-1','board_id':old.get('board_id'),'mode':old.get('mode'),'paths':paths,'gaps':gaps,
         'summary':{'paths':len(paths),**counts,'timed_paths':0}}
    Path(a.output).write_text(json.dumps(out,indent=2)+'\n',encoding='utf-8'); print(json.dumps(out['summary']))
if __name__=='__main__': main()
