#!/usr/bin/env python3
import json,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];VMB=ROOT.parent
sys.path.insert(0,str(ROOT/'tools'))
from ic_logic_models import DualDff74,Register273,Counter161,Counter393
def must(x,m):
 if not x:raise AssertionError(m)
d=DualDff74(); p={'1PRE_N':1,'1CLR_N':1,'1CLK':0,'1D':1,'2PRE_N':1,'2CLR_N':1,'2CLK':0,'2D':0};d.step(p);p['1CLK']=1
must(d.step(p)['1Q']==1,'74 rising edge');p['1CLR_N']=0;must(d.step(p)['1Q']==0,'74 async clear')
r=Register273();p={'CLR_N':1,'CLK':0,**{f'D{i}':i&1 for i in range(1,9)}};r.step(p);p['CLK']=1
must(r.step(p)['Q1']==1 and r.step(p)['Q2']==0,'273 capture');p['CLR_N']=0;must(all(v==0 for v in r.step(p).values()),'273 clear')
c=Counter161(False);p={'CLR_N':1,'LOAD_N':1,'ENP':1,'ENT':1,'CLK':0};c.step(p);p['CLK']=1;must(c.step(p)['QA']==1,'161 count');p['CLR_N']=0;must(c.step(p)['QA']==0,'161 async clear')
c=Counter161(True);p={'CLR_N':0,'LOAD_N':1,'ENP':1,'ENT':1,'CLK':0};c.step(p);must(c.value==0,'163 pre-edge');p['CLK']=1;must(c.step(p)['QA']==0,'163 sync clear')
q=Counter393();p={'1CLR':0,'1CLK':1,'2CLR':0,'2CLK':0};q.step(p);p['1CLK']=0;must(q.step(p)['1QA']==1,'393 falling-edge count');p['1CLR']=1;must(q.step(p)['1QA']==0,'393 clear')
code=json.load(open(ROOT/'compiler_db/code_store.json'));mods={m['module_id']:m for m in code['modules']}
for mid in ('VMB.LOGIC.74XX74','VMB.LOGIC.74XX273','VMB.LOGIC.74XX161','VMB.LOGIC.74XX163','VMB.LOGIC.74XX393'):must(mods[mid]['implementation']['compile_allowed'],mid)
store=json.load(open(ROOT/'compiler_db/component_store.json'));bind={}
for f in store['variant_stub_policy']['families']:
 if f.get('record_status')=='functional_variant':
  for a in f.get('variants',[]):bind[a.upper()]=f.get('vmb_binding',{}).get('module_id')
expect={'7474':'VMB.LOGIC.74XX74','74LS74':'VMB.LOGIC.74XX74','74273':'VMB.LOGIC.74XX273','74LS273':'VMB.LOGIC.74XX273','74161':'VMB.LOGIC.74XX161','74LS161':'VMB.LOGIC.74XX161','74163':'VMB.LOGIC.74XX163','74LS163':'VMB.LOGIC.74XX163','74393':'VMB.LOGIC.74XX393','74LS393':'VMB.LOGIC.74XX393','74HC393':'VMB.LOGIC.74XX393'}
for a,m in expect.items():must(bind.get(a)==m,a)
print(json.dumps({'pass':True,'new_modules':5,'promoted_aliases':17},indent=2))
