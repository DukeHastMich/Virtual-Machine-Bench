#!/usr/bin/env python3
import json, sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'tools'))
from ic_logic_models import comb,Latch373,Register374,SUPPORTED

def must(x,msg):
    if not x: raise AssertionError(msg)
# Gate truth and high-Z propagation
must(comb('VMB.LOGIC.74XX00',{'1A':1,'1B':1})['1Y']==0,'7400 NAND')
must(comb('VMB.LOGIC.74XX02',{'1A':0,'1B':0})['1Y']==1,'7402 NOR')
must(comb('VMB.LOGIC.74XX04',{'1A':1})['1Y']==0,'7404 NOT')
must(comb('VMB.LOGIC.74XX08',{'1A':1,'1B':1})['1Y']==1,'7408 AND')
must(comb('VMB.LOGIC.74XX32',{'1A':0,'1B':1})['1Y']==1,'7432 OR')
must(comb('VMB.LOGIC.74XX86',{'1A':1,'1B':1})['1Y']==0,'7486 XOR')
must(comb('VMB.LOGIC.74XX00',{'1A':None,'1B':1})['1Y'] is None,'unknown/Z propagation')
# Decoder enable/select
x=comb('VMB.LOGIC.74XX138',{'A':1,'B':0,'C':1,'G1':1,'G2A_N':0,'G2B_N':0})
must(x['Y5_N']==0 and sum(v==0 for v in x.values())==1,'74138 decode')
x=comb('VMB.LOGIC.74XX138',{'A':1,'B':0,'C':1,'G1':0,'G2A_N':0,'G2B_N':0})
must(all(v==1 for v in x.values()),'74138 disable')
# 244 tri-state
x=comb('VMB.LOGIC.74XX244',{'OE1_N':0,'1A1':1,'OE2_N':1,'2A1':1})
must(x['1Y1']==1 and x['2Y1'] is None,'74244 enable')
# 245 direction and disable
x=comb('VMB.LOGIC.74XX245',{'OE_N':0,'DIR':1,'A1':1})
must(x['B1']==1 and x['A1'] is None,'74245 A->B')
x=comb('VMB.LOGIC.74XX245',{'OE_N':0,'DIR':0,'B1':1})
must(x['A1']==1 and x['B1'] is None,'74245 B->A')
x=comb('VMB.LOGIC.74XX245',{'OE_N':1,'DIR':1,'A1':1})
must(x['A1'] is None and x['B1'] is None,'74245 disable')
# 373 level latch
l=Latch373(); q=l.step({'OE_N':0,'LE':1,'D1':1}); must(q['Q1']==1,'74373 transparent')
q=l.step({'OE_N':0,'LE':0,'D1':0}); must(q['Q1']==1,'74373 hold')
q=l.step({'OE_N':1,'LE':0}); must(q['Q1'] is None,'74373 output disable')
# 374 rising edge only
r=Register374(); r.step({'OE_N':0,'CLK':0,'D1':1}); q=r.step({'OE_N':0,'CLK':1,'D1':1}); must(q['Q1']==1,'74374 rising edge')
q=r.step({'OE_N':0,'CLK':1,'D1':0}); must(q['Q1']==1,'74374 no level retrigger')
r.step({'OE_N':0,'CLK':0,'D1':0}); q=r.step({'OE_N':0,'CLK':1,'D1':0}); must(q['Q1']==0,'74374 next rising edge')
# Store integrity
cs=json.load(open(ROOT/'compiler_db/component_store.json')); code=json.load(open(ROOT/'compiler_db/code_store.json'))
mods={m['module_id']:m for m in code['modules']}
for mid in SUPPORTED:
    must(mid in mods,mid+' absent from Code Store')
    must(mods[mid]['implementation'].get('status')=='implemented',mid+' not implemented')
functional=[f for f in cs['variant_stub_policy']['families'] if f.get('record_status')=='functional_variant' and str(f.get('vmb_binding',{}).get('module_id','')).startswith('VMB.LOGIC.')]
seen={}
for f in functional:
    mid=f['vmb_binding']['module_id']; must(mid in SUPPORTED,'unexpected functional id '+mid)
    pads=[p['pad'] for p in f.get('pins',[])]; must(len(pads)==len(set(pads)),'duplicate pads '+f['family'])
    for a in f['variants']:
        key=''.join(ch for ch in a.upper() if ch.isalnum())
        must(key not in seen,'duplicate functional alias '+a); seen[key]=mid
for alias in ['74LS00','74HC04','74LS138','SN74LS244N','SN74LS245N','74LS373','74LS374']:
    key=''.join(ch for ch in alias.upper() if ch.isalnum()); must(key in seen,alias+' not promoted')
print(json.dumps({'pass':True,'functional_families':len(functional),'module_ids':len(SUPPORTED),'functional_aliases':len(seen)},indent=2))
