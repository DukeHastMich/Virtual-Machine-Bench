#!/usr/bin/env python3
from intel8255a_model import Intel8255A
p=Intel8255A()
assert p.control==0x9B and p.pa_input and p.pb_input
p.set_inputs(0xA5,0x5A,0xC3); assert p.read(0)==0xA5 and p.read(1)==0x5A
# mode 0 all outputs
p.write(3,0x80); p.write(0,0x12);p.write(1,0x34);p.write(2,0x56)
assert (p.read(0),p.read(1),p.read(2))==(0x12,0x34,0x56)
# BSR modifies only selected PC latch bit, not mode control
c=p.control;p.write(3,(5<<1)|1);assert p.read(2)&0x20 and p.control==c;p.write(3,(5<<1));assert not(p.read(2)&0x20)
# Mode 1 Port B input; BSR PC2 sets INTE B; strobe latches input and asserts interrupt, CPU read clears IBF/INTR.
p.write(3,0x87) # GA mode0 output; GB mode1, PB input, PC lower input
p.write(3,(2<<1)|1);p.strobe_b(0xD6);assert p.ibfb and p.intrb and p.read(1)==0xD6 and not p.ibfb and not p.intrb
# Mode 1 Port A input uses PC4 BSR as INTE A.
p.write(3,0xB8);p.write(3,(4<<1)|1);p.strobe_a(0x66);assert p.intra and p.read(0)==0x66 and not p.intra
# Mode 2 Port A bidirectional; PC4/PC6 independently control input/output INTE.
p.write(3,0xC0);p.write(3,(4<<1)|1);p.strobe_a(0x77);assert p.intra and p.read(0)==0x77 and not p.ibfa
print('CP69 Intel 8255A manual-backed functional tests: PASS')
