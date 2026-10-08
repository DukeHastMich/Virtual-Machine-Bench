class At28c16t:
 SIZE=2048
 def __init__(self,data=None):
  if data is None:data=bytes([255])*self.SIZE
  if len(data)!=self.SIZE:raise ValueError("AT28C16-T image must be exactly 2048 bytes")
  self.data=bytearray(data);self.busy_until_ns=0;self.last_written=0
 def busy(self,now_ns):return now_ns<self.busy_until_ns
 def ready_busy_n(self,now_ns):return not self.busy(now_ns)
 def read(self,address,now_ns=0):
  if not 0<=address<self.SIZE:raise IndexError(address)
  v=self.data[address]
  return ((v&0x7f)|((~self.last_written)&0x80)) if self.busy(now_ns) else v
 def eval_read(self,address,ce_n,oe_n,we_n=True,now_ns=0):return None if ce_n or oe_n or not we_n else self.read(address,now_ns)
 def byte_write(self,address,value,now_ns=0,write_cycle_ns=1_000_000):
  if not 0<=address<self.SIZE:raise IndexError(address)
  if self.busy(now_ns):return False
  self.data[address]=value&255;self.last_written=value&255;self.busy_until_ns=now_ns+write_cycle_ns;return True
