class At28c64b:
    SIZE=8192
    PAGE=64
    def __init__(self,data=None):
        if data is None: data=bytes([0xFF])*self.SIZE
        if len(data)!=self.SIZE: raise ValueError('AT28C64B image must be exactly 8192 bytes')
        self.data=bytearray(data)
        self.sdp=False
        self.busy_until_ns=0
        self.last_written=0
        self._toggle=False
        self._cmd=[]
    def busy(self,now_ns): return now_ns < self.busy_until_ns
    def read(self,address,now_ns=0):
        if not 0<=address<self.SIZE: raise IndexError(address)
        v=self.data[address]
        if self.busy(now_ns):
            self._toggle=not self._toggle
            # Datasheet polling: I/O7 complements last-written I/O7; I/O6 toggles.
            v=(v & 0x3F) | ((~self.last_written)&0x80) | (0x40 if self._toggle else 0)
        return v
    def eval_read(self,address,ce_n,oe_n,we_n=True,now_ns=0):
        return None if ce_n or oe_n or not we_n else self.read(address,now_ns)
    def _command(self,a,d):
        self._cmd.append((a&0x1FFF,d&0xFF)); self._cmd=self._cmd[-5:]
        if self._cmd[-3:]==[(0x1555,0xAA),(0x0AAA,0x55),(0x1555,0xA0)]: self.sdp=True; return 'enable'
        if self._cmd[-5:]==[(0x1555,0xAA),(0x0AAA,0x55),(0x1555,0x80),(0x1555,0xAA),(0x0AAA,0x55)]: return 'disable-pending'
        return None
    def byte_write(self,address,value,now_ns=0,write_cycle_ns=10_000_000):
        if not 0<=address<self.SIZE: raise IndexError(address)
        if self.busy(now_ns): return False
        action=self._command(address,value)
        if action=='enable': self.busy_until_ns=now_ns+write_cycle_ns; self.last_written=value; return False
        if action=='disable-pending': return False
        # Final 20@1555 after disable prefix exits SDP; command bytes themselves are not stored.
        if len(self._cmd)>=5 and self._cmd[-5:]==[(0x0AAA,0x55),(0x1555,0x80),(0x1555,0xAA),(0x0AAA,0x55),(0x1555,0x20)]:
            self.sdp=False; self.busy_until_ns=now_ns+write_cycle_ns; self.last_written=value; return False
        if self.sdp: self.busy_until_ns=now_ns+write_cycle_ns; self.last_written=value; return False
        self.data[address]=value&0xFF; self.last_written=value&0xFF; self.busy_until_ns=now_ns+write_cycle_ns; return True
    def page_write(self,items,now_ns=0,write_cycle_ns=10_000_000):
        items=list(items)
        if not 1<=len(items)<=64: raise ValueError('page write requires 1..64 bytes')
        page=items[0][0]>>6
        if any((a>>6)!=page for a,_ in items): raise ValueError('all page-write addresses must share A6..A12')
        if self.sdp: return False
        for a,d in items:
            if not 0<=a<self.SIZE: raise IndexError(a)
            self.data[a]=d&0xFF; self.last_written=d&0xFF
        self.busy_until_ns=now_ns+write_cycle_ns; return True
