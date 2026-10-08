#!/usr/bin/env python3
import json,re
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];VMB=ROOT.parent
def must(x,m):
 if not x:raise AssertionError(m)
code=json.load(open(ROOT/'compiler_db/code_store.json'));mods={m['module_id']:m for m in code['modules']}
mid='VMB.MEM.EPROM_27256_X8';must(mid in mods,'27256 module absent')
m=mods[mid];must(m['implementation']['status']=='behavioral_contract','wrong status')
must(m['implementation']['compile_allowed'] is True,'not compile allowed')
cp=m['configuration_points'][0];must(cp['exact_bytes']==32768,'wrong payload size')
store=json.load(open(ROOT/'compiler_db/component_store.json'));bind={}
for f in store['variant_stub_policy']['families']:
 if f.get('record_status')=='functional_variant':
  for a in f.get('variants',[]):bind[a.upper()]=f.get('vmb_binding',{}).get('module_id')
for a in ('27256','HN27256','HN27C256','TMM27256','AM27C256','ΜPD27256','µPD27256'):
 if a.upper() in bind:must(bind[a.upper()]==mid,a+' wrong binding')
fox=(VMB/'Motherboards/Octek/FOX II 286 Rev 3.2/FoxII286Rev32Board.vb').read_text(errors='ignore')
mem=(VMB/'Hardware/Chipsets/Headland/HT12/Ht12MemoryFabric.vb').read_text(errors='ignore')
must('paired 27256 lanes' in fox,'FOX 27256 witness absent')
must('evenLaneInBed.Length <> &H8000' in mem and 'oddLaneInBed.Length <> &H8000' in mem,'FOX lane size witness absent')
must('SystemRomSizeInBed As Integer = &H10000' in mem,'FOX 64K linear ROM witness absent')
# Do not pretend physical DRAM specimens are known: source only identifies capacity classes/strap codes.
must('41256' not in fox and '41464' not in fox,'unexpected physical DRAM specimen claim')
print(json.dumps({'pass':True,'module':mid,'fox_rom_sockets':2,'bytes_per_socket':32768,'linear_bios_bytes':65536},indent=2))
