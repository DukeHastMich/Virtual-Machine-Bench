#!/usr/bin/env python3
import json
from pathlib import Path
from eprom27128_model import Eprom27128
from eprom27512_model import Eprom27512
root=Path(__file__).resolve().parents[1]
s=json.load(open(root/'compiler_db/component_store.json'))
a=next(x for x in s['records'] if x.get('display_name')=='27128A')
b=next(x for x in s['records'] if x.get('display_name')=='AT27C256R')
c=next(x for x in s['records'] if x.get('display_name')=='AT27C512R')
# Different board-facing interfaces must remain distinct even though all share read-only x8 behavior.
assert a['part_id']!=b['part_id']!=c['part_id']
pa={p['number']:p['name'] for p in a['pins']}; pb={p['number']:p['name'] for p in b['pins']}; pc={p['number']:p['name'] for p in c['pins']}
assert pa[27]=='PGM#' and pb[27]=='A14' and pc[27]=='A14'
assert pa[1]=='VPP' and pb[1]=='VPP' and pc[1]=='A15'
assert pc[22]=='OE#/VPP'
assert a['programmed_state']['capacity_bytes']==16384
assert c['programmed_state']['capacity_bytes']==65536
for Model,size in [(Eprom27128,16384),(Eprom27512,65536)]:
 data=bytes((i*17+3)&255 for i in range(size)); m=Model(data)
 for x in [0,1,size//2,size-1]:
  assert m.eval_read(x,False,False)==data[x]
  assert m.eval_read(x,True,False) is None
  assert m.eval_read(x,False,True) is None
print('CP77 EPROM interface identity + authentic payload read: PASS')
