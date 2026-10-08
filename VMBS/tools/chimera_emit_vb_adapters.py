#!/usr/bin/env python3
"""Emit executable VB glue only for protocol pairs whose source-backed composition contracts prove both sides.
No board names, part IDs, or adapter names are special-cased.
"""
import argparse,json,re
from pathlib import Path
ap=argparse.ArgumentParser(); ap.add_argument('construction_ir'); ap.add_argument('adapter_ir'); ap.add_argument('code_store'); ap.add_argument('out'); ap.add_argument('--class-name',default='GeneratedVCBAdapters'); a=ap.parse_args()
ci=json.load(open(a.construction_ir)); ai=json.load(open(a.adapter_ir)); store=json.load(open(a.code_store)); mods={m['module_id']:m for m in store['modules']}
assert ci['format']=='VMB-CONSTRUCTION-IR-1' and ai['format']=='VMB-ADAPTER-IR-1'
def ident(x): return re.sub(r'[^A-Za-z0-9_]','_',x)
instances={x['id']:x for x in ci['instances']}
# Join Adapter IR groups back to their reusable endpoint contracts.
groups=[]
for g in ai['adapter_groups']:
 rec=mods[instances[g['instance_id']]['module_id']]
 ep=next((e for e in rec.get('composition',{}).get('endpoints',[]) if e.get('interface')==g['interface']),None)
 groups.append((g,(ep or {}).get('binding',{})))
# Generic 4-wire Microwire-style semantic pairing: producer has event+sample sink; device has drive method+read property.
pairs=[]
covered_nets=set()
for ga,ba in groups:
 if not {'CS','CLK','DI','DO'} <= set(ga.get('signals',{})): continue
 if not ba.get('drive_event') or not ba.get('sample_method'): continue
 for gb,bb in groups:
  if ga is gb or gb['interface']!=ga['interface']: continue
  if not bb.get('drive_method') or not bb.get('read_property'): continue
  if not all(set(ga['signals'][s]) & set(gb.get('signals',{}).get(s,[])) for s in ('CS','CLK','DI','DO')): continue
  pairs.append((ga,ba,gb,bb))
  for sig in ('CS','CLK','DI','DO'): covered_nets.update(set(ga['signals'][sig]) & set(gb['signals'][sig]))
L=["' AUTO-GENERATED EXECUTABLE ADAPTER LAYER. Board Script is canonical; do not hand-edit.",'Option Strict On','Option Explicit On','Imports System','',f'Public NotInheritable Class {ident(a.class_name)}']
for x in ci['instances']: L.append(f'    Private ReadOnly {ident(x["id"])} As {x["implementation"]["class"]}')
L += ['','    Public Sub New()']
for x in ci['instances']:
 if x.get('construction',{}).get('arguments'): raise SystemExit('constructor arguments not yet representable')
 L.append(f'        {ident(x["id"])} = New {x["implementation"]["class"]}()')
for i,(ga,ba,gb,bb) in enumerate(pairs):
 L.append(f'        AddHandler {ident(ga["instance_id"])}.{ba["drive_event"]}, AddressOf ProtocolLink_{i}')
 L.append(f'        {ident(ga["instance_id"])}.{ba["sample_method"]}({ident(gb["instance_id"])}.{bb["read_property"]})')
L += ['    End Sub','']
for i,(ga,ba,gb,bb) in enumerate(pairs):
 src=ident(ga['instance_id']); dst=ident(gb['instance_id'])
 L += [f'    Private Sub ProtocolLink_{i}(chipSelect As Boolean, clock As Boolean, dataIn As Boolean)',
       f'        {dst}.{bb["drive_method"]}(chipSelect, clock, dataIn)',
       f'        {src}.{ba["sample_method"]}({dst}.{bb["read_property"]})',
       '    End Sub','']
L.append(f'    Public Shared ReadOnly Property ExecutableProtocolLinkCount As Integer = {len(pairs)}')
L.append(f'    Public Shared ReadOnly Property CoveredProtocolNetCount As Integer = {len(covered_nets)}')
L.append(f'    Public Shared ReadOnly Property PendingSemanticConnectionCount As Integer = {max(0,len(ci["connections"])-len(covered_nets))}')
L += ['End Class','']
Path(a.out).write_text('\n'.join(L))
print(json.dumps({'out':a.out,'protocol_links':len(pairs),'covered_protocol_nets':len(covered_nets),'pending_semantic_connections':max(0,len(ci['connections'])-len(covered_nets))},indent=2))
