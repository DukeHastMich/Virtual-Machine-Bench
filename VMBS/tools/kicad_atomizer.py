#!/usr/bin/env python3
"""VMBS KiCad atomizer prototype.
Lossless lexical atomization + compact dictionary-coded token stream + semantic inventory.
No KiCad library dependency; designed to preserve every source byte via token spans.
"""
from __future__ import annotations
import argparse, hashlib, json, re, struct
from collections import Counter, defaultdict
from pathlib import Path

TOKEN_RE = re.compile(rb'''(?P<ws>\s+)|(?P<lp>\()|(?P<rp>\))|(?P<string>"(?:\\.|[^"\\])*")|(?P<atom>[^\s()]+)''', re.S)

class Node:
    __slots__=("items", "start", "end")
    def __init__(self,start=0): self.items=[]; self.start=start; self.end=start

def tokenize(data: bytes):
    pos=0
    for m in TOKEN_RE.finditer(data):
        if m.start()!=pos: raise ValueError(f"unparsed bytes at {pos}")
        pos=m.end(); yield m.lastgroup, m.group(), m.start(), m.end()
    if pos!=len(data): raise ValueError(f"unparsed tail at {pos}")

def parse_sexpr(data: bytes):
    roots=[]; stack=[]
    for typ,val,s,e in tokenize(data):
        if typ=='ws': continue
        if typ=='lp':
            n=Node(s)
            if stack: stack[-1].items.append(n)
            else: roots.append(n)
            stack.append(n)
        elif typ=='rp':
            if not stack: raise ValueError(f"unmatched ) at {s}")
            stack[-1].end=e; stack.pop()
        else:
            item=(typ,val.decode('utf-8','surrogateescape'),s,e)
            if stack: stack[-1].items.append(item)
            else: roots.append(item)
    if stack: raise ValueError("unclosed (")
    return roots

def head(n):
    if not isinstance(n,Node) or not n.items: return None
    x=n.items[0]
    return x[1] if isinstance(x,tuple) else None

def scalar_text(x):
    if not isinstance(x,tuple): return None
    v=x[1]
    if x[0]=='string':
        try: return json.loads(v)
        except Exception: return v[1:-1]
    return v

def child_nodes(n, h=None):
    for x in n.items:
        if isinstance(x,Node) and (h is None or head(x)==h): yield x

def values(n):
    return [scalar_text(x) for x in n.items[1:] if isinstance(x,tuple)]

def first_value(n, h):
    c=next(child_nodes(n,h),None)
    vs=values(c) if c else []
    return vs[0] if vs else None

def semantic_inventory(roots):
    counts=Counter(); heads=Counter(); refs=[]; nets={}; layers=[]; footprints=[]; segments=0; vias=0; zones=0; pads=0
    stack=[x for x in roots if isinstance(x,Node)]
    while stack:
        n=stack.pop(); h=head(n); heads[h]+=1
        if h in ('footprint','module'):
            footprints.append({'lib': (values(n)[0] if values(n) else None), 'ref': None, 'value':None, 'layer':first_value(n,'layer'), 'at': values(next(child_nodes(n,'at'),Node()))})
            fp=footprints[-1]
            # legacy fp_text or modern property
            for c in child_nodes(n):
                ch=head(c); vs=values(c)
                if ch=='fp_text' and len(vs)>=2:
                    if vs[0]=='reference': fp['ref']=vs[1]
                    elif vs[0]=='value': fp['value']=vs[1]
                elif ch=='property' and len(vs)>=2:
                    if vs[0]=='Reference': fp['ref']=vs[1]
                    elif vs[0]=='Value': fp['value']=vs[1]
        elif h=='net':
            vs=values(n)
            if len(vs)>=2: nets[str(vs[0])]=vs[1]
        elif h=='layer':
            vs=values(n)
            if vs: layers.append(vs)
        elif h=='segment': segments+=1
        elif h=='via': vias+=1
        elif h=='zone': zones+=1
        elif h=='pad': pads+=1
        for c in child_nodes(n): stack.append(c)
    # component/function hints only from CAD facts, not invented behavior
    classes=Counter()
    for fp in footprints:
        text=' '.join(str(fp.get(k) or '') for k in ('lib','ref','value')).lower()
        for label, pats in {
            'connector':['connector','conn_','isa','edge'], 'resistor':['resistor',' r_','r_'], 'capacitor':['capacitor',' c_','c_'],
            'logic':['74hc','74ls','74hct','74f','74als'], 'memory':['sram','dram','eeprom','flash','rom'],
            'network':['rtl8019','ethernet','ne2000'], 'audio':['es1868','audio','codec'], 'clock':['crystal','oscillator','xtal'],
            'jumper_switch':['jumper','switch','dip'], 'diode_led':['diode','led'], 'transistor':['transistor','mosfet']}.items():
            if any(p in text for p in pats): classes[label]+=1
    return {'heads':dict(heads.most_common()), 'board':{'footprints':len(footprints),'pads':pads,'nets':len(nets),'segments':segments,'vias':vias,'zones':zones,'layers_seen':len(layers)}, 'component_classes':dict(classes), 'footprints':footprints, 'nets':nets}

def encode_dictionary(data: bytes):
    toks=list(tokenize(data))
    # Dictionary only atom/string spellings; whitespace and parens retained as structural tokens.
    words=[v for typ,v,_,_ in toks if typ in ('atom','string')]
    freq=Counter(words)
    dictionary=[w for w,_ in sorted(freq.items(), key=lambda kv:(-kv[1], kv[0]))]
    ids={w:i for i,w in enumerate(dictionary)}
    stream=[]
    kind={'lp':0,'rp':1,'ws':2,'atom':3,'string':4}
    for typ,v,s,e in toks:
        if typ in ('atom','string'): stream.append([kind[typ],ids[v],s,e])
        else: stream.append([kind[typ],v.decode('utf-8','surrogateescape'),s,e])
    return dictionary, stream

def main():
    ap=argparse.ArgumentParser(); ap.add_argument('cad'); ap.add_argument('-o','--out',default='analysis')
    a=ap.parse_args(); p=Path(a.cad); data=p.read_bytes(); out=Path(a.out); out.mkdir(parents=True,exist_ok=True)
    roots=parse_sexpr(data); inv=semantic_inventory(roots); dictionary,stream=encode_dictionary(data)
    manifest={'source':p.name,'bytes':len(data),'sha256':hashlib.sha256(data).hexdigest(),'lossless_basis':'token stream stores every token plus original byte spans; source SHA-256 is reconstruction oracle','dictionary_entries':len(dictionary),'tokens':len(stream),'semantic':inv}
    (out/(p.name+'.manifest.json')).write_text(json.dumps(manifest,indent=2),encoding='utf-8')
    # Hex dictionary is compact and byte-exact under surrogateescape.
    with (out/(p.name+'.dict.hex')).open('w',encoding='ascii') as f:
        for i,w in enumerate(dictionary): f.write(f'{i:08X} {w.hex()}\n')
    (out/(p.name+'.tokens.json')).write_text(json.dumps(stream,separators=(',',':')),encoding='utf-8',errors='surrogateescape')
    print(json.dumps({'source':p.name,'sha256':manifest['sha256'],'dictionary_entries':len(dictionary),'tokens':len(stream),'board':inv['board'],'classes':inv['component_classes']},indent=2))
if __name__=='__main__': main()
