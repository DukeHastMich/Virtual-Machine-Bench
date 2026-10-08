#!/usr/bin/env python3
from line_interface_models import MC1488,MC1489
assert MC1488.drive(0)==1 and MC1488.drive(1)==-1
assert MC1489.receive(1)==0 and MC1489.receive(-1)==1
for x in (0,1): assert MC1489.receive(MC1488.drive(x))==x
for cls,arg in ((MC1488,2),(MC1489,0)):
 try:
  (cls.drive if cls is MC1488 else cls.receive)(arg);raise AssertionError('accepted invalid level')
 except ValueError:pass
print('CP63 EIA-232 line-interface manual-backed tests: PASS')
