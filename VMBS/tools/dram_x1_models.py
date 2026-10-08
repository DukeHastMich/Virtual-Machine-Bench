"""CP64 manual-backed x1 asynchronous DRAM cycle models.
Models address multiplexing, page access, write/read and refresh accounting.
Does not invent deterministic capacitor-decay corruption after refresh expiry.
"""
class DramX1:
    def __init__(self,row_bits,col_bits,refresh_bits,refresh_window_ns):
        self.row_bits=row_bits;self.col_bits=col_bits;self.refresh_bits=refresh_bits
        self.refresh_window_ns=refresh_window_ns
        self.data=bytearray((1<<(row_bits+col_bits)+7)//8)
        self.last_refresh=[0]*(1<<refresh_bits)
    @property
    def cells(self): return 1<<(self.row_bits+self.col_bits)
    def _addr(self,row,col):
        if not 0<=row<(1<<self.row_bits) or not 0<=col<(1<<self.col_bits): raise ValueError('address')
        return (row<<self.col_bits)|col
    def write(self,row,col,bit):
        if bit not in (0,1):raise ValueError('bit')
        a=self._addr(row,col);m=1<<(a&7);i=a>>3
        if bit:self.data[i]|=m
        else:self.data[i]&=255^m
    def read(self,row,col):
        a=self._addr(row,col);return (self.data[a>>3]>>(a&7))&1
    def refresh(self,refresh_address,now_ns):
        if not 0<=refresh_address<(1<<self.refresh_bits):raise ValueError('refresh address')
        self.last_refresh[refresh_address]=now_ns
    def overdue_refresh_rows(self,now_ns):
        return [i for i,t in enumerate(self.last_refresh) if now_ns-t>self.refresh_window_ns]

class MCM6665A(DramX1):
    # 64Kx1, 8 row + 8 column address bits; 128-cycle / 2 ms refresh.
    def __init__(self):super().__init__(8,8,7,2_000_000)

class TMS4256(DramX1):
    # 256Kx1, 9 row + 9 column address bits; 256 refresh cycles / 4 ms.
    def __init__(self):super().__init__(9,9,8,4_000_000)
