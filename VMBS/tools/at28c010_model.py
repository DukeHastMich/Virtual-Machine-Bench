class At28c010:
    SIZE=131072; PAGE=128
    def __init__(self,data=None):
        if data is None: data=bytes([0xFF])*self.SIZE
        if len(data)!=self.SIZE: raise ValueError("AT28C010 image must be exactly 131072 bytes")
        self.data=bytearray(data); self.busy_until_ns=0; self.last_written=0; self._toggle=False
    def busy(self,now_ns): return now_ns < self.busy_until_ns
    def read(self,address,now_ns=0):
        if not 0<=address<self.SIZE: raise IndexError(address)
        v=self.data[address]
        if self.busy(now_ns):
            self._toggle=not self._toggle; v=(v&0x3F)|((~self.last_written)&0x80)|(0x40 if self._toggle else 0)
        return v
    def eval_read(self,address,ce_n,oe_n,we_n=True,now_ns=0): return None if ce_n or oe_n or not we_n else self.read(address,now_ns)
    def byte_write(self,address,value,now_ns=0,write_cycle_ns=10_000_000):
        if not 0<=address<self.SIZE: raise IndexError(address)
        if self.busy(now_ns): return False
        self.data[address]=value&255; self.last_written=value&255; self.busy_until_ns=now_ns+write_cycle_ns; return True
    def page_write(self,items,now_ns=0,write_cycle_ns=10_000_000):
        items=list(items)
        if not 1<=len(items)<=128: raise ValueError("page write requires 1..128 bytes")
        page=items[0][0]>>7
        if any((a>>7)!=page for a,_ in items): raise ValueError("all page-write addresses must share A7..A16")
        for a,d in items:
            if not 0<=a<self.SIZE: raise IndexError(a)
            self.data[a]=d&255; self.last_written=d&255
        self.busy_until_ns=now_ns+write_cycle_ns; return True
