#!/usr/bin/env python3
from intel828x_bus_models import *
for C,inv in ((Intel8282,False),(Intel8283,True)):
 m=C(); assert m.step(0x55,1,0)==(0xAA if inv else 0x55)
 # transparent while STB high, then retain after low
 assert m.step(0xA5,1,0)==(0x5A if inv else 0xA5)
 assert m.step(0x00,0,0)==(0x5A if inv else 0xA5)
 assert m.step(0x00,0,1) is None
for C,inv in ((Intel8286,False),(Intel8287,True)):
 m=C(); mask=0xFF if inv else 0
 assert m.drive(0x12,0x34,1,0)==(None,0x12^mask)
 assert m.drive(0x12,0x34,0,0)==(0x34^mask,None)
 assert m.drive(0x12,0x34,1,1)==(None,None)
print('CP67 Intel 8282/8283 latch + 8286/8287 transceiver tests: PASS')
