#!/usr/bin/env python3
"""Standalone VMBS -> KiCad exporter. Deliberately has no source-CAD parser/import path."""
import argparse,json
from pathlib import Path

def atom_out(a):
    return json.dumps(str(a['value']),ensure_ascii=False,separators=(',',':')) if a['type']=='string' else str(a['value'])
def emit_node(enc,d,depth=0):
    """Emit readable KiCad S-expressions while preserving semantic item order."""
    heads=d['node_heads']; atoms=d['atoms']; hid,items=enc
    pad='\t'*depth
    # Atomic-only lists use KiCad's compact single-line form.
    if all(typ=='a' for typ,_ in items):
        vals=[atom_out(atoms[val]) for typ,val in items]
        return '('+heads[hid]+((' '+' '.join(vals)) if vals else '')+')'

    lines=['('+heads[hid]]
    on_opening=True
    for typ,val in items:
        if typ=='a':
            tok=atom_out(atoms[val])
            if on_opening:
                lines[0]+=' '+tok
            else:
                # A scalar after a child list must remain after that child.
                # Give it its own continuation line rather than reordering it.
                lines.append('\t'*(depth+1)+tok)
        else:
            lines.append('\t'*(depth+1)+emit_node(val,d,depth+1).lstrip('\t'))
            on_opening=False
    lines.append(pad+')')
    return '\n'.join(lines)

def main():
    ap=argparse.ArgumentParser(); ap.add_argument('bs'); ap.add_argument('out'); a=ap.parse_args()
    b=json.loads(Path(a.bs).read_text(encoding='utf-8'))
    if b.get('format')!='VMBS-BOARD-1': raise SystemExit('unsupported BS format')
    if b.get('tits')!=0: raise SystemExit('COMPILER HALTED: TITS(UP)')
    Path(a.out).write_text(emit_node(b['design'],b['dictionaries'])+'\n',encoding='utf-8')
if __name__=='__main__': main()
