#!/usr/bin/env python3
"""Emit VB topology metadata from VMB-GLUE-IR-1 without runtime silicon allocation/link calls."""
import argparse,json,re,hashlib
from pathlib import Path
ap=argparse.ArgumentParser(); ap.add_argument('glue_ir'); ap.add_argument('out'); ap.add_argument('--class-name',default='GeneratedVCBTopology'); a=ap.parse_args()
p=json.load(open(a.glue_ir,encoding='utf-8'))
if p.get('format')!='VMB-GLUE-IR-1' or not p.get('ready'): raise SystemExit('glue IR not ready')
def ident(x): return re.sub(r'[^A-Za-z0-9_]','_',x)
def vb(s): return '"'+str(s).replace('"','""')+'"'
L=["' AUTO-GENERATED TOPOLOGY LAYER. Derived from Board Script; do not hand-edit.",'Option Strict On','Option Explicit On','Imports System','Imports System.Collections.Generic','',f'Public NotInheritable Class {ident(a.class_name)}',
'    Public NotInheritable Class Endpoint','        Public ReadOnly Kind As String','        Public ReadOnly InstanceId As String','        Public ReadOnly InterfaceName As String','        Public ReadOnly Signal As String','        Public Sub New(k As String, i As String, f As String, s As String)','            Kind=k : InstanceId=i : InterfaceName=f : Signal=s','        End Sub','    End Class','    Public NotInheritable Class Net','        Public ReadOnly Name As String','        Public ReadOnly Operation As String','        Public ReadOnly Endpoints As Endpoint()','        Public Sub New(n As String, o As String, e As Endpoint())','            Name=n : Operation=o : Endpoints=e','        End Sub','    End Class','', '    Public Shared Function BuildTopology() As Net()','        Return New Net() {']
for ni,x in enumerate(p['operations']):
 eps=[]
 for e in x['endpoints']:
  eps.append(f'New Endpoint({vb(e.get("kind",""))}, {vb(e.get("instance_id",""))}, {vb(e.get("interface",""))}, {vb(e.get("signal",""))})')
 comma=',' if ni<len(p['operations'])-1 else ''
 L.append(f'            New Net({vb(x["net"])}, {vb(x["operation"])}, New Endpoint() {{{", ".join(eps)}}}){comma}')
L += ['        }','    End Function','',f'    Public Shared ReadOnly Property NetCount As Integer = {len(p["operations"])}','End Class','']
Path(a.out).write_text('\n'.join(L),encoding='utf-8')
print(json.dumps({'out':a.out,'nets':len(p['operations']),'sha256':hashlib.sha256(Path(a.out).read_bytes()).hexdigest()},indent=2))
