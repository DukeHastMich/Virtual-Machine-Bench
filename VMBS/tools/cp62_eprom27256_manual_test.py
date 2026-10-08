#!/usr/bin/env python3
from eprom27256_model import Eprom27256
p=bytes((i*17+3)&255 for i in range(32768));m=Eprom27256(p)
for a in (0,1,0x1234,32767):
 assert m.eval_read(a,False,False)==p[a]
 assert m.eval_read(a,True,False) is None
 assert m.eval_read(a,False,True) is None
try:Eprom27256(b'bad');raise AssertionError('accepted wrong payload')
except ValueError:pass
print('CP62 27256 EPROM manual-backed tests: PASS')
