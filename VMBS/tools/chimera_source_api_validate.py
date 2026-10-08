#!/usr/bin/env python3
"""Honesty check: composition contracts may not name direct source members that do not exist."""
import argparse,json,re
from pathlib import Path
ap=argparse.ArgumentParser(); ap.add_argument('manifest'); ap.add_argument('code_store'); ap.add_argument('vmb_root'); ap.add_argument('out'); a=ap.parse_args()
manifest=json.load(open(a.manifest,encoding='utf-8')); store=json.load(open(a.code_store,encoding='utf-8')); mods={m['module_id']:m for m in store['modules']}; root=Path(a.vmb_root)
errs=[]; checked=[]
member_keys={'read','write','register_read','register_write','dma_read','dma_write','irq_event','event','snapshot_method','drive_method','read_property','drive_event','sample_method','cs_property','clock_property','di_property'}
for inst in manifest['instances']:
 mid=inst['module_id']; rec=mods[mid]; impl=rec.get('implementation',{}); src=impl.get('source'); cls=impl.get('class')
 if not src: continue
 path=root/src
 if not path.exists(): errs.append({'code':'SOURCE_MISSING','module_id':mid,'source':src}); continue
 text=path.read_text(encoding='utf-8-sig')
 # Directly named members only. Adapter names are contracts for generated glue, not source members.
 names=[]
 comp=rec.get('composition',{})
 for ep in comp.get('endpoints',[]):
  b=ep.get('binding',{})
  for k,v in b.items():
   if k in member_keys and isinstance(v,str): names.append((k,v))
 for hs in comp.get('host_services',[]):
  for k,v in hs.items():
   if k in member_keys and isinstance(v,str): names.append((k,v))
 st=comp.get('state') or {}
 if isinstance(st.get('snapshot_method'),str): names.append(('snapshot_method',st['snapshot_method']))
 missing=[]
 for kind,name in sorted(set(names)):
  if not re.search(r'\b(?:Sub|Function|Event|Property)\s+'+re.escape(name)+r'\b',text,re.I): missing.append({'binding':kind,'member':name})
 if missing: errs.append({'code':'COMPOSITION_SOURCE_MEMBER_MISSING','module_id':mid,'class':cls,'missing':missing})
 checked.append({'module_id':mid,'class':cls,'source':src,'direct_members':[n for _,n in sorted(set(names))]})
out={'format':'VMB-CHIMERA-SOURCE-API-VALIDATION-1','checked':checked,'errors':errs,'ready':not errs}
Path(a.out).write_text(json.dumps(out,indent=2)+'\n',encoding='utf-8'); print(json.dumps({'out':a.out,'checked':len(checked),'ready':not errs,'errors':errs},indent=2))
