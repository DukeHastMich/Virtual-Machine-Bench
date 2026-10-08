#!/usr/bin/env python3
"""Deterministic VMBS Analyzer: VMBS-BOARD-1 -> VMB Board IR.
Runtime is bytes + stores + rules. No specimen identity shortcuts.
"""
import argparse,json,hashlib
from pathlib import Path
class Node:
 __slots__=('head','items')
 def __init__(self,h,i): self.head=h; self.items=i
 def children(self,h=None): return [x for x in self.items if isinstance(x,Node) and (h is None or x.head==h)]
 def child(self,h):
  x=self.children(h); return x[0] if x else None
 def scalars(self): return [x for x in self.items if not isinstance(x,Node)]
def decode(e,d):
 heads,atoms=d['node_heads'],d['atoms']; hid,items=e; out=[]
 for typ,val in items: out.append(atoms[val]['value'] if typ=='a' else decode(val,d))
 return Node(heads[hid],out)
def s(n,i=0,default=None):
 a=n.scalars() if n else []; return a[i] if i<len(a) else default
def component_key(m):
 lib=s(m); val=None
 for t in m.children('fp_text'):
  a=t.scalars();
  if len(a)>=2 and a[0]=='value': val=a[1]
 return json.dumps({'library':lib,'properties':{},'value':val},sort_keys=True,separators=(',',':'))
