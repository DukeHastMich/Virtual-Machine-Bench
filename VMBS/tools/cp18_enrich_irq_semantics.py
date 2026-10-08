#!/usr/bin/env python3
"""Enrich component records generically: an isa_irq_output pin with an IRQ number exposes ISA8 IRQ<n>.
This is component-intrinsic knowledge; no board references are consulted.
"""
import json, argparse
from pathlib import Path
ap=argparse.ArgumentParser(); ap.add_argument('component_store'); a=ap.parse_args()
p=Path(a.component_store); d=json.load(open(p)); changed=0
for r in d.get('records',[]):
 for pin in r.get('pins',[]):
  if pin.get('role')=='isa_irq_output' and pin.get('irq') is not None:
   facet={'interface':'ISA8','signal':f"IRQ{int(pin['irq'])}",'direction':'output'}
   fs=pin.setdefault('interface_facets',[])
   if facet not in fs: fs.append(facet); changed+=1
p.write_text(json.dumps(d,indent=2)+'\n')
print(json.dumps({'changed_pins':changed}))
