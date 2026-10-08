class Intel2142:
    SIZE=1024
    def __init__(self):self.mem=bytearray(self.SIZE)
    def cycle(self,address,cs1_n,cs2,we_n,od=False,data_in=0):
        if not 0<=address<self.SIZE:raise IndexError(address)
        if cs1_n or not cs2:return None
        if not we_n:self.mem[address]=data_in&0xF;return None
        if od:return None
        return self.mem[address]&0xF
