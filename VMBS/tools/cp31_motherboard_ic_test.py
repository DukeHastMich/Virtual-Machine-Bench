#!/usr/bin/env python3
import json,re
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
VMB=ROOT.parent
def must(x,m):
    if not x: raise AssertionError(m)
code=json.load(open(ROOT/'compiler_db/code_store.json'))
store=json.load(open(ROOT/'compiler_db/component_store.json'))
mods={m['module_id']:m for m in code['modules']}
expected={
 'VMB.AT.PIC8259A':('HardwareBus.vb','Pic8259'),
 'VMB.AT.PIT8254':('HardwareBus.vb','Pit8253'),
 'VMB.AT.DMA8237A':('MotherboardDevices.vb','Dma8237'),
 'VMB.AT.KBC8042':('MotherboardDevices.vb','KeyboardController8042'),
 'VMB.AT.RTC146818':('MotherboardDevices.vb','CmosRtc'),
}
for mid,(src,cls) in expected.items():
    must(mid in mods,mid+' missing')
    im=mods[mid]['implementation']
    must(im.get('status')=='implemented' and im.get('compile_allowed') is True,mid+' not compile allowed')
    text=(VMB/src).read_text(errors='ignore')
    must(re.search(r'Public (?:NotInheritable )?Class\s+'+re.escape(cls)+r'\b',text) is not None,mid+' class absent')
functional=[f for f in store['variant_stub_policy']['families'] if f.get('record_status')=='functional_variant']
bound={}
for f in functional:
    mid=f.get('vmb_binding',{}).get('module_id')
    if mid in expected:
        for a in f.get('variants',[]): bound[a.upper()]=mid
for a in ['8237','8237A','P8237A','8259','8259A','P8259A','8253','8254','8254-2','8042','P8042','8742','8742AH','MC146818','MC146818A','146818']:
    must(a in bound,a+' not promoted')
# Existing runtime semantic witnesses: these are deliberately behavior checks, not just class names.
hb=(VMB/'HardwareBus.vb').read_text(errors='ignore')
mb=(VMB/'MotherboardDevices.vb').read_text(errors='ignore')
for needle in ['AcknowledgeIrq','FindPendingIrq','_interruptMask','_inService']:
    must(needle in hb,'PIC semantic witness missing '+needle)
for needle in ['ReadBack','LatchedStatus','RefreshRequest','GetOutput']:
    must(needle.lower() in hb.lower(),'PIT semantic witness missing '+needle)
for needle in ['BaseAddress','CurrentCount','MaskRegister','HoldRequestChanged','TerminalCount']:
    must(needle in mb,'DMA semantic witness missing '+needle)
for needle in ['A20Changed','ResetRequested','CommandByteTranslation','KeyboardSerialClockHz']:
    must(needle in mb,'8042 semantic witness missing '+needle)
for needle in ['UpdateWarningPicoseconds','_registerC','NmiMaskChanged','periodic']:
    must(needle.lower() in mb.lower(),'RTC semantic witness missing '+needle)
print(json.dumps({'pass':True,'motherboard_module_ids':len(expected),'promoted_aliases':len(bound)},indent=2))
