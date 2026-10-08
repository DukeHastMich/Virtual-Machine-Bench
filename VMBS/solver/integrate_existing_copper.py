#!/usr/bin/env python3
from pathlib import Path
import json,hashlib
from existing_copper import extract_existing_copper,existing_obstacles
from physical_router import board_bounds

def stable(x):return hashlib.sha256(json.dumps(x,sort_keys=True,separators=(',',':')).encode()).hexdigest()
def integrate(pcb,geom,out,step=1.27):
 e=extract_existing_copper(pcb);g=json.load(open(geom));b=board_bounds(g['outline']['polygon_mm'],step)
 nets=sorted({x['net'] for k in ('segments','vias','zones') for x in e[k] if x.get('net')})
 # Projection at zero added clearance records explicit specimen copper only; netclass clearance remains route-specific.
 counts={n:len(existing_obstacles(e,n,step,0.0,b)) for n in nets}
 serial={'segment_count':len(e['segments']),'via_count':len(e['vias']),'zone_count':len(e['zones']),'gap_count':len(e['gaps']),
         'net_count':len(nets),'bounds':list(b),'grid_mm':step,'other_net_obstacle_counts':counts}
 r={'format':'VMBS-EXISTING-COPPER-CP363',**serial,'evidence_gaps':e['gaps'],'digest':stable(serial),
    'state':'INSPECTED' if not e['gaps'] else 'TITS_UP'}
 Path(out).write_text(json.dumps(r,indent=2)+'\n');return r
if __name__=='__main__':
 import argparse;a=argparse.ArgumentParser();a.add_argument('pcb');a.add_argument('geom');a.add_argument('out');a.add_argument('--step',type=float,default=1.27);z=a.parse_args();print(json.dumps(integrate(z.pcb,z.geom,z.out,z.step),indent=2))
