#!/usr/bin/env python3
"""VMBS CP03 component dictionary.
Component TYPE = source-specified intrinsic identity/specification only.
Placement, reference, orientation, nets, pad geometry and KiCad instance metadata are separate.
Raw CAD token stream remains byte-exact authority.
"""
import hashlib,json

# 3-bit prefix. 000..110 are direct widths. 111 uses low five bits of the
# same header byte as the count of bytes beyond 56; +1 avoids duplicate 56.
DIRECT_WIDTHS={0:8,1:16,2:24,3:32,4:40,5:48,6:56}

def choose_width(n_types:int):
    if n_types < 0: raise ValueError('negative type count')
    needed=max(1,n_types)  # token 0 reserved; types use 1..n
    for flag,bits in DIRECT_WIDTHS.items():
        if needed <= (1<<bits)-1:
            return {'mode':'direct','flag':flag,'header_byte':flag<<5,'bits':bits,'bytes':bits//8}
    # extended xxxxx = additional bytes beyond 56, encoded as 0=>+1 byte.
    for ext_code in range(32):
        nbytes=7+(ext_code+1); bits=nbytes*8
        if needed <= (1<<bits)-1:
            return {'mode':'extended','flag':7,'extension_code':ext_code,
                    'header_byte':0xE0|ext_code,'bits':bits,'bytes':nbytes}
    raise OverflowError('component dictionary exceeds VMBS 312-bit token namespace')

def _vals(ka,n): return ka.values(n) if n else []
def _cv(ka,n,h):
    c=next(ka.child_nodes(n,h),None); return _vals(ka,c)

def _source_properties(ka,n):
    out={}
    for c in ka.child_nodes(n):
        if ka.head(c)=='property':
            v=_vals(ka,c)
            if len(v)>=2 and v[0] not in ('Reference',): out[v[0]]=v[1]
    return out

def _texts(ka,n):
    ref=value=None
    for c in ka.child_nodes(n):
        v=_vals(ka,c); h=ka.head(c)
        if h=='fp_text' and len(v)>=2:
            if v[0]=='reference': ref=v[1]
            elif v[0]=='value': value=v[1]
        elif h=='property' and len(v)>=2:
            if v[0]=='Reference': ref=v[1]
            elif v[0]=='Value': value=v[1]
    return ref,value

def build(ka,roots):
    types={}; instances=[]
    stack=[x for x in roots if isinstance(x,ka.Node)]
    while stack:
        n=stack.pop(); h=ka.head(n)
        if h in ('footprint','module'):
            nv=_vals(ka,n); library=nv[0] if nv else None
            ref,value=_texts(ka,n)
            # Deliberately excludes location/orientation/layer/net/pad geometry/UUID.
            # Physical footprint atoms are accounted separately by the CAD semantic layer.
            spec={'library':library,'value':value,'properties':_source_properties(ka,n)}
            canonical=json.dumps(spec,sort_keys=True,separators=(',',':'),ensure_ascii=False)
            digest=hashlib.sha256(canonical.encode('utf-8','surrogateescape')).hexdigest()
            types.setdefault(digest,{'spec':spec,'instances':0})['instances']+=1
            wiring=[]
            for p in ka.child_nodes(n,'pad'):
                pv=_vals(ka,p)
                wiring.append({'pad':pv[0] if pv else None,'net':_cv(ka,p,'net')})
            instances.append({'ref':ref,'type_digest':digest,'at':_cv(ka,n,'at'),
                              'layer':ka.first_value(n,'layer'),'wiring':wiring})
            continue
        stack.extend(ka.child_nodes(n))
    # Human-useful deterministic order: library, value, canonical JSON, digest.
    ordered=sorted(types.items(),key=lambda kv:(str(kv[1]['spec']['library']),str(kv[1]['spec']['value']),
                                                json.dumps(kv[1]['spec'],sort_keys=True),kv[0]))
    width=choose_width(len(ordered)); digits=(width['bits']+3)//4
    id_by_digest={d:i+1 for i,(d,_) in enumerate(ordered)}
    dictionary={f'0x{i:0{digits}X}':{'sha256':d,**entry} for i,(d,entry) in enumerate(ordered,1)}
    for seq,inst in enumerate(instances,1):
        i=id_by_digest[inst.pop('type_digest')]
        inst['instance_token']=seq
        inst['type_token']=f'0x{i:0{digits}X}'
    return {'header':{'component_token':width,
                      'encoding':'000=8,001=16,010=24,011=32,100=40,101=48,110=56,111xxxxx=56+(xxxxx+1)*8'},
            'component_type_count':len(ordered),'component_instance_count':len(instances),
            'dictionary':dictionary,'instances':instances}
