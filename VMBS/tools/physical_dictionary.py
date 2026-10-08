#!/usr/bin/env python3
"""VMBS CP04 physical primitive dictionaries.
Canonical definitions contain reusable physical specification; occurrence records contain
location/connectivity/ownership. Raw lexical token stream remains byte-exact authority.
"""
import json, hashlib
from collections import defaultdict

def _vals(ka,n): return ka.values(n) if n else []
def _children(ka,n,h=None): return list(ka.child_nodes(n,h))
def _cv(ka,n,h):
    c=next(ka.child_nodes(n,h),None); return _vals(ka,c)
def _head(ka,n): return ka.head(n)
def _f(v):
    try:return float(v)
    except:return v

def _token_width(count):
    # same cardinality-first token ladder as component dictionary
    widths=[8,16,24,32,40,48,56]
    for flag,bits in enumerate(widths):
        if max(1,count) <= (1<<bits)-1:
            return {'flag':flag,'header_byte':flag<<5,'bits':bits,'bytes':bits//8}
    for ext in range(32):
        bits=(8+ext)*8
        if max(1,count) <= (1<<bits)-1:
            return {'flag':7,'extension_code':ext,'header_byte':0xE0|ext,'bits':bits,'bytes':8+ext}
    raise OverflowError('dictionary exceeds 312-bit namespace')

def _canon(obj): return json.dumps(obj,sort_keys=True,separators=(',',':'),ensure_ascii=False)
def _digest(obj): return hashlib.sha256(_canon(obj).encode('utf-8','surrogateescape')).hexdigest()

class DictBuilder:
    def __init__(self): self.types={}; self.occ=[]
    def add(self,spec,occ):
        d=_digest(spec)
        self.types.setdefault(d,{'spec':spec,'instances':0})['instances']+=1
        self.occ.append((d,occ))
    def finish(self):
        ordered=sorted(self.types.items(),key=lambda kv:(_canon(kv[1]['spec']),kv[0]))
        width=_token_width(len(ordered)); digits=(width['bits']+3)//4
        ids={d:i+1 for i,(d,_) in enumerate(ordered)}
        dictionary={f'0x{i:0{digits}X}':{'sha256':d,**entry} for i,(d,entry) in enumerate(ordered,1)}
        occurrences=[]
        for seq,(d,o) in enumerate(self.occ,1):
            occurrences.append({'occurrence_token':seq,'type_token':f'0x{ids[d]:0{digits}X}',**o})
        return {'type_count':len(ordered),'occurrence_count':len(occurrences),'token_width':width,
                'dictionary':dictionary,'occurrences':occurrences}

def _fp_ref(ka,n):
    for c in ka.child_nodes(n):
        v=_vals(ka,c); h=_head(ka,c)
        if h=='fp_text' and len(v)>=2 and v[0]=='reference': return v[1]
        if h=='property' and len(v)>=2 and v[0]=='Reference': return v[1]
    return None

def _pad_spec_occ(ka,p,owner):
    pv=_vals(ka,p); num=pv[0] if pv else None; ptype=pv[1] if len(pv)>1 else None; shape=pv[2] if len(pv)>2 else None
    spec={'type':ptype,'shape':shape,'size':list(map(_f,_cv(ka,p,'size'))),'drill':[_f(x) for x in _cv(ka,p,'drill')],
          'layers':_cv(ka,p,'layers')}
    # preserve source-specified pad fabrication/electrical modifiers as type properties
    for h in ('roundrect_rratio','chamfer_ratio','chamfer','solder_mask_margin','solder_paste_margin','solder_paste_margin_ratio','clearance','zone_connect','thermal_width','thermal_gap'):
        v=_cv(ka,p,h)
        if v: spec[h]=[_f(x) for x in v]
    occ={'owner':owner,'pad':num,'at':[_f(x) for x in _cv(ka,p,'at')], 'net':_cv(ka,p,'net')}
    return spec,occ

def _drill_spec_occ(ka,p,owner):
    dr=_cv(ka,p,'drill')
    if not dr:return None
    pv=_vals(ka,p); ptype=pv[1] if len(pv)>1 else None
    spec={'shape':'oval' if dr and dr[0]=='oval' else 'round','dimensions':[_f(x) for x in dr if x!='oval'],
          'plated':ptype!='np_thru_hole'}
    return spec,{'owner':owner,'pad':pv[0] if pv else None,'at':[_f(x) for x in _cv(ka,p,'at')]}

def _graphics_spec_occ(ka,n,owner=None):
    h=_head(ka,n); layer=ka.first_value(n,'layer')
    spec={'kind':h,'layer':layer}
    for key in ('stroke','width','fill','effects','locked'):
        cs=_children(ka,n,key)
        if cs: spec[key]=[serialize_node(ka,c,exclude={'tstamp','uuid'}) for c in cs]
    occ={'owner':owner}
    for key in ('start','end','mid','center','at','pts'):
        cs=_children(ka,n,key)
        if cs: occ[key]=[serialize_node(ka,c,exclude={'tstamp','uuid'}) for c in cs]
    vals=_vals(ka,n)
    if vals: occ['values']=vals
    return spec,occ

def serialize_node(ka,n,exclude=set()):
    h=_head(ka,n)
    if h in exclude:return None
    arr=[h]
    for x in n.items[1:]:
        if isinstance(x,ka.Node):
            z=serialize_node(ka,x,exclude)
            if z is not None:arr.append(z)
        else: arr.append(ka.scalar_text(x))
    return arr

def build(ka,roots):
    B={k:DictBuilder() for k in ('footprints','pads','drills','segments','vias','zones','drawings')}
    stack=[x for x in roots if isinstance(x,ka.Node)]
    while stack:
        n=stack.pop(); h=_head(ka,n)
        if h in ('footprint','module'):
            ref=_fp_ref(ka,n); nv=_vals(ka,n)
            # Reusable footprint physical definition: library identity plus relative physical children.
            rel=[]
            for c in ka.child_nodes(n):
                ch=_head(ka,c)
                if ch=='pad':
                    ps,po=_pad_spec_occ(ka,c,ref); B['pads'].add(ps,po)
                    d=_drill_spec_occ(ka,c,ref)
                    if d:B['drills'].add(*d)
                    rel.append(['pad',po.get('pad'),ps,po.get('at')])
                elif ch in ('fp_line','fp_arc','fp_circle','fp_rect','fp_poly'):
                    gs,go=_graphics_spec_occ(ka,c,ref); B['drawings'].add(gs,go); rel.append([ch,gs,{k:v for k,v in go.items() if k != 'owner'}])
            fpspec={'library':nv[0] if nv else None,'relative_physical':rel}
            fpocc={'ref':ref,'at':[_f(x) for x in _cv(ka,n,'at')],'layer':ka.first_value(n,'layer')}
            B['footprints'].add(fpspec,fpocc)
            # footprint handled; don't recurse and double-count
            continue
        if h=='segment':
            spec={'width':[_f(x) for x in _cv(ka,n,'width')],'layer':ka.first_value(n,'layer')}
            occ={'start':[_f(x) for x in _cv(ka,n,'start')],'end':[_f(x) for x in _cv(ka,n,'end')],'net':_cv(ka,n,'net')}
            B['segments'].add(spec,occ)
        elif h=='via':
            spec={'size':[_f(x) for x in _cv(ka,n,'size')],'drill':[_f(x) for x in _cv(ka,n,'drill')],'layers':_cv(ka,n,'layers')}
            occ={'at':[_f(x) for x in _cv(ka,n,'at')],'net':_cv(ka,n,'net')}
            B['vias'].add(spec,occ)
            if _cv(ka,n,'drill'):
                B['drills'].add({'shape':'round','dimensions':[_f(x) for x in _cv(ka,n,'drill')],'plated':True},{'owner':'via','at':occ['at']})
        elif h=='zone':
            # settings are type; net/layer/polygon/fill geometry are occurrence
            spec={}
            occ={}
            for c in ka.child_nodes(n):
                ch=_head(ka,c)
                if ch in ('net','net_name','layer','layers','polygon','filled_polygon','filled_areas_thickness'):
                    occ.setdefault(ch,[]).append(serialize_node(ka,c,exclude={'tstamp','uuid'}))
                elif ch not in ('tstamp','uuid'):
                    spec.setdefault(ch,[]).append(serialize_node(ka,c,exclude={'tstamp','uuid'}))
            B['zones'].add(spec,occ)
        elif h in ('gr_line','gr_arc','gr_circle','gr_rect','gr_poly','gr_curve','gr_text','dimension'):
            B['drawings'].add(*_graphics_spec_occ(ka,n,None))
        for c in ka.child_nodes(n): stack.append(c)
    return {k:v.finish() for k,v in B.items()}
