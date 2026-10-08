#!/usr/bin/env python3
import json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];VMB=ROOT.parent
def must(x,m):
 if not x:raise AssertionError(m)
code=json.load(open(ROOT/'compiler_db/code_store.json'));mods={m['module_id']:m for m in code['modules']}
for mid in ('VMB.HEADLAND.HT12A','VMB.HUALON.HM6818P'):
 must(mid in mods,mid+' absent');must(mods[mid]['implementation']['status']=='implemented',mid+' not implemented');must(mods[mid]['implementation']['compile_allowed'] is True,mid+' blocked')
reg=(VMB/'Hardware/Board Script/BoardScriptWorkers.vb').read_text(errors='ignore')
must('Register("headland.ht12-a"' in reg,'HT12 worker not registered')
rtc=(VMB/'Hardware/Support Chips/RTC/Hualon HM6818/Hm6818Rtc.vb').read_text(errors='ignore')
must('Class Hm6818Rtc' in rtc and 'MyBase.New(picInBed, persistencePathInBed, 64, Nothing)' in rtc,'HM6818 64-byte behavior witness absent')
fox=(VMB/'Motherboards/Octek/FOX II 286 Rev 3.2/FoxII286Rev32Board.vb').read_text(errors='ignore')
must('Hualon HM6818P' in fox and 'AMD N80L286-16/S' in fox,'FOX specimen witness absent')
must('ProcessorExecutionAvailable' in fox and 'Return False' in fox,'CPU execution gate witness absent')
store=json.load(open(ROOT/'compiler_db/component_store.json'));bind={}
for f in store['variant_stub_policy']['families']:
 for a in f.get('variants',[]):
  if f.get('record_status')=='functional_variant':bind[a.upper()]=f.get('vmb_binding',{}).get('module_id')
must(bind.get('HT12')=='VMB.HEADLAND.HT12A','HT12 binding')
must(bind.get('HM6818P')=='VMB.HUALON.HM6818P','HM6818P binding')
# CPU remains unresolved: do not claim the AMD package while the neutral 286 execution binding is gated.
for alias in ('80C286','N80L286-16/S'):
 must(bind.get(alias) is None,alias+' prematurely bound')
print(json.dumps({'pass':True,'new_functional_devices':['HT12/A','HM6818P'],'cpu_binding':'intentionally unresolved'},indent=2))
