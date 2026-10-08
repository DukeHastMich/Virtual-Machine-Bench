"""Reject geometry and rule sets outside this experimental adapter's coverage."""
import json
import math
import sys
from pathlib import Path
from board_geometry import load,pads,outline
from sexpr import child,children,atom

def inspect(board_path,rules_path,reroute_existing=False):
    rules=json.loads(Path(rules_path).read_text(encoding='utf-8-sig'))
    for key in ('edge_clearance_mm','hole_clearance_mm'):
        value=rules.get(key)
        if isinstance(value,bool) or not isinstance(value,(int,float)) or not math.isfinite(value) or value<0:
            raise ValueError(f'Project rules require a finite nonnegative {key}')
    if not rules.get('provenance'):raise ValueError('Project rules require provenance (where these limits came from)')
    path=Path(board_path);board=load(path)
    if not reroute_existing and any(children(board,k) for k in ('segment','via','zone','arc')):
        raise ValueError('Existing copper preserved: use explicit full-reroute mode to replace traces/vias/zones in a new draft')
    if children(board,'footprint') or not children(board,'module'):
        raise ValueError('Only legacy KiCad module-format boards are supported by this experimental adapter')
    layers=child(board,'layers',[])
    if sum(isinstance(n,list) and len(n)>2 and n[2]=='signal' for n in layers)!=2:
        raise ValueError('Only two-copper-layer boards are currently supported')
    if any(atom(n[1])!='Default' and children(n,'add_net') for n in children(board,'net_class')):
        raise ValueError('Multiple net classes need a richer rule exporter; refusing to flatten their rules')
    for zone in children(board,'zone'):
        if child(zone,'keepout'):raise ValueError('Board keepouts are not yet supported by the adapter')
    if path.with_suffix('.kicad_dru').exists():
        raise ValueError('Custom KiCad rules require explicit adapter support')
    setup=child(board,'setup');minimum=float(child(setup,'trace_min')[1])
    default=children(board,'net_class')[0]
    if minimum<=0 or float(child(default,'trace_width')[1])<minimum:
        raise ValueError('Invalid source track width/minimum')
    # The tested importer currently uses this drill value for the exported via.
    if abs(float(child(default,'via_drill')[1])-.635)>1e-8:
        raise ValueError('Via drill must be 0.635 mm until configurable via import is supported')
    for module in children(board,'module'):
        if child(module,'clearance'):raise ValueError('Footprint clearance overrides are unsupported')
        for pad in children(module,'pad'):
            if child(pad,'clearance'):raise ValueError('Pad clearance overrides are unsupported')
    pads(board);outline(board)
    return dict(status='supported',rules=rules,minimum_trace_mm=minimum,
                scope='Two-layer legacy KiCad, one net class; output is an experimental proposal')

if __name__=='__main__':
    try: result=inspect(sys.argv[1],sys.argv[2],'--reroute-existing' in sys.argv[3:])
    except Exception as exc: print(f'Preflight rejected input: {exc}',file=sys.stderr);raise SystemExit(2)
    print(json.dumps(result,indent=2))
