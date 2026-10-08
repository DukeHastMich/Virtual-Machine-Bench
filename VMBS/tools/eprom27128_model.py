class Eprom27128:
    SIZE=16384
    def __init__(self,data):
        if len(data)!=self.SIZE: raise ValueError('27128 payload must be exactly 16384 bytes')
        self.data=bytes(data)
    def read(self,a):
        if not 0<=a<self.SIZE: raise IndexError(a)
        return self.data[a]
    def eval_read(self,a,ce_n,oe_n,pgm_n=True):
        # Intel 27128A normal Read mode requires CE#=L, OE#=L, PGM#=H.
        return None if ce_n or oe_n or not pgm_n else self.read(a)
