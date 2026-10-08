class Intel2101A:
    SIZE=256
    def __init__(self): self.mem=bytearray(self.SIZE)
    def cycle(self,address,ce1_n,ce2,rw,od=False,data_in=0):
        if not 0<=address<self.SIZE: raise IndexError(address)
        selected=(not ce1_n) and bool(ce2)
        if not selected: return None
        if not rw:
            self.mem[address]=data_in&0xF
            return None
        if od: return None
        return self.mem[address]&0xF
