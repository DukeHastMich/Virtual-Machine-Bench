"""Intel 8255A PPI functional model (CP69).
Models CPU-visible programming plus mode-0 and the documented mode-1/2 handshake state.
Electrical timing/thresholds are intentionally outside this digital model.
"""
class Intel8255A:
    def __init__(self): self.reset()
    def reset(self):
        # Intel reset places all ports in input mode.
        self.control=0x9B; self.pa_out=self.pb_out=self.pc_out=0
        self.pa_in=self.pb_in=self.pc_in=0
        self.ibfa=self.ibfb=False; self.obfa_n=self.obfb_n=True
        self.inte_a_in=self.inte_a_out=self.inte_b=False
        self.intra=self.intrb=False
        self._decode()
    def _decode(self):
        c=self.control
        self.ga_mode=2 if (c&0x60)==0x40 else ((c>>5)&1)
        self.gb_mode=(c>>2)&1
        self.pa_input=bool(c&0x10); self.pc_upper_input=bool(c&0x08)
        self.pb_input=bool(c&0x02); self.pc_lower_input=bool(c&0x01)
    def set_inputs(self,a=None,b=None,c=None):
        if a is not None:self.pa_in=a&255
        if b is not None:self.pb_in=b&255
        if c is not None:self.pc_in=c&255
    def write(self,addr,val):
        addr&=3; val&=255
        if addr==3:
            if val&0x80:
                self.control=val; self._decode(); self.pa_out=self.pb_out=self.pc_out=0
                self.ibfa=self.ibfb=False; self.obfa_n=self.obfb_n=True; self.intra=self.intrb=False
            else:
                bit=(val>>1)&7; mask=1<<bit
                if val&1:self.pc_out|=mask
                else:self.pc_out&=~mask&255
                # BSR also controls mode-1/2 interrupt-enable flip-flops on handshake PC bits.
                state=bool(val&1)
                if bit==4 and (self.ga_mode==2 or (self.ga_mode==1 and self.pa_input)):self.inte_a_in=state
                if bit==6 and (self.ga_mode==2 or (self.ga_mode==1 and not self.pa_input)):self.inte_a_out=state
                if bit==2 and self.gb_mode==1:self.inte_b=state
                self._update_intr()
            return
        if addr==0:
            self.pa_out=val
            if self.ga_mode in (1,2) and not self.pa_input:
                self.obfa_n=False; self._update_intr()
        elif addr==1:
            self.pb_out=val
            if self.gb_mode==1 and not self.pb_input:
                self.obfb_n=False; self._update_intr()
        else:self.pc_out=val
    def read(self,addr):
        addr&=3
        if addr==3:return self.control
        if addr==0:
            v=self.pa_in if (self.pa_input or self.ga_mode==2) else self.pa_out
            if self.ga_mode in (1,2) and (self.pa_input or self.ga_mode==2): self.ibfa=False; self._update_intr()
            return v
        if addr==1:
            v=self.pb_in if self.pb_input else self.pb_out
            if self.gb_mode==1 and self.pb_input:self.ibfb=False; self._update_intr()
            return v
        # Mode 0: ordinary PC directions. Handshake modes substitute status/control bits.
        v=0
        for bit in range(8):
            inp=self.pc_upper_input if bit>=4 else self.pc_lower_input
            if (self.pc_in if inp else self.pc_out)&(1<<bit):v|=1<<bit
        if self.gb_mode==1:
            if self.pb_input:
                v=(v&~0x07)|(self.intrb<<0)|(self.ibfb<<1) # PC2 is STB input, externally supplied
            else:
                v=(v&~0x07)|(self.intrb<<0)|(self.obfb_n<<1) # PC2 is ACK input
        if self.ga_mode==1:
            if self.pa_input:v=(v&~0x38)|(self.intra<<3)|(self.ibfa<<5) # PC4 STB input
            else:v=(v&~0xC8)|(self.intra<<3)|(self.obfa_n<<7) # PC6 ACK input
        elif self.ga_mode==2:
            v=(v&~0xF8)|(self.intra<<3)|(self.ibfa<<5)|(self.obfa_n<<7)
        return v&255
    def strobe_a(self,data):
        if not (self.ga_mode==2 or (self.ga_mode==1 and self.pa_input)):raise RuntimeError('Port A not strobed input')
        self.pa_in=data&255; self.ibfa=True; self._update_intr()
    def acknowledge_a(self):
        if not (self.ga_mode==2 or (self.ga_mode==1 and not self.pa_input)):raise RuntimeError('Port A not strobed output')
        self.obfa_n=True; self._update_intr()
    def strobe_b(self,data):
        if not (self.gb_mode==1 and self.pb_input):raise RuntimeError('Port B not strobed input')
        self.pb_in=data&255; self.ibfb=True; self._update_intr()
    def acknowledge_b(self):
        if not (self.gb_mode==1 and not self.pb_input):raise RuntimeError('Port B not strobed output')
        self.obfb_n=True; self._update_intr()
    def _update_intr(self):
        if self.ga_mode==2:self.intra=(self.inte_a_in and self.ibfa) or (self.inte_a_out and self.obfa_n)
        elif self.ga_mode==1:self.intra=(self.inte_a_in and self.ibfa) if self.pa_input else (self.inte_a_out and self.obfa_n)
        else:self.intra=False
        self.intrb=(self.inte_b and (self.ibfb if self.pb_input else self.obfb_n)) if self.gb_mode==1 else False
