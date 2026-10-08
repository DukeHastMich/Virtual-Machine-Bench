class At27Otp:
    def __init__(self, words, width=8, data=None):
        self.words=words; self.width=width; self.mask=(1<<width)-1
        erased=self.mask
        self.data=list(data) if data is not None else [erased]*words
        if len(self.data)!=words: raise ValueError("OTP image length mismatch")
        if any((x<0 or x>self.mask) for x in self.data): raise ValueError("OTP word width exceeded")
    def read(self,address):
        if not 0<=address<self.words: raise IndexError(address)
        return self.data[address]
    def eval_read(self,address,ce_n,oe_n):
        return None if ce_n or oe_n else self.read(address)
