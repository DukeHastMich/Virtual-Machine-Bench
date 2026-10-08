class Intel2102A:
    SIZE=1024
    def __init__(self): self.mem=bytearray(self.SIZE)
    def cycle(self,address,ce,rw,data_in=0):
        if not 0<=address<self.SIZE: raise IndexError(address)
        if ce: return None
        if not rw:
            self.mem[address]=1 if data_in else 0
            return None
        return self.mem[address]&1
