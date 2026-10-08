class Eprom27256:
 def __init__(self,data):
  if len(data)!=32768:raise ValueError('27256 payload must be exactly 32768 bytes')
  self.data=bytes(data)
 def read(self,a):return self.data[a]
 def eval_read(self,a,ce_n,oe_n):return None if ce_n or oe_n else self.read(a)
