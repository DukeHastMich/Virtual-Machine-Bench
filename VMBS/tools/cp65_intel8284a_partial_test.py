#!/usr/bin/env python3
from intel8284a_model import Intel8284AClockCore
m=Intel8284AClockCore();cl=[];pc=[]
for i in range(60):
 c,p,r=m.source_tick(0);cl.append(c);pc.append(p)
# Abstract source ticks produce one asserted CLK sample per 3 source periods.
assert sum(cl)==20
# RESET changes only when a CLK falling edge is observed.
m=Intel8284AClockCore()
for _ in range(3):m.source_tick(1)
assert m.clk==1 and m.reset==0
m.source_tick(1);assert m.clk==0 and m.reset==1
# This is intentionally partial: READY synchronizer is not represented/claimed.
print('CP65 Intel 8284A partial clock/reset manual-backed tests: PASS (READY remains TITS UP)')
