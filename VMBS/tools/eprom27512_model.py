class Eprom27512:
    SIZE=65536
    def __init__(self,data):
        if len(data)!=self.SIZE: raise ValueError('27512 payload must be exactly 65536 bytes')
        self.data=bytes(data)
    def read(self,a):
        if not 0<=a<self.SIZE: raise IndexError(a)
        return self.data[a]
    def eval_read(self,a,ce_n,oe_n):
        return None if ce_n or oe_n else self.read(a)
