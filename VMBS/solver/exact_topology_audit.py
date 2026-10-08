#!/usr/bin/env python3
from pathlib import Path
import json
from physical_router import extract_physical
from existing_copper import extract_existing_copper
from exact_copper_topology import build_net_graph,stable

def audit(pcb,out):
    ph=extract_physical(pcb);ev=extract_existing_copper(pcb)
    nets=sorted({p['net'] for p in ph['pads'] if p.get('net_id') and p.get('net')})
    rec=[build_net_graph(n,ev,ph['pads']) for n in nets]
    serial={'format':'VMBS-EXACT-COPPER-TOPOLOGY-CP438','net_count':len(rec),
      'connected_net_count':sum(r['historical_pad_connectivity']=='CONNECTED' for r in rec),
      'unresolved_net_count':sum(r['historical_pad_connectivity']!='CONNECTED' for r in rec),
      'pads_touching_copper':sum(r['pads_touching_copper'] for r in rec),
      'pad_count':sum(r['pad_count'] for r in rec),
      'evidence_gap_count':sum(len(r['evidence_gaps']) for r in rec if r['pad_count']>1),
      'single_terminal_isolated_count':sum(r['pad_count']==1 and r['pads_touching_copper']==0 for r in rec),
      'multi_terminal_connected_count':sum(r['pad_count']>1 and r['historical_pad_connectivity']=='CONNECTED' for r in rec),
      'multi_terminal_unresolved_count':sum(r['pad_count']>1 and r['historical_pad_connectivity']!='CONNECTED' for r in rec),
      'extraction_gap_count':len(ev.get('gaps',[])),'nets':rec}
    serial['digest']=stable({k:v for k,v in serial.items() if k!='digest'})
    Path(out).write_text(json.dumps(serial,indent=2)+'\n');return serial
if __name__=='__main__':
 import argparse
 a=argparse.ArgumentParser();a.add_argument('pcb');a.add_argument('out');z=a.parse_args()
 r=audit(z.pcb,z.out);print(json.dumps({k:r[k] for k in ('net_count','connected_net_count','unresolved_net_count','pads_touching_copper','pad_count','evidence_gap_count','single_terminal_isolated_count','multi_terminal_connected_count','multi_terminal_unresolved_count','extraction_gap_count','digest')},indent=2))
