#!/usr/bin/env python3
import argparse,json,hashlib,math,importlib.util
import component_dictionary as cd
import physical_dictionary as pd
import semantic_promote as sp
import semantic_coverage_cp06 as sc
from pathlib import Path
HERE=Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('ka', HERE/'kicad_atomizer.py'); ka=importlib.util.module_from_spec(spec); spec.loader.exec_module(ka)

def vals(n): return ka.values(n)
def cv(n,h):
 c=next(ka.child_nodes(n,h),None); return vals(c) if c else []
def f(x,default=0.0):
 try:return float(x)
 except:return default

def absxy(fp_at,pad_at):
 fx,fy=(f(fp_at[0]),f(fp_at[1])) if len(fp_at)>=2 else (0,0); fr=f(fp_at[2]) if len(fp_at)>=3 else 0
 px,py=(f(pad_at[0]),f(pad_at[1])) if len(pad_at)>=2 else (0,0)
 r=math.radians(fr); return [fx+px*math.cos(r)-py*math.sin(r), fy+px*math.sin(r)+py*math.cos(r)]

def holes(roots):
 out=[]; seq=0
 def add(kind,xy,drill,owner=None,pad=None,plated=None,shape=None):
  nonlocal seq
  if not drill:return
  seq+=1; d=[f(x) if x not in ('oval',) else x for x in drill]
  ds='oval' if drill and drill[0]=='oval' else 'round'; nums=[x for x in d if isinstance(x,float)]
  dims=nums[:2] if ds=='oval' else nums[:1]
  out.append({'token':f'H{seq:04X}','kind':kind,'x_mm':round(xy[0],6),'y_mm':round(xy[1],6),'drill_shape':ds,'drill_mm':dims,'owner':owner,'pad':pad,'plated':plated,'pad_shape':shape})
 for root in roots:
  stack=[root] if isinstance(root,ka.Node) else []
  while stack:
   n=stack.pop(); h=ka.head(n)
   if h in ('footprint','module'):
    fat=cv(n,'at'); ref=None
    for c in ka.child_nodes(n):
     vv=vals(c)
     if ka.head(c)=='fp_text' and len(vv)>=2 and vv[0]=='reference':ref=vv[1]
     if ka.head(c)=='property' and len(vv)>=2 and vv[0]=='Reference':ref=vv[1]
    for p in ka.child_nodes(n,'pad'):
     pv=vals(p); pat=cv(p,'at'); dr=cv(p,'drill'); ptype=pv[1] if len(pv)>1 else None; pshape=pv[2] if len(pv)>2 else None
     if dr:
      add('pad_hole',absxy(fat,pat),dr,ref,pv[0] if pv else None,ptype!='np_thru_hole',pshape)
    # don't descend into footprint, avoids double counting pads
    for c in ka.child_nodes(n):
     if ka.head(c) not in ('pad',): stack.append(c)
   elif h=='via':
    add('via', [f(x) for x in cv(n,'at')[:2]], cv(n,'drill'), plated=True)
    for c in ka.child_nodes(n):stack.append(c)
   else:
    for c in ka.child_nodes(n):stack.append(c)
 return out

def main():
 ap=argparse.ArgumentParser();ap.add_argument('cad');ap.add_argument('-o','--out',default='analysis');a=ap.parse_args();p=Path(a.cad);data=p.read_bytes();o=Path(a.out);o.mkdir(parents=True,exist_ok=True)
 roots=ka.parse_sexpr(data); dictionary,stream=ka.encode_dictionary(data); hs=holes(roots); components=cd.build(ka,roots); physical=pd.build(ka,roots); promoted=sp.build(ka,roots); coverage=sc.audit(ka,roots)
 # byte-exact reconstruction from token[data]
 rec=bytearray()
 for typ,v,s,e in stream:
  if typ in (3,4): rec.extend(dictionary[v])
  else: rec.extend(v.encode('utf-8','surrogateescape'))
 recon=bytes(rec); ok=(recon==data)
 hole_tokens={x['token']:x for x in hs}
 manifest={'format':'VMBS-ATOM-CP06','source':p.name,'bytes':len(data),'source_sha256':hashlib.sha256(data).hexdigest(),'reconstructed_sha256':hashlib.sha256(recon).hexdigest(),'byte_exact_reconstruction':ok,'unaccounted_bytes':0 if ok else None,'dictionary_entries':len(dictionary),'tokens':len(stream),'holes':{'count':len(hs),'token_data':hole_tokens},'components':components,'physical':physical,'semantic_promoted':promoted,'semantic_coverage':coverage,'semantic':ka.semantic_inventory(roots)}
 (o/(p.name+'.full.manifest.json')).write_text(json.dumps(manifest,indent=2))
 (o/(p.name+'.tokens.json')).write_text(json.dumps(stream,separators=(',',':')),encoding='utf-8',errors='surrogateescape')
 with (o/(p.name+'.dict.hex')).open('w',encoding='ascii') as z:
  for i,w in enumerate(dictionary):z.write(f'{i:08X} {w.hex()}\n')
 (o/(p.name+'.reconstructed')).write_bytes(recon)
 print(json.dumps({'byte_exact':ok,'source_sha256':manifest['source_sha256'],'reconstructed_sha256':manifest['reconstructed_sha256'],'tokens':len(stream),'dictionary_entries':len(dictionary),'holes':len(hs),'component_types':components['component_type_count'],'component_instances':components['component_instance_count'],'component_token_width_bits':components['header']['component_token']['bits'],'component_header_byte':components['header']['component_token']['header_byte'],'physical':{k:{'types':v['type_count'],'occurrences':v['occurrence_count'],'bits':v['token_width']['bits']} for k,v in physical.items()},'semantic_coverage':{k:coverage[k] for k in ('node_occurrences','unique_heads','semantic_occurrences','structural_occurrences','preserved_only_occurrences','preserved_only_heads')},'unaccounted_bytes':manifest['unaccounted_bytes']},indent=2))
if __name__=='__main__':main()
