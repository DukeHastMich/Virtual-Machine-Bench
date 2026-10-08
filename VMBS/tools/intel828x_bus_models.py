"""CP67 Intel 8282/8283 latches and 8286/8287 bus transceivers."""
Z=None
class Intel8282:
 def __init__(self,invert=False):self.q=0;self.invert=invert
 def step(self,data,stb,oe_n):
  if not 0<=data<=255:raise ValueError('data')
  if stb:self.q=data
  if oe_n:return Z
  return (self.q ^ (0xFF if self.invert else 0)) & 0xFF
class Intel8283(Intel8282):
 def __init__(self):super().__init__(True)
class Intel8286:
 def __init__(self,invert=False):self.invert=invert
 def drive(self,a,b,t,oe_n):
  if not 0<=a<=255 or not 0<=b<=255:raise ValueError('bus')
  if oe_n:return Z,Z
  mask=0xFF if self.invert else 0
  if t:return Z,(a^mask)&255 # T=1 A -> B
  return (b^mask)&255,Z     # T=0 B -> A
class Intel8287(Intel8286):
 def __init__(self):super().__init__(True)
