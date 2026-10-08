#!/usr/bin/env python3
"""Emit the static-instantiation layer of a VCB from language-neutral Construction IR.
This intentionally refuses to claim executable wiring until every connection has a backend adapter.
"""
import argparse,json,re,hashlib
from pathlib import Path
ap=argparse.ArgumentParser(); ap.add_argument('construction_ir'); ap.add_argument('out'); ap.add_argument('--class-name',default='GeneratedVCBStatic'); a=ap.parse_args()
p=json.load(open(a.construction_ir,encoding='utf-8'))
if p.get('format')!='VMB-CONSTRUCTION-IR-1' or not p.get('ready'): raise SystemExit('construction IR is not ready')
def ident(x): return re.sub(r'[^A-Za-z0-9_]','_',x)
L=["' AUTO-GENERATED STATIC COMPOSITION LAYER. Board Script is canonical; do not hand-edit.",'Option Strict On','Option Explicit On','Imports System','']
cls=ident(a.class_name); L += [f'Public NotInheritable Class {cls}']
for x in p['instances']:
 c=x['implementation']['class']; L.append(f'    Private ReadOnly {ident(x["id"])} As {c}')
L += ['','    Public Sub New()']
for x in p['instances']:
 c=x['implementation']['class']; args=x.get('construction',{}).get('arguments',[])
 if args: raise SystemExit('constructor arguments not yet representable: '+x['id'])
 L.append(f'        {ident(x["id"])} = New {c}()')
L += ['    End Sub','']
# Explicitly expose compiler state rather than silently pretending connections are live.
L.append(f'    Public Shared ReadOnly Property PendingSemanticConnectionCount As Integer = {len(p["connections"])}')
L.append('    Public Shared ReadOnly Property StaticInstantiationComplete As Boolean = True')
L.append('    Public Shared ReadOnly Property ExecutableWiringComplete As Boolean = False')
L += ['End Class','']
Path(a.out).write_text('\n'.join(L),encoding='utf-8')
print(json.dumps({'out':a.out,'instances':len(p['instances']),'pending_connections':len(p['connections']),'static_instantiation_complete':True,'executable_wiring_complete':False,'sha256':hashlib.sha256(Path(a.out).read_bytes()).hexdigest()},indent=2))
