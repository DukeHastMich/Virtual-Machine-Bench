#!/usr/bin/env python3
"""Lower VMB-CONSTRUCTION-IR-1 to compact, deterministic VMB-BOARD-CODE-1 pseudo-MSIL."""
import argparse,json,hashlib
from pathlib import Path
ap=argparse.ArgumentParser(); ap.add_argument('plan'); ap.add_argument('out'); a=ap.parse_args()
p=json.load(open(a.plan)); ops=[]
def emit(op,**kw): ops.append({'op':op,**kw})
# Allocation first.
for m in p['module_instances']: emit('ALLOC_MODULE',id=m['instance_id'],module_id=m['module_id'],part_id=m['part_id'])
for x in p['platform_attachments']: emit('ATTACH_PLATFORM',id=x['attachment_id'],platform=x['platform'],signals=x['signals'])
for c in p['controls']: emit('CREATE_CONTROL',id=c['control_id'],kind=c['kind'],elements=c['elements'],change_while_powered=c['change_while_powered'],storage=c['storage'])
# State/configuration next.
for b in p['configuration_bindings']: emit('BIND_CONFIG_PIN',module=b['instance_id'],pin=b['pin'],role=b['role'],sampling=b['sampling'],source=b['source'])
for d in p['decoded_properties']: emit('DEFINE_PROPERTY',module=d['instance_id'],property=d['property'],status=d['status'],allowed_values=d.get('allowed_values'),default_value=d.get('default_value'),selectors=d.get('selectors',[]),physical_irq_paths=d.get('physical_irq_paths'))
# Connectivity after all endpoints exist.
for l in p['interface_links']: emit('LINK',id=l['link_id'],net=l['net'],endpoints=l['endpoints'])
# Persistent/programmed state last.
for s in p['programmed_device_slots']: emit('CREATE_DEVICE_SLOT',**s)
emit('FINALIZE_BOARD',hardware_tits=p['substrates']['VMB_HARDWARE']['tits'],execution_tits=p['substrates']['VMB_EXECUTION']['tits'],default_execution_tits=p['substrates'].get('VMB_DEFAULT_EXECUTION',p['substrates']['VMB_EXECUTION'])['tits'])
out={'format':'VMB-BOARD-CODE-1','source_sha256':hashlib.sha256(Path(a.plan).read_bytes()).hexdigest(),'instructions':ops,'instruction_count':len(ops)}
Path(a.out).write_text(json.dumps(out,indent=2)+'\n'); print(json.dumps({'instructions':len(ops),'ops':{k:sum(1 for x in ops if x['op']==k) for k in sorted({x['op'] for x in ops})}},indent=2))
