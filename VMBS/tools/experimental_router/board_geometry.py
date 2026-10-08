"""ISA8 legacy KiCad geometry, in millimetres and KiCad's downward Y axis."""
import math
import os
import sys
from pathlib import Path
sys.path.insert(0, os.environ.get('VMBS_ROUTER_DEPS',str(Path(__file__).parent / "deps")))
from shapely.geometry import Point, LineString, Polygon, box
from shapely.affinity import rotate, translate
from shapely.ops import polygonize, unary_union
from sexpr import child, children, atom, parse


def xy(node, key):
    return tuple(map(float, child(node, key)[1:3]))


def pads(board):
    """Extract pad copper; KiCad pad angles are absolute, locations footprint-relative."""
    result = []
    for module in children(board, "module"):
        ma = child(module, "at")
        mx, my = map(float, ma[1:3])
        angle = float(ma[3]) if len(ma) > 3 else 0
        ref = next(atom(t[2]) for t in children(module, "fp_text") if t[1] == "reference")
        for pad in children(module, "pad"):
            pa = child(pad, "at")
            px, py = map(float, pa[1:3])
            theta = math.radians(-angle)
            x = mx + px * math.cos(theta) - py * math.sin(theta)
            y = my + px * math.sin(theta) + py * math.cos(theta)
            w, h = xy(pad, "size")
            shape = pad[3]
            if shape == "circle":
                assert abs(w-h) < 1e-8
                geom = Point(0, 0).buffer(w/2, quad_segs=32)
            elif shape == "rect":
                geom = box(-w/2, -h/2, w/2, h/2)
            elif shape == "roundrect":
                radius = min(w,h) * float(child(pad,"roundrect_rratio")[1])
                geom = box(-w/2+radius, -h/2+radius, w/2-radius, h/2-radius).buffer(radius, quad_segs=16)
            else:
                raise ValueError(f"Unsupported pad {shape}")
            rotation = float(pa[3]) if len(pa)>3 else 0
            geom = translate(rotate(geom, -rotation, origin=(0,0)), x, y)
            ls = [atom(a) for a in child(pad,"layers")[1:]]
            layers = [0,1] if "*.Cu" in ls else [z for z,names in enumerate((("F.Cu","Front"),("B.Cu","Back"))) if any(n in ls for n in names)]
            net = child(pad,"net",["net","0"])
            drill=child(pad,'drill')
            drill_diameter=None
            if pad[2]=='np_thru_hole':
                if not drill or len(drill)!=2:
                    raise ValueError('NPTH routing requires a centered circular drill')
                drill_diameter=float(drill[1])
            result.append(dict(ref=ref, pin=atom(pad[1]), net=int(net[1]), x=x,y=y,
                               geom=geom,layers=layers,kind=pad[2],shape=shape,width=w,height=h,
                               drill_diameter=drill_diameter))
    return result


def outline(board):
    """Polygonize straight edges and finely sampled legacy arcs; reject broken outlines."""
    edges=[]
    for n in board:
        if not isinstance(n,list) or child(n,"layer") != ["layer","Edge.Cuts"]:
            continue
        a,b=xy(n,"start"),xy(n,"end")
        if n[0]=="gr_line":
            pts=[a,b]
        elif n[0]=="gr_arc":
            sweep=math.radians(float(child(n,"angle")[1]))
            t=math.atan2(b[1]-a[1],b[0]-a[0]); r=math.dist(a,b)
            count=max(2,math.ceil(abs(sweep)*r/0.025))
            pts=[(a[0]+r*math.cos(t+sweep*i/count),a[1]+r*math.sin(t+sweep*i/count)) for i in range(count+1)]
        else:
            raise ValueError(f"Unsupported outline {n[0]}")
        edges.append(LineString([(round(x,6),round(y,6)) for x,y in pts]))
    polys=list(polygonize(unary_union(edges)))
    if len(polys)!=1:
        raise ValueError(f"Expected one closed board outline, got {len(polys)}")
    return polys[0]


def load(path):
    return parse(Path(path).read_text(encoding="utf-8"))[0]
