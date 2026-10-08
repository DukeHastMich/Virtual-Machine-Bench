#!/usr/bin/env python3
from async_sram_models import Sram6116,Sram6264,Sram62256
for cls,size in ((Sram6116,2048),(Sram6264,8192),(Sram62256,32768)):
 m=cls(); assert len(m.data)==size
 for a in (0,1,size//2,size-1):
  m.apply_write(a,(a^0xA5)&255,False,False); assert m.eval_read(a,False,False,True)==((a^0xA5)&255)
  assert m.eval_read(a,True,False,True) is None
  assert m.eval_read(a,False,True,True) is None
  assert m.eval_read(a,False,False,False) is None
print('CP61 asynchronous SRAM manual-backed tests: PASS')
