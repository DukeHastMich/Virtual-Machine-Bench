"""Put FreeRouting's new copper onto unchanged original KiCad footprints."""
import sys
from pathlib import Path
from sexpr import parse, children, child, atom, dump
from board_geometry import load


def walk(node, key):
    if not isinstance(node,list):return
    if node and node[0]==key:yield node
    for n in node:
        if isinstance(n,list):yield from walk(n,key)


def import_session(original, session, output):
    """Import session wires/vias in DSN coordinates; never reuse original copper."""
    b=load(original)
    b=[n for n in b if not(isinstance(n,list) and n[0] in ('segment','via','zone'))]
    ses=parse(Path(session).read_text())[0]
    resolution=next(walk(ses,'resolution'))
    assert resolution[1]=='um', resolution
    scale=1000*float(resolution[2])
    layers={'F.Cu':'Front','B.Cu':'Back'}
    stack_diameters={}
    for s in walk(ses,'padstack'):
        shape=child(child(s,'shape'),'circle')
        if shape:stack_diameters[atom(s[1])]=float(shape[2])/scale
    for net in walk(ses,'net'):
        name=atom(net[1])
        if not name.startswith('N'):raise ValueError(f'Unknown net {name}')
        ni=int(name[1:])
        for wire in children(net,'wire'):
            p=child(wire,'path'); layer=layers[atom(p[1])]; width=float(p[2])/scale
            cs=list(map(float,p[3:]));pts=[(cs[i]/scale,-cs[i+1]/scale) for i in range(0,len(cs),2)]
            for a,z in zip(pts,pts[1:]):
                if a==z:continue
                b.append(['segment',['start',*map(str,a)],['end',*map(str,z)],['width',str(width)],['layer',layer],['net',str(ni)]])
        for via in children(net,'via'):
            size=stack_diameters[atom(via[1])]
            b.append(['via',['at',str(float(via[2])/scale),str(-float(via[3])/scale)],
                      ['size',str(size)],['drill','0.635'],['layers','Front','Back'],['net',str(ni)]])
    general=child(b,'general')
    if general and child(general,'tracks'):
        child(general,'tracks')[1]=str(len(children(b,'segment'))+len(children(b,'via')))
    Path(output).write_text('(kicad_pcb\n'+'\n'.join(dump(n) for n in b[1:])+'\n)',encoding='utf-8')
    print('Imported',len(children(b,'segment')),'segments and',len(children(b,'via')),'vias')


if __name__=='__main__':import_session(*sys.argv[1:])
