class Intel2114:
    SIZE=1024
    def __init__(self): self.mem=bytearray(self.SIZE)
    def read_nibble(self,address):
        if not 0<=address<self.SIZE: raise IndexError(address)
        return self.mem[address]&0xF
    def write_nibble(self,address,value):
        if not 0<=address<self.SIZE: raise IndexError(address)
        self.mem[address]=value&0xF
    def cycle(self,address,cs_n,we_n,data_in=0):
        if cs_n: return None
        if not we_n:
            self.write_nibble(address,data_in); return None
        return self.read_nibble(address)
