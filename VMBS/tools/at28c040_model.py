class At28c040:
 SIZE=524288; PAGE=256
 def __init__(self,data=None):
  self.data=bytearray(data if data is not None else bytes([255])*self.SIZE); self.busy_until_ns=0; self.last_written=0; self.toggle=False
  if len(self.data)!=self.SIZE: raise ValueError("AT28C040 image must be exactly 524288 bytes")
 def busy(self,n): return n<self.busy_until_ns
 def read(self,a,n=0):
  if not 0<=a<self.SIZE: raise IndexError(a)
  v=self.data[a]
  if self.busy(n): self.toggle=not self.toggle; v=(v&63)|((~self.last_written)&128)|(64 if self.toggle else 0)
  return v
 def eval_read(self,a,ce_n,oe_n,we_n=True,now_ns=0): return None if ce_n or oe_n or not we_n else self.read(a,now_ns)
 def page_write(self,items,now_ns=0,write_cycle_ns=10_000_000):
  q=list(items)
  if not 1<=len(q)<=256: raise ValueError("page write requires 1..256 bytes")
  p=q[0][0]>>8
  if any((a>>8)!=p for a,_ in q): raise ValueError("page boundary")
  for a,d in q: self.data[a]=d&255; self.last_written=d&255
  self.busy_until_ns=now_ns+write_cycle_ns; return True
