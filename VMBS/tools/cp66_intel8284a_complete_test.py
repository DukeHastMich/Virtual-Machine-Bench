#!/usr/bin/env python3
from intel8284a_model import Intel8284AClockCore
# /3 CLK and /2 PCLK relationship
m=Intel8284AClockCore(); c=[]
for _ in range(60): c.append(m.crystal_tick(1)[0])
assert sum(c)==20
# F/C external selects EFI; crystal still drives OSC but not divider
m=Intel8284AClockCore();m.set_controls(True,False,False)
for _ in range(6):m.crystal_tick(_&1)
assert m.clk==0 and m.osc==1
for _ in range(3):m.external_tick()
assert m.clk==1
# CSYNC forces CLK/PCLK high
m.set_controls(True,True,False);m.external_tick();assert m.clk==1 and m.pclk==1
# RESET only changes on CLK falling edge
m=Intel8284AClockCore();m.set_controls(False,False,True)
for _ in range(3):m.crystal_tick(1)
assert m.clk==1 and m.reset==0
m.crystal_tick(1);assert m.clk==0 and m.reset==1
# AEN active-low qualification
m=Intel8284AClockCore();m.set_ready_inputs(1,1,0,1,1)
for _ in range(4):m.crystal_tick(1)
assert m.ready==0
# one-stage path: qualified RDY reaches READY on falling edge
m=Intel8284AClockCore();m.set_ready_inputs(1,0,0,1,1)
for _ in range(4):m.crystal_tick(1)
assert m.ready==1
# two-stage path: capture on preceding rising CLK, transfer on falling CLK
m=Intel8284AClockCore();m.set_ready_inputs(1,0,0,1,0)
for _ in range(3):m.crystal_tick(1)
assert m.clk==1 and m.ready==0
m.set_ready_inputs(0,0,0,1,0);m.crystal_tick(1)
assert m.clk==0 and m.ready==1
print('CP66 Intel 8284A source/CSYNC/RESET/READY manual-backed tests: PASS')
