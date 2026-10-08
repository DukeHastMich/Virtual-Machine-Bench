#!/usr/bin/env python3
import json,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]; VMB=ROOT.parent
sys.path.insert(0,str(ROOT/'tools'))
from ic_logic_models import comb,SUPPORTED
def must(x,m):
 if not x: raise AssertionError(m)
for mid in ('VMB.LOGIC.74XX157','VMB.LOGIC.74XX688'): must(mid in SUPPORTED,mid+' unsupported')
# 157: select A/B and inhibit-low output behavior
must(comb('VMB.LOGIC.74XX157',{'E_N':0,'S':0,'1A':1,'1B':0})['1Y']==1,'157 A select')
must(comb('VMB.LOGIC.74XX157',{'E_N':0,'S':1,'1A':1,'1B':0})['1Y']==0,'157 B select')
must(comb('VMB.LOGIC.74XX157',{'E_N':1,'S':0,'1A':1,'1B':1})['1Y']==0,'157 inhibit')
# 688: low only when enabled and all 8 pairs equal
eq={'G_N':0}; eq.update({f'P{i}':i&1 for i in range(8)}); eq.update({f'Q{i}':i&1 for i in range(8)})
must(comb('VMB.LOGIC.74XX688',eq)['P_EQ_Q_N']==0,'688 equal')
neq=dict(eq); neq['Q3']=1-neq['Q3']
must(comb('VMB.LOGIC.74XX688',neq)['P_EQ_Q_N']==1,'688 unequal')
dis=dict(eq); dis['G_N']=1
must(comb('VMB.LOGIC.74XX688',dis)['P_EQ_Q_N']==1,'688 disabled')
code=json.load(open(ROOT/'compiler_db/code_store.json')); mods={m['module_id']:m for m in code['modules']}
for mid in ('VMB.LOGIC.74XX157','VMB.LOGIC.74XX688'):
 must(mods[mid]['implementation']['compile_allowed'] is True,mid+' not compile allowed')
vb=(VMB/'Hardware/IC Modules/DigitalLogicIc.vb').read_text(errors='ignore')
must('Vmb74xx157' in vb and 'Vmb74xx688' in vb,'VB models absent')
store=json.load(open(ROOT/'compiler_db/component_store.json')); bind={}
for f in store['variant_stub_policy']['families']:
 if f.get('record_status')=='functional_variant':
  for a in f.get('variants',[]): bind[a.upper()]=f.get('vmb_binding',{}).get('module_id')
for a in ('74157','74LS157','74F157','74HCT157','74HC157'): must(bind.get(a.upper())=='VMB.LOGIC.74XX157',a)
for a in ('74688','74LS688','74ALS688','74F688','74HCT688','74HC688'): must(bind.get(a.upper())=='VMB.LOGIC.74XX688',a)
# Neighboring non-equivalent muxes must remain unresolved.
for a in ('74153','74LS153','74158','74LS158'): must(bind.get(a.upper()) is None,a+' incorrectly conflated')
print(json.dumps({'pass':True,'new_modules':2,'new_aliases':11},indent=2))
