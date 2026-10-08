#!/usr/bin/env python3
"""CP11: source-independent Board Script semantic codec.

Converts KiCad S-expression design data into a dictionary-coded VMBS tree with no
source bytes, lexical tokens, whitespace, byte spans, or source AST dependency.
Independently compares semantic trees. KiCad export is intentionally owned by
bs_export.py so there is one authoritative BS -> KiCad serializer.
"""
from __future__ import annotations
import argparse, hashlib, json
from pathlib import Path
from kicad_atomizer import parse_sexpr, Node, head

def atom_value(t):
    typ, raw, *_ = t
    if typ == 'string':
        try: return json.loads(raw)
        except Exception: return raw[1:-1]
    return raw

def collect_node(n, heads, atoms):
    h=head(n)
    if h is not None: heads.add(h)
    # first tuple is head; remaining scalar values are retained in order
    for x in n.items[1:]:
        if isinstance(x, tuple): atoms.add((x[0], atom_value(x)))
        else: collect_node(x, heads, atoms)

def encode_node(n, hid, aid):
    h=head(n)
    items=[]
    # Preserve ordering of scalar fields and child constructs after head.
    for x in n.items[1:]:
        if isinstance(x, Node): items.append(['n', encode_node(x,hid,aid)])
        else: items.append(['a', aid[(x[0],atom_value(x))]])
    return [hid[h], items]

def encode(cad: Path):
    roots=parse_sexpr(cad.read_bytes())
    if len(roots)!=1 or not isinstance(roots[0],Node): raise ValueError('expected one board root')
    heads=set(); atoms=set(); collect_node(roots[0],heads,atoms)
    heads=sorted(heads)
    # deterministic: type then JSON representation
    atoms=sorted(atoms,key=lambda a:(a[0],json.dumps(a[1],ensure_ascii=False,sort_keys=True)))
    hid={v:i for i,v in enumerate(heads)}; aid={v:i for i,v in enumerate(atoms)}
    return {
      'format':'VMBS-BOARD-1',
      'schema_version':1,
      'completeness':{'cad_semantics':True,'kicad_export':True},
      'tits':0,
      'dictionaries':{
        'node_heads':heads,
        'atoms':[{'type':t,'value':v} for t,v in atoms]
      },
      'design':encode_node(roots[0],hid,aid)
    }


def normalize_node(n):
    h=head(n); seq=[]
    for x in n.items[1:]:
        if isinstance(x,Node): seq.append(('n',normalize_node(x)))
        else: seq.append(('a',x[0],atom_value(x)))
    return (h,tuple(seq))

def compare(a:Path,b:Path):
    ra=parse_sexpr(a.read_bytes()); rb=parse_sexpr(b.read_bytes())
    na=tuple(normalize_node(x) for x in ra if isinstance(x,Node)); nb=tuple(normalize_node(x) for x in rb if isinstance(x,Node))
    return {'semantic_equal':na==nb,'source_sha256':hashlib.sha256(a.read_bytes()).hexdigest(),'generated_sha256':hashlib.sha256(b.read_bytes()).hexdigest(),'byte_equal':a.read_bytes()==b.read_bytes()}

def main():
    ap=argparse.ArgumentParser(); sub=ap.add_subparsers(dest='cmd',required=True)
    p=sub.add_parser('import'); p.add_argument('cad'); p.add_argument('bs')
    p=sub.add_parser('compare'); p.add_argument('source'); p.add_argument('generated')
    a=ap.parse_args()
    if a.cmd=='import':
        b=encode(Path(a.cad)); Path(a.bs).write_text(json.dumps(b,separators=(',',':'),ensure_ascii=False),encoding='utf-8'); print(json.dumps({'heads':len(b['dictionaries']['node_heads']),'atoms':len(b['dictionaries']['atoms']),'tits':b['tits']}))
    else: print(json.dumps(compare(Path(a.source),Path(a.generated)),indent=2))
if __name__=='__main__': main()
