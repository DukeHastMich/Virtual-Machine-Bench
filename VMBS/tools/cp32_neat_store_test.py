#!/usr/bin/env python3
import json,re
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]; VMB=ROOT.parent
def must(x,m):
 if not x: raise AssertionError(m)
code=json.load(open(ROOT/'compiler_db/code_store.json')); store=json.load(open(ROOT/'compiler_db/component_store.json'))
mods={m['module_id']:m for m in code['modules']}
expected={
 '82C206':('VMB.CT.82C206','Ct82C206BoardScriptWorker','ct.neat.82c206'),
 '82C211':('VMB.CT.82C211','Ct82C211BoardScriptWorker','ct.neat.82c211'),
 '82C212':('VMB.CT.82C212','Ct82C212BoardScriptWorker','ct.neat.82c212'),
 '82C215':('VMB.CT.82C215','Ct82C215BoardScriptWorker','ct.neat.82c215')}
src=(VMB/'Hardware/Chipsets/Chips and Technologies/CS8221 NEAT/CtNeatBoardScriptWorkers.vb').read_text(errors='ignore')
reg=(VMB/'Hardware/Board Script/BoardScriptWorkers.vb').read_text(errors='ignore')
functional={}
for f in store['variant_stub_policy']['families']:
 if f.get('record_status')=='functional_variant':
  for a in f.get('variants',[]): functional[a.upper()]=f.get('vmb_binding',{}).get('module_id')
for alias,(mid,cls,worker) in expected.items():
 must(mid in mods,mid+' absent')
 im=mods[mid]['implementation']; must(im.get('status')=='implemented',mid+' not implemented')
 must(cls in src,cls+' absent')
 must(worker in reg,worker+' not registered')
 must(functional.get(alias)==mid,alias+' wrong functional binding')
# Do not accidentally claim later NEAT variants.
for alias in ('82C235','82C236'):
 must(functional.get(alias) is None,alias+' was promoted without audited implementation')
print(json.dumps({'pass':True,'neat_packages':len(expected),'unpromoted':['82C235','82C236']},indent=2))
