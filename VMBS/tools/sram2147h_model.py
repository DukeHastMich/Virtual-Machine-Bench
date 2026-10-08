class Intel2147H:
    SIZE=4096
    def __init__(self):self.mem=bytearray(self.SIZE);self.standby=True
    def cycle(self,address,cs_n,we_n,data_in=0):
        if not 0<=address<self.SIZE:raise IndexError(address)
        self.standby=bool(cs_n)
        if cs_n:return None
        if not we_n:self.mem[address]=1 if data_in else 0;return None
        return self.mem[address]&1