def main():
 ap=argparse.ArgumentParser();
 for x in ['bs','component_store','component_map','code_store','platform_store','out']: ap.add_argument(x)
 a=ap.parse_args(); bs=json.load(open(a.bs)); store=json.load(open(a.component_store)); cmap=json.load(open(a.component_map)); code=json.load(open(a.code_store)); platforms=json.load(open(a.platform_store))
 root=decode(bs['design'],bs['dictionaries']); records={r['part_id']:r for r in store['records']}; bykey={r['canonical_key']:r for r in store['records']}; known={m['module_id'] for m in code['modules']}
 def norm_variant(x):
  return ''.join(ch for ch in str(x or '').upper() if ch.isalnum())
 variant_index={}
 for fam in store.get('variant_stub_policy',{}).get('families',[]):
  for alias in fam.get('variants',[]):
   k=norm_variant(alias)
   if k: variant_index.setdefault(k,[]).append({'family':fam.get('family'),'class':fam.get('class'),'module_id':fam.get('vmb_binding',{}).get('module_id'),'alias':alias,'pins':fam.get('pins',[]),'vmb_binding':fam.get('vmb_binding',{}),'record_status':fam.get('record_status')})
 keytok={records[e['part_id']]['canonical_key']:e['token'] for e in cmap['dictionary'] if e['part_id'] in records}; nets={int(s(n,0)):s(n,1) for n in root.children('net')}
 devices=[]; devbyref={}; variant_semantics={}; endpoints={n:[] for n in nets}; diagnostics=[]; bindings=[]
 for m in root.children('module'):
  key=component_key(m); rec=bykey.get(key); ref=val=None
  for t in m.children('fp_text'):
   q=t.scalars();
   if len(q)>=2:
    if q[0]=='reference': ref=q[1]
    elif q[0]=='value': val=q[1]
  variant_stub=None
  if rec is None:
   hits=variant_index.get(norm_variant(val),[])
   if len(hits)==1: variant_stub=hits[0]
   elif len(hits)>1: diagnostics.append({'severity':'warning','code':'AMBIGUOUS_VARIANT_STUB','ref':ref,'value':val,'families':[x['family'] for x in hits]})
  av=m.child('at').scalars() if m.child('at') else []
  stub_part_id=(('VARIANT:'+norm_variant(val)) if variant_stub and variant_stub.get('record_status')=='functional_variant' else ('VARIANT_STUB:'+norm_variant(val))) if variant_stub else None
  d={'ref':ref,'value':val,'footprint':s(m),'at':av,'local_component_token':keytok.get(key),'part_id':rec['part_id'] if rec else stub_part_id,'module_id':rec.get('vmb_binding',{}).get('module_id') if rec else (variant_stub.get('module_id') if variant_stub else None),'variant_stub_family':variant_stub.get('family') if variant_stub else None,'pads':[]}
  for p in m.children('pad'):
   pa=p.scalars(); pn=pa[0] if pa else None; nn=p.child('net'); nid=int(s(nn,0)) if nn and s(nn,0) is not None else None; pe={'pad':pn,'net_id':nid,'net':nets.get(nid)}; d['pads'].append(pe)
   if nid is not None:endpoints.setdefault(nid,[]).append({'ref':ref,'pad':pn})
  devices.append(d); devbyref[ref]=d
  if variant_stub: variant_semantics[ref]=variant_stub
  if not rec and not variant_stub: diagnostics.append({'severity':'warning','code':'UNKNOWN_COMPONENT','ref':ref})
  if variant_stub:
   diagnostics.append({'severity':'info' if variant_stub.get('record_status')=='functional_variant' else 'warning','code':'COMPONENT_FUNCTIONAL_VARIANT' if variant_stub.get('record_status')=='functional_variant' else 'COMPONENT_VARIANT_STUB','ref':ref,'value':val,'family':variant_stub.get('family'),'module_id':variant_stub.get('module_id')})
  if d['module_id']:
   bindings.append({'ref':ref,'part_id':d['part_id'],'module_id':d['module_id'],'resolved':d['module_id'] in known})
   if d['module_id'] not in known: diagnostics.append({'severity':'error','code':'MODULE_ID_UNRESOLVED','ref':ref})
 # semantic pin lookup. Exact functional variant families can provide pin semantics without fabricating a canonical PART_ID.
 def rec_of(d):
  r=records.get(d.get('part_id'))
  if r is not None:return r
  return variant_semantics.get(d.get('ref'),{})
 pinsem={}
 for d in devices:
  r=rec_of(d)
  if r:
   for p in r.get('pins',[]): pinsem[(d['ref'],str(p['pad']))]=p
 # Generic transparent-passive traversal. It contracts only components whose store record explicitly permits it.
 def paired_pad(ref,pad):
  d=devbyref.get(ref); r=rec_of(d) if d else None; va=(r or {}).get('vmb_abstraction',{})
  if va.get('kind')!='transparent_passive_array': return None
  for x,y in va.get('pairs',[]):
   if str(pad)==x:return y
   if str(pad)==y:return x
 def net_of(ref,pad):
  d=devbyref[ref]
  q=next((x for x in d['pads'] if str(x['pad'])==str(pad)),None); return q['net_id'] if q else None
 def resolve_from(ref,pad,blocked_ref=None):
  start=net_of(ref,pad); seen=set(); todo=[start] if start is not None else []; out=[]
  while todo:
   nid=todo.pop()
   if nid in seen:continue
   seen.add(nid)
   for ep in endpoints.get(nid,[]):
    if ep['ref']==blocked_ref: continue
    pp=paired_pad(ep['ref'],ep['pad'])
    if pp is not None:
     n2=net_of(ep['ref'],pp)
     if n2 is not None and n2 not in seen: todo.append(n2)
    else: out.append({'ref':ep['ref'],'pad':ep['pad'],'pin':pinsem.get((ep['ref'],str(ep['pad']))),'net':nets.get(nid)})
  # deterministic dedupe
  uniq={ (x['ref'],str(x['pad'])):x for x in out if not (x['ref']==ref and str(x['pad'])==str(pad)) }
  return [uniq[k] for k in sorted(uniq)]
 # Platform interfaces are recognized by component class/package; pin meaning comes from platform store.
 buses=[]; platform_pin={}
 for pf in platforms['platforms']:
  for d in devices:
   r=rec_of(d); mt=pf['connector_match']
   if r.get('class')==mt.get('class') and r.get('package')==mt.get('package'):
    sig=[]
    for p in d['pads']:
     role=pf['pins'].get(str(p['pad']))
     if role: platform_pin[(d['ref'],str(p['pad']))]=role; sig.append({'pad':p['pad'],'signal':role,'net':p['net']})
    buses.append({'ref':d['ref'],'platform':pf['id'],'signals':sig})
 # User controls: retain as primitives; resolve what each contact reaches without pretending a PCB file contains switch position.
 controls=[]
 for d in devices:
  r=rec_of(d); cp=r.get('control_primitive')
  if not cp:continue
  elems=[]
  for idx,(x,y) in enumerate(cp['pairs'],1):
   def simp(z):
    q=[]
    for e in resolve_from(d['ref'],z,blocked_ref=d['ref']):
     q.append({'ref':e['ref'],'pad':e['pad'],'pin_role':(e['pin'] or {}).get('role'),'pin_name':(e['pin'] or {}).get('name'),'platform_signal':platform_pin.get((e['ref'],str(e['pad']))),'net':e['net']})
    return q
   elems.append({'index':idx,'contacts':[x,y],'side_a':simp(x),'side_b':simp(y),'state':'USER_CONFIGURED'})
  controls.append({'ref':d['ref'],'kind':cp['kind'],'count':cp['count'],'change_while_powered':cp['change_while_powered'],'elements':elems})
 # Module configuration sources: ask component semantics which pins affect VMB behavior and describe their actual source class.
 module_cfg=[]
 for b in bindings:
  d=devbyref[b['ref']]; r=rec_of(d); cfg=[]
  for p in r.get('pins',[]):
   if p.get('role') not in {'configuration_mode','pnp_force','boot_rom_select','io_base_select','irq_select','organization_select'}: continue
   pad=str(p['pad']); q=next((x for x in d['pads'] if str(x['pad'])==pad),None); net=q['net'] if q else None
   if net in ('VCC','+5V'): source={'kind':'CONSTANT','value':1}
   elif net=='GND': source={'kind':'CONSTANT','value':0}
   else:
    # A pin reached by a control through a transparent passive is USER_CONTROL; otherwise retain connection, unresolved.
    hits=[]
    for c in controls:
     for e in c['elements']:
      for side in ('side_a','side_b'):
       if any(x['ref']==d['ref'] and str(x['pad'])==pad for x in e[side]): hits.append({'control':c['ref'],'element':e['index']})
    source={'kind':'USER_CONTROL','bindings':hits} if hits else ({'kind':'INTERNAL_DEFAULT','value':p.get('reset_default')} if p.get('sampling')=='reset_latched' and 'reset_default' in p else {'kind':'CONNECTED','net':net})
   cfg.append({'pin':p['name'],'pad':pad,'role':p['role'],'sampling':p.get('sampling'),'source':source})
  module_cfg.append({'ref':b['ref'],'module_id':b['module_id'],'pin_configuration':cfg})
 # VMB-facing interface graph. This is not signal simulation: it joins declared pin intentions by BS net identity.
 # A physical pin may expose multiple behavioral facets when the component datasheet multiplexes that pin by operating phase.
 interface_endpoints={}
 for d in devices:
  r=rec_of(d)
  for p in r.get('pins',[]):
   facets=p.get('interface_facets') or ([{'interface':p['interface'],'signal':p.get('signal',p.get('name')),'direction':p.get('direction')}] if p.get('interface') else [])
   q=next((x for x in d['pads'] if str(x['pad'])==str(p['pad'])),None)
   if not q or q['net_id'] is None: continue
   for f in facets:
    interface_endpoints.setdefault(q['net_id'],[]).append({'kind':'MODULE_PIN','ref':d['ref'],'module_id':d.get('module_id'),'interface':f['interface'],'signal':f['signal'],'direction':f.get('direction'),'pad':str(p['pad'])})
 for b in buses:
  for sig in b['signals']:
   d=devbyref[b['ref']]; q=next((x for x in d['pads'] if str(x['pad'])==str(sig['pad'])),None)
   if q and q['net_id'] is not None: interface_endpoints.setdefault(q['net_id'],[]).append({'kind':'PLATFORM_PIN','ref':b['ref'],'platform':b['platform'],'interface':b['platform'],'signal':sig['signal'],'pad':str(sig['pad'])})
 interface_links=[]
 for nid,eps in sorted(interface_endpoints.items()):
  # Ignore power-only/single-ended facts; retain nets that actually relate two behavioral endpoints.
  if len(eps)<2: continue
  # deterministic dedupe
  uq={json.dumps(e,sort_keys=True):e for e in eps}; eps=[uq[k] for k in sorted(uq)]
  if len(eps)>=2: interface_links.append({'net_id':nid,'net':nets.get(nid),'endpoints':eps})
 # Component-owned configuration decoders. They consume the already-derived pin sources; no board identity is consulted.
 def pin_source_values(src,pin,controls_by_ref):
  k=src.get('kind')
  if k in ('CONSTANT','INTERNAL_DEFAULT'): return [int(src['value'])]
  if k!='USER_CONTROL': return None
  vals=set()
  # Open state uses the component pin's documented reset/default value. Closed state is inferred only when the other switch contact is a rail.
  if 'reset_default' in pin: vals.add(int(pin['reset_default']))
  for b in src.get('bindings',[]):
   c=controls_by_ref.get(b['control']); e=next((x for x in c.get('elements',[]) if x['index']==b['element']),None) if c else None
   if not e: continue
   for side in ('side_a','side_b'):
    hits=e[side]
    if any(x.get('ref')==dref and str(x.get('pad'))==str(pin['pad']) for x in hits):
     other=e['side_b' if side=='side_a' else 'side_a']
     if any(x.get('net') in ('VCC','+5V') for x in other): vals.add(1)
     if any(x.get('net')=='GND' for x in other): vals.add(0)
  return sorted(vals) if vals else None
 def row_matches(bits,pat): return len(bits)==len(pat) and all(p=='*' or b==p for b,p in zip(bits,pat))
 controls_by_ref={c['ref']:c for c in controls}
 configuration_decode=[]
 for mc in module_cfg:
  dref=mc['ref']; d=devbyref[dref]; rec=rec_of(d); src_by_name={x['pin']:x['source'] for x in mc['pin_configuration']}; pin_by_name={x['name']:x for x in rec.get('pins',[])}
  for rule in rec.get('vmb_decode_rules',[]):
   domains=[]; selectors=[]; resolvable=True
   for name in rule['inputs']:
    pin=pin_by_name.get(name); src=src_by_name.get(name)
    if not pin or not src: resolvable=False; break
    vals=pin_source_values(src,pin,controls_by_ref)
    if vals is None: resolvable=False; break
    domains.append(vals); selectors.append({'pin':name,'source':src,'possible_bits':vals})
   if not resolvable:
    configuration_decode.append({'ref':dref,'module_id':mc['module_id'],'property':rule['property'],'status':'UNRESOLVED','selectors':selectors}); continue
   import itertools
   outs=[]
   for combo in itertools.product(*domains):
    bits=''.join(str(x) for x in combo); row=next((r for r in rule['rows'] if row_matches(bits,r['bits'])),None)
    if row is not None: outs.append(row['value'])
   # canonical dedupe works for scalar or object values
   uq={json.dumps(x,sort_keys=True):x for x in outs}; outs=[uq[k] for k in sorted(uq)]
   # Default decode uses component defaults/open user controls, never an invented switch position.
   default_bits=[]; default_ok=True
   for name in rule['inputs']:
    pin=pin_by_name.get(name); src=src_by_name.get(name,{})
    if src.get('kind') in ('CONSTANT','INTERNAL_DEFAULT'): default_bits.append(int(src['value']))
    elif src.get('kind')=='USER_CONTROL' and pin is not None and 'reset_default' in pin: default_bits.append(int(pin['reset_default']))
    else: default_ok=False; break
   default_value=None
   if default_ok:
    db=''.join(str(x) for x in default_bits); dr=next((r for r in rule['rows'] if row_matches(db,r['bits'])),None)
    if dr is not None: default_value=dr['value']
   item={'ref':dref,'module_id':mc['module_id'],'property':rule['property'],'status':'FIXED' if len(outs)==1 else 'USER_SELECTABLE','selectors':selectors,'allowed_values':outs}
   if default_ok and default_value is not None: item['default_value']=default_value
   configuration_decode.append(item)
 # Programmed devices declare slots only. No firmware identity is guessed from chip identity.
 payloads=[]
 for d in devices:
  r=rec_of(d); pp=r.get('programmed_payload')
  if pp: payloads.append({'ref':d['ref'],'part_id':d['part_id'],'kind':pp['kind'],'size_bytes':pp['size_bytes'],'payload_id':None,'installation':'MANUAL_FROM_FIRMWARE_STORE_TO_MACHINE_PCB_INSTANCE'})
  elif r.get('class')=='serial_eeprom': payloads.append({'ref':d['ref'],'part_id':d['part_id'],'kind':'persistent_eeprom','size_bits':r.get('memory',{}).get('density_bits'),'payload_id':None,'installation':'MANUAL_OR_BLANK_MACHINE_PCB_INSTANCE'})
 # Required VMB configuration is incomplete if a module has behavior-affecting pins whose sources are merely connected/unresolved.
 for mc in module_cfg:
  unresolved=[x['pin'] for x in mc['pin_configuration'] if x['source']['kind']=='CONNECTED']
  if unresolved: diagnostics.append({'severity':'error','code':'MODULE_CONFIGURATION_SOURCE_UNRESOLVED','ref':mc['ref'],'pins':unresolved})
 conns=[{'net_id':nid,'name':nets[nid],'endpoints':endpoints.get(nid,[])} for nid in sorted(nets)]
 errors=[x for x in diagnostics if x['severity']=='error']
 ir={'format':'VMBS-BOARD-IR-1','source':{'format':bs['format'],'sha256':hashlib.sha256(Path(a.bs).read_bytes()).hexdigest()},'tits':1 if errors else 0,'devices':devices,'module_bindings':bindings,'module_configuration':module_cfg,'controls':controls,'configuration_decode':configuration_decode,'buses':buses,'interface_links':interface_links,'connections':conns,'programmed_contents':payloads,'diagnostics':diagnostics,'analysis_summary':{'devices':len(devices),'nets':len(nets),'controls':len(controls),'vmb_module_instances':len(bindings),'interface_links':len(interface_links),'decoded_properties':len(configuration_decode),'errors':len(errors)}}
 Path(a.out).write_text(json.dumps(ir,indent=2)+'\n'); print(json.dumps(ir['analysis_summary'],indent=2)); print('TITS:', 'UP' if ir['tits'] else 'DOWN')
if __name__=='__main__':main()
