class AsyncSram8:
 def __init__(self,size):
  if size<=0 or size&(size-1): raise ValueError(size)
  self.data=bytearray(size)
 def read(self,a): return self.data[a]
 def write(self,a,v): self.data[a]=v&255
 def eval_read(self,a,ce_n,oe_n,we_n):
  return None if ce_n or oe_n or not we_n else self.read(a)
 def apply_write(self,a,v,ce_n,we_n):
  if not ce_n and not we_n:self.write(a,v)
class Sram6116(AsyncSram8):
 def __init__(self):super().__init__(2048)
class Sram6264(AsyncSram8):
 def __init__(self):super().__init__(8192)
class Sram62256(AsyncSram8):
 def __init__(self):super().__init__(32768)
