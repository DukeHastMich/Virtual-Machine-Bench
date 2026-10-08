#!/usr/bin/env python3
"""Compile VMBS Board IR into implementation-neutral VMB construction IR.
No board identity shortcuts: all instances, links, controls and payload slots come from Board IR.
"""
import argparse, json, hashlib
from pathlib import Path

def stable(obj):
    return json.dumps(obj, sort_keys=True, separators=(',', ':'))

def main():
    ap=argparse.ArgumentParser()
    ap.add_argument('board_ir'); ap.add_argument('code_store'); ap.add_argument('out')
    a=ap.parse_args()
    ir=json.load(open(a.board_ir)); code=json.load(open(a.code_store))
    known={m['module_id']:m for m in code['modules']}
    diag=[]

    # Modules are created solely from Analyzer bindings.
    modules=[]
    for b in sorted(ir.get('module_bindings',[]), key=lambda x:x['ref']):
        mid=b['module_id']; contract=known.get(mid)
        if not b.get('resolved') or contract is None:
            diag.append({'severity':'error','code':'CONSTRUCTOR_MODULE_UNRESOLVED','ref':b['ref'],'module_id':mid})
        impl=(contract or {}).get('implementation',{})
        if contract is not None and (impl.get('status')=='stub' or impl.get('compile_allowed') is False):
            msg=impl.get('error_message') or "VMB module is a deliberate stub."
            print(f"\nERROR: {mid} required by {b['ref']} is registered but not implemented.\n{msg}\n")
            raise SystemExit(2)
        modules.append({'instance_id':b['ref'],'part_id':b['part_id'],'module_id':mid,
                        'implementation_status':impl.get('status','UNRESOLVED')})

    # Physical controls become machine-PCB-instance controls.
    controls=[]
    for c in sorted(ir.get('controls',[]), key=lambda x:x['ref']):
        controls.append({'control_id':c['ref'],'kind':c['kind'],'elements':c['count'],
                         'change_while_powered':c['change_while_powered'],'storage':'MACHINE_PCB_INSTANCE'})

    # Pin configuration retains temporal semantics and source kind.
    cfg=[]
    for mc in sorted(ir.get('module_configuration',[]), key=lambda x:x['ref']):
        for p in mc.get('pin_configuration',[]):
            src=p['source']; item={'instance_id':mc['ref'],'module_id':mc['module_id'],'pin':p['pin'],
                                  'role':p['role'],'sampling':p['sampling'],'source':src}
            cfg.append(item)
            if src.get('kind')=='CONNECTED':
                diag.append({'severity':'error','code':'CONSTRUCTOR_CONFIG_SOURCE_UNRESOLVED','ref':mc['ref'],'pin':p['pin']})

    # Decoded properties are declarative domains; do not choose user settings.
    props=[]
    for d in sorted(ir.get('configuration_decode',[]), key=lambda x:(x['ref'],x['property'])):
        x={'instance_id':d['ref'],'module_id':d['module_id'],'property':d['property'],'status':d['status'],
           'selectors':d.get('selectors',[])}
        if 'allowed_values' in d: x['allowed_values']=d['allowed_values']
        if 'default_value' in d: x['default_value']=d['default_value']
        props.append(x)
        if d['status']=='UNRESOLVED':
            diag.append({'severity':'error','code':'CONSTRUCTOR_PROPERTY_UNRESOLVED','ref':d['ref'],'property':d['property']})

    # Platform attachments are generated from Analyzer-recognized buses.
    platforms=[]
    for b in sorted(ir.get('buses',[]), key=lambda x:(x['platform'],x['ref'])):
        platforms.append({'attachment_id':b['ref'],'platform':b['platform'],
                          'signals':sorted([{'signal':s['signal'],'net':s['net'],'pad':s['pad']} for s in b['signals']],key=lambda x:(x['signal'],str(x['pad'])))})

    # Preserve semantic links as named nets. Constructor does not reinterpret copper.
    links=[]
    for l in sorted(ir.get('interface_links',[]), key=lambda x:(x['net_id'],x['net'])):
        eps=[]
        for e in l['endpoints']:
            q={'kind':e['kind'],'instance_id':e['ref'],'interface':e.get('interface'),'signal':e.get('signal'),'pad':e.get('pad')}
            if e.get('direction') is not None:q['direction']=e['direction']
            if e.get('platform') is not None:q['platform']=e['platform']
            if e.get('module_id') is not None:q['module_id']=e['module_id']
            eps.append(q)
        links.append({'link_id':f"NET{l['net_id']}",'net':l['net'],'endpoints':eps})

    # Cross-check decoded IRQ domains against actual semantic ISA IRQ output links.
    # This is generic: property name IRQ plus interface signal IRQ<n>; no specimen refs or values are encoded.
    irq_paths={}
    for l in links:
        for e in l['endpoints']:
            if e['kind']=='MODULE_PIN' and e.get('interface') and e.get('signal','').startswith('IRQ'):
                if any(x['kind']=='PLATFORM_PIN' and x.get('interface')==e.get('interface') and x.get('signal')==e.get('signal') for x in l['endpoints']):
                    try: irq=int(e['signal'][3:])
                    except ValueError: continue
                    irq_paths.setdefault(e['instance_id'],set()).add(irq)
    for d in props:
        if d['property']=='IRQ' and d.get('status')!='UNRESOLVED':
            allowed={int(x) for x in d.get('allowed_values',[]) if isinstance(x,(int,float))}
            physical=irq_paths.get(d['instance_id'],set())
            missing=sorted(allowed-physical)
            d['physical_irq_paths']=sorted(physical)
            if missing:
                diag.append({'severity':'error','code':'DECODED_IRQ_HAS_NO_PHYSICAL_PATH','ref':d['instance_id'],'irq':missing})

    # Programmed contents become machine-local slots. Missing firmware is execution-incomplete, not hardware-invalid.
    slots=[]; exec_diag=[]
    for p in sorted(ir.get('programmed_contents',[]), key=lambda x:x['ref']):
        q={'slot_id':p['ref'],'instance_id':p['ref'],'part_id':p['part_id'],'kind':p['kind'],
           'payload_id':p.get('payload_id'),'installation':p['installation'],'storage':'MACHINE_PCB_INSTANCE'}
        if 'size_bytes' in p:q['size_bytes']=p['size_bytes']
        if 'size_bits' in p:q['size_bits']=p['size_bits']
        # Derive optional boot-ROM payload condition from semantic links rather than board identity.
        if p['kind']=='firmware':
            peers=[]
            for l in links:
                if any(e['kind']=='MODULE_PIN' and e['instance_id']==p['ref'] for e in l['endpoints']):
                    peers += [e for e in l['endpoints'] if e['kind']=='MODULE_PIN' and e['instance_id']!=p['ref'] and e.get('interface')=='BOOT_ROM']
            peer_ids=sorted({e['instance_id'] for e in peers})
            if len(peer_ids)==1:
                prop=next((d for d in props if d['instance_id']==peer_ids[0] and d['property']=='BOOT_ROM'),None)
                if prop:
                    q['required_when']={'module':peer_ids[0],'property':'BOOT_ROM','predicate':{'enabled':True}}
                    q['default_required']=bool(isinstance(prop.get('default_value'),dict) and prop['default_value'].get('enabled'))
        slots.append(q)
        if p['kind']=='firmware' and not p.get('payload_id'):
            exec_diag.append({'severity':'incomplete','code':'FIRMWARE_PAYLOAD_REQUIRED_FOR_FULL_CONFIGURATION','ref':p['ref'],'required_when':q.get('required_when')})

    # Generic required-interface sanity: every module interface link must point to a declared module instance.
    mids={m['instance_id'] for m in modules}
    for l in links:
        for e in l['endpoints']:
            if e['kind']=='MODULE_PIN' and e['instance_id'] not in mids:
                diag.append({'severity':'error','code':'LINK_MODULE_INSTANCE_MISSING','link_id':l['link_id'],'ref':e['instance_id']})

    errors=[d for d in diag if d['severity']=='error']
    plan={'format':'VMB-CONSTRUCTION-IR-1',
          'source':{'board_ir_format':ir.get('format'),'sha256':hashlib.sha256(Path(a.board_ir).read_bytes()).hexdigest()},
          'substrates':{'VMB_HARDWARE':{'tits':1 if errors else 0,'complete':not errors},
                        'VMB_EXECUTION':{'tits':1 if errors or exec_diag else 0,'complete':not(errors or exec_diag)},
                        'VMB_DEFAULT_EXECUTION':{'tits':1 if errors or any(s.get('default_required') and not s.get('payload_id') for s in slots) else 0,'complete':not(errors or any(s.get('default_required') and not s.get('payload_id') for s in slots))}},
          'module_instances':modules,'platform_attachments':platforms,'controls':controls,
          'configuration_bindings':cfg,'decoded_properties':props,'interface_links':links,
          'programmed_device_slots':slots,'diagnostics':diag+exec_diag,
          'summary':{'modules':len(modules),'platforms':len(platforms),'controls':len(controls),
                     'configuration_bindings':len(cfg),'decoded_properties':len(props),
                     'interface_links':len(links),'programmed_device_slots':len(slots),
                     'hardware_errors':len(errors),'execution_incomplete':len(exec_diag)}}
    Path(a.out).write_text(json.dumps(plan,indent=2)+'\n')
    print(json.dumps(plan['summary'],indent=2)); print('VMB HARDWARE TITS:', 'UP' if errors else 'DOWN'); print('VMB EXECUTION TITS:', 'UP' if errors or exec_diag else 'DOWN')
if __name__=='__main__': main()
