"""Export a stripped ISA8 benchmark without borrowing its original routing."""
import argparse
import json
from pathlib import Path
from board_geometry import pads, outline, load
from sexpr import child, children, atom, dump


def coords(points, origin=(0,0)):
    return " ".join(f"{(x-origin[0])*1000:.3f} {-(y-origin[1])*1000:.3f}" for x,y in points)


def export(source, target, width=None, edge_clearance=0.5, hole_clearance=0.25):
    """Emit DSN with exact pad placement and documented submicron curve approximation."""
    b=load(source); ps=pads(b); edge=outline(b)
    default=next(n for n in children(b,"net_class") if atom(n[1])=="Default")
    clearance=float(child(default,"clearance")[1])
    if min(edge_clearance,hole_clearance)<0:raise ValueError('Clearances must be nonnegative')
    # Edge clearance is passed as a native router setting by run_router.py.
    # An inset boundary alone proved insufficient in the KiCad control run.
    margin=0.005
    inset=0
    routing_edge=edge
    if routing_edge.is_empty or routing_edge.geom_type!='Polygon' or routing_edge.interiors:
        raise ValueError('Inset routing envelope must remain one simple polygon')
    trace_width=width if width is not None else float(child(default,"trace_width")[1])
    via=float(child(default,"via_dia")[1])
    lines=['(pcb ISA8', '(parser (string_quote ") (space_in_quoted_tokens on))',
           '(resolution um 10) (unit um)', '(structure (layer F.Cu (type signal)) (layer B.Cu (type signal))',
           '(boundary (path pcb 0 '+coords(routing_edge.exterior.coords)+'))',
           f'(via VIA) (rule (width {trace_width*1000}) (clearance {clearance*1000})))', '(placement']
    for i,p in enumerate(ps):
        lines.append(f'(component PAD{i} (place P{i} {p["x"]*1000:.3f} {-p["y"]*1000:.3f} front 0))')
    lines += [')','(library']
    for i,p in enumerate(ps):
        lines.append(f'(image PAD{i} (pin S{i} 1 0 0))')
        lines.append(f'(padstack S{i}')
        for z in p['layers']:
            layer=['F.Cu','B.Cu'][z]
            if p['shape']=='circle':
                extra=max(0,hole_clearance-clearance)+margin if p['kind']=='np_thru_hole' else 0
                diameter=max(p['width'],p['drill_diameter'] or 0)
                shape=f'(circle {layer} {(diameter+2*extra)*1000} 0 0)'
            else:
                if p['kind']=='np_thru_hole':raise ValueError('Non-circular NPTH needs explicit drill geometry support')
                shape=f'(polygon {layer} 0 '+coords(p['geom'].exterior.coords,(p['x'],p['y']))+')'
            lines.append('(shape '+shape+')')
        lines.append('(attach off))')
    lines.append(f'(padstack VIA (shape (circle F.Cu {via*1000})) (shape (circle B.Cu {via*1000})) (attach off))')
    lines += [')','(network']
    nets={int(n[1]):atom(n[2]) for n in children(b,'net') if int(n[1])}
    for net,name in nets.items():
        pins=[f'P{i}-1' for i,p in enumerate(ps) if p['net']==net]
        if pins: lines.append(f'(net N{net} (pins '+ ' '.join(pins)+'))')
    lines += [')','(wiring)',')']
    Path(target).write_text('\n'.join(lines),encoding='utf-8')
    stripped=[n for n in b if not(isinstance(n,list) and n[0] in ('segment','via','zone'))]
    Path(target).with_suffix('.kicad_pcb').write_text(dump(stripped),encoding='utf-8')
    report=dict(source=str(source),pads=len(ps),nets=len(nets),width=trace_width,clearance=clearance,
                edge_clearance=edge_clearance,npth_clearance=hole_clearance,routing_boundary_inset=inset,
                constraint_margin_mm=margin,constraint_scope='Edge and circular NPTH; plated hole rules remain independently checked by KiCad',
                required_router_settings={'copper_to_edge_clearance_um':edge_clearance*1000+margin*1000,
                                          'hole_clearance_um':hole_clearance*1000+margin*1000,
                                          'automatic_neckdown':False,
                                          'neck_width_um':float(child(child(b,'setup'),'trace_min')[1])*1000},
                via_diameter=via,via_drill=float(child(default,'via_drill')[1]),
                pad_mapping=[{k:v for k,v in p.items() if k!='geom'} for p in ps],
                net_names=nets,outline_area=edge.area)
    Path(target).with_suffix('.manifest.json').write_text(json.dumps(report,indent=2))
    print({k:v for k,v in report.items() if k not in ('pad_mapping','net_names')})


if __name__=='__main__':
    ap=argparse.ArgumentParser(); ap.add_argument('source'); ap.add_argument('target');ap.add_argument('--width',type=float)
    ap.add_argument('--edge-clearance',type=float,default=.5);ap.add_argument('--hole-clearance',type=float,default=.25)
    a=ap.parse_args();export(a.source,a.target,a.width,a.edge_clearance,a.hole_clearance)
