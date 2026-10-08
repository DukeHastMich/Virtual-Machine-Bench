#!/usr/bin/env python3
"""Reference functional models for reusable 74xx IC modules.

This is intentionally digital/behavioral, not an analog TTL simulator.  A pin value is
0, 1, or None (high impedance).  The same MODULE_IDs are implemented in VMB's
DigitalLogicIc.vb runtime; these Python models give VMBS deterministic executable
regression coverage without requiring the Windows/.NET toolchain.
"""
from dataclasses import dataclass, field
from typing import Dict, Optional

Bit = Optional[int]  # None == high impedance

def b(v):
    if v is None: return None
    return 1 if int(v) else 0

def inv(v):
    v=b(v); return None if v is None else 1-v

def gate2(a,c,op):
    a=b(a); c=b(c)
    if a is None or c is None: return None
    return op(a,c)

def comb(module_id: str, pins: Dict[str, Bit]) -> Dict[str, Bit]:
    """Evaluate combinational outputs for a supported MODULE_ID."""
    p=lambda n:b(pins.get(n))
    o={}
    if module_id == 'VMB.LOGIC.74XX00':
        for i in range(1,5): o[f'{i}Y']=inv(gate2(p(f'{i}A'),p(f'{i}B'),lambda x,y:x&y))
    elif module_id == 'VMB.LOGIC.74XX02':
        for i in range(1,5): o[f'{i}Y']=inv(gate2(p(f'{i}A'),p(f'{i}B'),lambda x,y:x|y))
    elif module_id == 'VMB.LOGIC.74XX04':
        for i in range(1,7): o[f'{i}Y']=inv(p(f'{i}A'))
    elif module_id == 'VMB.LOGIC.74XX08':
        for i in range(1,5): o[f'{i}Y']=gate2(p(f'{i}A'),p(f'{i}B'),lambda x,y:x&y)
    elif module_id == 'VMB.LOGIC.74XX32':
        for i in range(1,5): o[f'{i}Y']=gate2(p(f'{i}A'),p(f'{i}B'),lambda x,y:x|y)
    elif module_id == 'VMB.LOGIC.74XX86':
        for i in range(1,5): o[f'{i}Y']=gate2(p(f'{i}A'),p(f'{i}B'),lambda x,y:x^y)
    elif module_id == 'VMB.LOGIC.74XX157':
        sel=p('S'); en=p('E_N')==0
        for i in range(1,5):
            if not en: o[f'{i}Y']=0
            elif sel==0: o[f'{i}Y']=p(f'{i}A')
            elif sel==1: o[f'{i}Y']=p(f'{i}B')
            else: o[f'{i}Y']=None
    elif module_id == 'VMB.LOGIC.74XX688':
        en=p('G_N')==0
        equal=all(p(f'P{i}') is not None and p(f'Q{i}') is not None and p(f'P{i}')==p(f'Q{i}') for i in range(8))
        o['P_EQ_Q_N']=0 if en and equal else 1
    elif module_id == 'VMB.LOGIC.74XX138':
        enabled=(p('G1')==1 and p('G2A_N')==0 and p('G2B_N')==0)
        sel=(p('A') or 0)|((p('B') or 0)<<1)|((p('C') or 0)<<2)
        for i in range(8): o[f'Y{i}_N']=0 if enabled and i==sel else 1
    elif module_id == 'VMB.LOGIC.74XX244':
        for bank in (1,2):
            en=p(f'OE{bank}_N')==0
            for i in range(1,5): o[f'{bank}Y{i}']=p(f'{bank}A{i}') if en else None
    elif module_id in ('VMB.LOGIC.74XX125','VMB.LOGIC.74XX126'):
        active_low=module_id.endswith('125')
        for i in range(1,5):
            g=p(f'{i}G_N' if active_low else f'{i}G'); en=(g==0) if active_low else (g==1)
            o[f'{i}Y']=p(f'{i}A') if en else None
    elif module_id == 'VMB.LOGIC.74XX51':
        # two independent AND-OR-INVERT functions; channel 1 is 2x2, channel 2 is 2x3 on LS51.
        a=(p('1A') or 0)&(p('1B') or 0)&(p('1C') or 0); c=(p('1D') or 0)&(p('1E') or 0)&(p('1F') or 0); o['1Y']=1-(a|c)
        a=(p('2A') or 0)&(p('2B') or 0); c=(p('2C') or 0)&(p('2D') or 0); o['2Y']=1-(a|c)
    elif module_id == 'VMB.LOGIC.74XX139':
        for sec in (1,2):
            en=p(f'{sec}G_N')==0; sel=(p(f'{sec}A') or 0)|((p(f'{sec}B') or 0)<<1)
            for n in range(4): o[f'{sec}Y{n}_N']=0 if en and n==sel else 1
    elif module_id == 'VMB.LOGIC.74XX151':
        en=p('G_N')==0; sel=(p('A') or 0)|((p('B') or 0)<<1)|((p('C') or 0)<<2)
        y=p(f'D{sel}') if en else 0; o['Y']=y; o['W']=None if y is None else 1-y
    elif module_id == 'VMB.LOGIC.74XX153':
        sel=(p('A') or 0)|((p('B') or 0)<<1)
        for sec in (1,2): o[f'{sec}Y']=p(f'{sec}C{sel}') if p(f'{sec}G_N')==0 else 0
    elif module_id == 'VMB.LOGIC.74XX148':
        en=p('EI_N')==0; active=[n for n in range(8) if p(f'I{n}_N')==0] if en else []; w=max(active) if active else None
        if w is None: o.update(A0_N=1,A1_N=1,A2_N=1,GS_N=1,EO_N=0 if en else 1)
        else: o.update(A0_N=1-(w&1),A1_N=1-((w>>1)&1),A2_N=1-((w>>2)&1),GS_N=0,EO_N=1)
    elif module_id == 'VMB.LOGIC.74XX245':
        en=p('OE_N')==0; direction=p('DIR')
        for i in range(1,9):
            if not en: o[f'A{i}']=None; o[f'B{i}']=None
            elif direction==1: o[f'B{i}']=p(f'A{i}'); o[f'A{i}']=None
            elif direction==0: o[f'A{i}']=p(f'B{i}'); o[f'B{i}']=None
            else: o[f'A{i}']=None; o[f'B{i}']=None
    else:
        raise KeyError(module_id)
    return o

@dataclass
class DualDff74:
    q: Dict[int,int]=field(default_factory=lambda:{1:0,2:0})
    last_clk: Dict[int,int]=field(default_factory=lambda:{1:0,2:0})
    def step(self,pins:Dict[str,Bit])->Dict[str,Bit]:
        for n in (1,2):
            pre=b(pins.get(f'{n}PRE_N')); clr=b(pins.get(f'{n}CLR_N'))
            clk=b(pins.get(f'{n}CLK')) or 0
            if pre==0 and clr==1:self.q[n]=1
            elif clr==0 and pre==1:self.q[n]=0
            elif pre==0 and clr==0:self.q[n]=1  # forbidden input combination; physical outputs overridden HIGH below
            elif self.last_clk[n]==0 and clk==1:
                v=b(pins.get(f'{n}D'))
                if v is not None:self.q[n]=v
            self.last_clk[n]=clk
        o={}
        for n in (1,2):
            pre=b(pins.get(f'{n}PRE_N')); clr=b(pins.get(f'{n}CLR_N'))
            if pre==0 and clr==0:
                o[f'{n}Q']=1; o[f'{n}Q_N']=1
            else:
                o[f'{n}Q']=self.q[n]; o[f'{n}Q_N']=1-self.q[n]
        return o

@dataclass
class Register273:
    q: Dict[int,int]=field(default_factory=lambda:{i:0 for i in range(1,9)})
    last_clk:int=0
    def step(self,pins:Dict[str,Bit])->Dict[str,Bit]:
        clr=b(pins.get('CLR_N')); clk=b(pins.get('CLK')) or 0
        if clr==0:
            for i in self.q:self.q[i]=0
        elif self.last_clk==0 and clk==1:
            for i in range(1,9):
                v=b(pins.get(f'D{i}'))
                if v is not None:self.q[i]=v
        self.last_clk=clk
        return {f'Q{i}':self.q[i] for i in range(1,9)}

@dataclass
class Counter161:
    synchronous_clear:bool=False
    value:int=0
    last_clk:int=0
    def step(self,pins:Dict[str,Bit])->Dict[str,Bit]:
        clk=b(pins.get('CLK')) or 0; clr=b(pins.get('CLR_N'))
        if not self.synchronous_clear and clr==0:self.value=0
        elif self.last_clk==0 and clk==1:
            if self.synchronous_clear and clr==0:self.value=0
            elif b(pins.get('LOAD_N'))==0:
                self.value=sum((b(pins.get(n)) or 0)<<i for i,n in enumerate(('A','B','C','D')))
            elif b(pins.get('ENP'))==1 and b(pins.get('ENT'))==1:self.value=(self.value+1)&15
        self.last_clk=clk
        o={n:(self.value>>i)&1 for i,n in enumerate(('QA','QB','QC','QD'))}
        o['RCO']=1 if self.value==15 and b(pins.get('ENT'))==1 else 0
        return o

@dataclass
class Counter393:
    value:Dict[int,int]=field(default_factory=lambda:{1:0,2:0})
    last_clk:Dict[int,int]=field(default_factory=lambda:{1:0,2:0})
    def step(self,pins:Dict[str,Bit])->Dict[str,Bit]:
        o={}
        for n in (1,2):
            clk=b(pins.get(f'{n}CLK')) or 0
            if b(pins.get(f'{n}CLR'))==1:self.value[n]=0
            elif self.last_clk[n]==1 and clk==0:self.value[n]=(self.value[n]+1)&15
            self.last_clk[n]=clk
            for i,l in enumerate('ABCD'):o[f'{n}Q{l}']=(self.value[n]>>i)&1
        return o

@dataclass
class Latch373:
    q: Dict[int,int]=field(default_factory=lambda:{i:0 for i in range(1,9)})
    def step(self,pins:Dict[str,Bit])->Dict[str,Bit]:
        if b(pins.get('LE'))==1:
            for i in range(1,9):
                v=b(pins.get(f'D{i}'))
                if v is not None:self.q[i]=v
        enabled=b(pins.get('OE_N'))==0
        return {f'Q{i}':self.q[i] if enabled else None for i in range(1,9)}

@dataclass
class Register374:
    q: Dict[int,int]=field(default_factory=lambda:{i:0 for i in range(1,9)})
    last_clk:int=0
    def step(self,pins:Dict[str,Bit])->Dict[str,Bit]:
        clk=b(pins.get('CLK')) or 0
        if self.last_clk==0 and clk==1:
            for i in range(1,9):
                v=b(pins.get(f'D{i}'))
                if v is not None:self.q[i]=v
        self.last_clk=clk
        enabled=b(pins.get('OE_N'))==0
        return {f'Q{i}':self.q[i] if enabled else None for i in range(1,9)}

@dataclass
class Register174:
    q:list=field(default_factory=lambda:[0]*6); last_clk:int=0
    def step(self,pins):
        clk=b(pins.get('CLK')) or 0
        if b(pins.get('CLR_N'))==0:self.q=[0]*6
        elif self.last_clk==0 and clk==1:self.q=[b(pins.get(f'D{i}')) or 0 for i in range(6)]
        self.last_clk=clk; return {f'Q{i}':self.q[i] for i in range(6)}

@dataclass
class Shift166:
    q:list=field(default_factory=lambda:[0]*8); last_clk:int=0
    def step(self,pins):
        clk=b(pins.get('CLK')) or 0
        if b(pins.get('CLR_N'))==0:self.q=[0]*8
        elif self.last_clk==0 and clk==1 and b(pins.get('CLK_INH'))==0:
            if b(pins.get('SH_LD_N'))==0:self.q=[b(pins.get(chr(ord('A')+i))) or 0 for i in range(8)]
            else:self.q=[b(pins.get('SER')) or 0]+self.q[:7]
        self.last_clk=clk; return {'QH':self.q[7]}

SUPPORTED={
 'VMB.LOGIC.74XX00','VMB.LOGIC.74XX02','VMB.LOGIC.74XX04','VMB.LOGIC.74XX08',
 'VMB.LOGIC.74XX32','VMB.LOGIC.74XX86','VMB.LOGIC.74XX138','VMB.LOGIC.74XX157','VMB.LOGIC.74XX688','VMB.LOGIC.74XX244',
 'VMB.LOGIC.74XX245','VMB.LOGIC.74XX373','VMB.LOGIC.74XX374','VMB.LOGIC.74XX74',
 'VMB.LOGIC.74XX125','VMB.LOGIC.74XX126','VMB.LOGIC.74XX51','VMB.LOGIC.74XX139','VMB.LOGIC.74XX148','VMB.LOGIC.74XX151','VMB.LOGIC.74XX153','VMB.LOGIC.74XX166','VMB.LOGIC.74XX174','VMB.LOGIC.74XX273','VMB.LOGIC.74XX161','VMB.LOGIC.74XX163','VMB.LOGIC.74XX393'}

@dataclass
class Register175:
    q:list=field(default_factory=lambda:[0]*4); last_clk:int=0
    def step(self,pins):
        clk=b(pins.get('CLK')) or 0
        if b(pins.get('CLR_N'))==0:self.q=[0]*4
        elif self.last_clk==0 and clk==1:self.q=[b(pins.get(f'D{i}')) or 0 for i in range(4)]
        self.last_clk=clk
        o={}
        for i in range(4):o[f'Q{i}']=self.q[i];o[f'Q{i}_N']=1-self.q[i]
        return o

@dataclass
class Counter19x:
    bcd:bool=False; value:int=0; last_clk:int=0
    def step(self,pins):
        # TI LS190/191: LOAD_N is asynchronous; counting occurs on low-to-high CLK when CTEN_N is low.
        if b(pins.get('LOAD_N'))==0:
            self.value=sum((b(pins.get(n)) or 0)<<i for i,n in enumerate(('A','B','C','D')))
            self.value%=10 if self.bcd else 16
        clk=b(pins.get('CLK')) or 0
        if self.last_clk==0 and clk==1 and b(pins.get('CTEN_N'))==0 and b(pins.get('LOAD_N'))!=0:
            down=b(pins.get('D_U'))==1
            mod=10 if self.bcd else 16
            self.value=(self.value+(-1 if down else 1))%mod
        self.last_clk=clk
        o={n:(self.value>>i)&1 for i,n in enumerate(('QA','QB','QC','QD'))}
        terminal=(self.value==(0 if b(pins.get('D_U'))==1 else ((9 if self.bcd else 15))))
        o['MAX_MIN']=1 if terminal else 0
        o['RCO_N']=0 if terminal and clk==0 and b(pins.get('CTEN_N'))==0 else 1
        return o

# CP54 combinational additions are wrapped so earlier comb behavior remains unchanged.
_comb_cp53=comb
def comb(module_id: str, pins: Dict[str, Bit]) -> Dict[str, Bit]:
    p=lambda n:b(pins.get(n)); o={}
    if module_id=='VMB.LOGIC.74XX42':
        v=(p('A') or 0)|((p('B') or 0)<<1)|((p('C') or 0)<<2)|((p('D') or 0)<<3)
        for n in range(10):o[f'Y{n}_N']=0 if v<10 and n==v else 1
        return o
    if module_id=='VMB.LOGIC.74XX85':
        a=sum((p(f'A{i}') or 0)<<i for i in range(4)); q=sum((p(f'B{i}') or 0)<<i for i in range(4))
        if a>q:return {'A_GT_B':1,'A_EQ_B':0,'A_LT_B':0}
        if a<q:return {'A_GT_B':0,'A_EQ_B':0,'A_LT_B':1}
        # Equal local words propagate the lower-order cascade decision.
        return {'A_GT_B':p('I_GT') or 0,'A_EQ_B':p('I_EQ') or 0,'A_LT_B':p('I_LT') or 0}
    return _comb_cp53(module_id,pins)

SUPPORTED.update({'VMB.LOGIC.74XX42','VMB.LOGIC.74XX85','VMB.LOGIC.74XX175','VMB.LOGIC.74XX190','VMB.LOGIC.74XX191'})

# CP55 manual-backed arithmetic/parity/dual-clock counter/universal shift register.
@dataclass
class Counter192193:
    bcd: bool=False; value:int=0; last_up:int=1; last_down:int=1
    def step(self,pins):
        if b(pins.get('CLR'))==1:self.value=0
        elif b(pins.get('LOAD_N'))==0:
            self.value=sum((b(pins.get(n)) or 0)<<i for i,n in enumerate(('A','B','C','D')))%(10 if self.bcd else 16)
        else:
            up=b(pins.get('UP')); down=b(pins.get('DOWN')); mod=10 if self.bcd else 16
            if self.last_up==0 and up==1 and down==1:self.value=(self.value+1)%mod
            if self.last_down==0 and down==1 and up==1:self.value=(self.value-1)%mod
        self.last_up=b(pins.get('UP')) or 0; self.last_down=b(pins.get('DOWN')) or 0
        o={n:(self.value>>i)&1 for i,n in enumerate(('QA','QB','QC','QD'))}
        o['CARRY_N']=0 if self.value==(9 if self.bcd else 15) and self.last_up==0 else 1
        o['BORROW_N']=0 if self.value==0 and self.last_down==0 else 1
        return o

@dataclass
class Shift194:
    q:list=field(default_factory=lambda:[0]*4); last_clk:int=0
    def step(self,pins):
        clk=b(pins.get('CLK')) or 0
        if b(pins.get('CLR_N'))==0:self.q=[0]*4
        elif self.last_clk==0 and clk==1:
            mode=((b(pins.get('S1')) or 0)<<1)|(b(pins.get('S0')) or 0)
            if mode==1:self.q=[b(pins.get('SR')) or 0]+self.q[:3]
            elif mode==2:self.q=self.q[1:]+[b(pins.get('SL')) or 0]
            elif mode==3:self.q=[b(pins.get(n)) or 0 for n in ('A','B','C','D')]
        self.last_clk=clk
        return {n:self.q[i] for i,n in enumerate(('QA','QB','QC','QD'))}

_comb_cp54=comb
def comb(module_id: str, pins: Dict[str, Bit]) -> Dict[str, Bit]:
    if module_id=='VMB.LOGIC.74XX280':
        parity=0
        for n in range(9):parity^=(b(pins.get(f'I{n}')) or 0)
        return {'EVEN':parity,'ODD':1-parity}
    if module_id=='VMB.LOGIC.74XX283':
        a=sum((b(pins.get(f'A{i}')) or 0)<<i for i in range(4)); q=sum((b(pins.get(f'B{i}')) or 0)<<i for i in range(4)); s=a+q+(b(pins.get('C0')) or 0)
        o={f'S{i}':(s>>i)&1 for i in range(4)};o['C4']=(s>>4)&1;return o
    return _comb_cp54(module_id,pins)
SUPPORTED.update({'VMB.LOGIC.74XX192','VMB.LOGIC.74XX193','VMB.LOGIC.74XX194','VMB.LOGIC.74XX280','VMB.LOGIC.74XX283'})

# CP56 manual-backed latch/J-K/ripple-counter tier.
@dataclass
class DualJk73:
    q:list=field(default_factory=lambda:[0,0]); last_clk:list=field(default_factory=lambda:[0,0])
    def step(self,pins):
        o={}
        for ch in range(2):
            n=ch+1; clk=b(pins.get(f'{n}CLK')) or 0
            if b(pins.get(f'{n}CLR_N'))==0:self.q[ch]=0
            elif self.last_clk[ch]==1 and clk==0:
                j=b(pins.get(f'{n}J')) or 0;k=b(pins.get(f'{n}K')) or 0
                if j and not k:self.q[ch]=1
                elif k and not j:self.q[ch]=0
                elif j and k:self.q[ch]^=1
            self.last_clk[ch]=clk;o[f'{n}Q']=self.q[ch];o[f'{n}Q_N']=1-self.q[ch]
        return o

@dataclass
class DualJk76:
    q:list=field(default_factory=lambda:[0,0]); last_clk:list=field(default_factory=lambda:[0,0])
    def step(self,pins):
        o={}
        for ch in range(2):
            n=ch+1; clk=b(pins.get(f'{n}CLK')) or 0; pre=b(pins.get(f'{n}PRE_N')); clr=b(pins.get(f'{n}CLR_N'))
            if pre==0 and clr!=0:self.q[ch]=1
            elif clr==0 and pre!=0:self.q[ch]=0
            elif pre==0 and clr==0:
                # forbidden steady state: both physical outputs high; q retained only as hidden state
                self.last_clk[ch]=clk;o[f'{n}Q']=1;o[f'{n}Q_N']=1;continue
            elif self.last_clk[ch]==1 and clk==0:
                j=b(pins.get(f'{n}J')) or 0;k=b(pins.get(f'{n}K')) or 0
                if j and not k:self.q[ch]=1
                elif k and not j:self.q[ch]=0
                elif j and k:self.q[ch]^=1
            self.last_clk[ch]=clk;o[f'{n}Q']=self.q[ch];o[f'{n}Q_N']=1-self.q[ch]
        return o

@dataclass
class Latch75:
    q:list=field(default_factory=lambda:[0,0,0,0])
    def step(self,pins):
        for ch in range(4):
            en=b(pins.get('C1' if ch<2 else 'C2')) or 0
            if en:self.q[ch]=b(pins.get(f'D{ch}')) or 0
        o={}
        for ch in range(4):o[f'Q{ch}']=self.q[ch];o[f'Q{ch}_N']=1-self.q[ch]
        return o

@dataclass
class Ripple90x:
    kind:int=90; qa:int=0; section:int=0; last_a:int=0; last_b:int=0
    def step(self,pins):
        # TI SN74LS90/92/93: separate /2 CKA stage plus /5,/6,/8 CKB section; falling-edge count behavior.
        r0a=b(pins.get('R0_1')) or 0;r0b=b(pins.get('R0_2')) or 0
        if r0a and r0b:self.qa=0;self.section=0
        elif self.kind==90 and (b(pins.get('R9_1')) or 0) and (b(pins.get('R9_2')) or 0):self.qa=1;self.section=4
        else:
            ca=b(pins.get('CKA')) or 0;cb=b(pins.get('CKB')) or 0
            if self.last_a==1 and ca==0:self.qa^=1
            if self.last_b==1 and cb==0:self.section=(self.section+1)%({90:5,92:6,93:8}[self.kind])
            self.last_a=ca;self.last_b=cb
        return {'QA':self.qa,'QB':self.section&1,'QC':(self.section>>1)&1,'QD':(self.section>>2)&1}

SUPPORTED.update({'VMB.LOGIC.74XX73','VMB.LOGIC.74XX75','VMB.LOGIC.74XX76','VMB.LOGIC.74XX90','VMB.LOGIC.74XX92','VMB.LOGIC.74XX93'})

# CP57: pinout sibling 83A and parallel-access shift register 95B.
@dataclass
class Shift95B:
    q:list=field(default_factory=lambda:[0]*4); last_clk1:int=0; last_clk2:int=0
    def step(self,pins):
        c1=b(pins.get('CLK1')) or 0;c2=b(pins.get('CLK2')) or 0;mode=b(pins.get('MODE')) or 0
        if mode and self.last_clk2==1 and c2==0:
            self.q=[b(pins.get(n)) or 0 for n in ('A','B','C','D')]
        elif not mode and self.last_clk1==1 and c1==0:
            # Shift-right QA toward QD; serial input enters QA.
            self.q=[b(pins.get('SER')) or 0]+self.q[:3]
        self.last_clk1=c1;self.last_clk2=c2
        return {n:self.q[i] for i,n in enumerate(('QA','QB','QC','QD'))}

_comb_cp56=comb
def comb(module_id: str, pins: Dict[str, Bit]) -> Dict[str, Bit]:
    if module_id=='VMB.LOGIC.74XX83A':
        return _comb_cp56('VMB.LOGIC.74XX283',pins)
    return _comb_cp56(module_id,pins)
SUPPORTED.update({'VMB.LOGIC.74XX83A','VMB.LOGIC.74XX95B'})


# CP58 manual-backed primitive/SSI gate tier.
# Open-collector outputs use None for the released/high-impedance state.
_comb_cp57=comb
def comb(module_id: str, pins: Dict[str, Bit]) -> Dict[str, Bit]:
    p=lambda n:b(pins.get(n)); o={}
    if module_id in ('VMB.LOGIC.74XX03',):
        for i in range(1,5):
            a=p(f'{i}A'); c=p(f'{i}B'); o[f'{i}Y']=0 if a==1 and c==1 else (None if a is not None and c is not None else None)
        return o
    if module_id in ('VMB.LOGIC.74XX05','VMB.LOGIC.74XX06'):
        for i in range(1,7):
            a=p(f'{i}A'); o[f'{i}Y']=0 if a==1 else (None if a==0 else None)
        return o
    if module_id=='VMB.LOGIC.74XX14':
        for i in range(1,7):o[f'{i}Y']=inv(p(f'{i}A'))
        return o
    spec={
      'VMB.LOGIC.74XX10':(3,3,'NAND'), 'VMB.LOGIC.74XX11':(3,3,'AND'),
      'VMB.LOGIC.74XX20':(2,4,'NAND'), 'VMB.LOGIC.74XX21':(2,4,'AND'),
      'VMB.LOGIC.74XX27':(3,3,'NOR'), 'VMB.LOGIC.74XX30':(1,8,'NAND')}
    if module_id in spec:
        gates,width,kind=spec[module_id]
        for g in range(1,gates+1):
            vals=[p(f'{g}{chr(64+n)}') for n in range(1,width+1)]
            if any(v is None for v in vals): y=None
            else:
                v=all(vals) if kind in ('NAND','AND') else any(vals)
                y=int((not v) if kind in ('NAND','NOR') else v)
            o[f'{g}Y']=y
        return o
    return _comb_cp57(module_id,pins)
SUPPORTED.update({'VMB.LOGIC.74XX03','VMB.LOGIC.74XX05','VMB.LOGIC.74XX06','VMB.LOGIC.74XX10','VMB.LOGIC.74XX11','VMB.LOGIC.74XX14','VMB.LOGIC.74XX20','VMB.LOGIC.74XX21','VMB.LOGIC.74XX27','VMB.LOGIC.74XX30'})


# CP59 manual-backed remaining simple gate / tri-state mux tier.
_comb_cp58=comb
def comb(module_id: str, pins: Dict[str, Bit]) -> Dict[str, Bit]:
    p=lambda n:b(pins.get(n));o={}
    if module_id=='VMB.LOGIC.74XX01':
        for i in range(1,5):
            a=p(f'{i}A');c=p(f'{i}B');o[f'{i}Y']=0 if a==1 and c==1 else None
        return o
    if module_id=='VMB.LOGIC.74XX132':
        # Binary functional projection of Schmitt NAND; analog hysteresis remains electrical-domain data.
        for i in range(1,5):
            a=p(f'{i}A');c=p(f'{i}B');o[f'{i}Y']=None if a is None or c is None else 1-(a&c)
        return o
    if module_id=='VMB.LOGIC.74XX133':
        vals=[p(chr(65+i)) for i in range(13)];o['Y']=None if any(v is None for v in vals) else int(not all(vals));return o
    if module_id=='VMB.LOGIC.74XX136':
        for i in range(1,5):
            a=p(f'{i}A');c=p(f'{i}B');o[f'{i}Y']=0 if a is not None and c is not None and (a^c)==0 else None
        return o
    if module_id=='VMB.LOGIC.74XX251':
        if p('G_N')!=0:return {'Y':None,'W':None}
        sel=(p('A') or 0)|((p('B') or 0)<<1)|((p('C') or 0)<<2);y=p(f'D{sel}');return {'Y':y,'W':None if y is None else 1-y}
    return _comb_cp58(module_id,pins)
SUPPORTED.update({'VMB.LOGIC.74XX01','VMB.LOGIC.74XX132','VMB.LOGIC.74XX133','VMB.LOGIC.74XX136','VMB.LOGIC.74XX251'})

# CP60 timing-aware monostables. pulse_width_ns is the board-resolved timing interval;
# conversion from physical Rext/Cext remains an electrical-model responsibility.
@dataclass
class MonostableDual:
    retriggerable: bool
    pulse_width_ns: int
    active_until: list = field(default_factory=lambda:[0,0])
    last_a: list = field(default_factory=lambda:[1,1])
    last_b: list = field(default_factory=lambda:[0,0])
    def step_at(self, now_ns:int, pins:Dict[str,Bit])->Dict[str,Bit]:
        out={}
        for ch in range(2):
            n=str(ch+1); a=b(pins.get(n+'A')); bb=b(pins.get(n+'B')); clr=b(pins.get(n+'CLR_N'))
            a=1 if a is None else a; bb=0 if bb is None else bb; clr=1 if clr is None else clr
            if clr==0:
                self.active_until[ch]=now_ns
            else:
                # TI '123/'221 gated trigger: negative A transition while B high, or positive B transition while A low.
                trig=(self.last_a[ch]==1 and a==0 and bb==1) or (self.last_b[ch]==0 and bb==1 and a==0)
                if trig and (self.retriggerable or now_ns>=self.active_until[ch]):
                    self.active_until[ch]=now_ns+self.pulse_width_ns
            self.last_a[ch]=a; self.last_b[ch]=bb
            q=1 if now_ns < self.active_until[ch] else 0
            out[n+'Q']=q; out[n+'Q_N']=1-q
        return out

class Monostable123(MonostableDual):
    def __init__(self,pulse_width_ns:int): super().__init__(True,pulse_width_ns)
class Monostable221(MonostableDual):
    def __init__(self,pulse_width_ns:int): super().__init__(False,pulse_width_ns)

SUPPORTED.update({'VMB.LOGIC.74XX123','VMB.LOGIC.74XX221'})


# CP131-155: exact TI SN74HC identities preserve HC physical/electrical truth in
# Component Store while sharing the already-regressed computer-visible 74xx logic.
_TI_HC_ALIAS = {
    'VMB.LOGIC.TI_SN74HC00': 'VMB.LOGIC.74XX00',
    'VMB.LOGIC.TI_SN74HC02': 'VMB.LOGIC.74XX02',
    'VMB.LOGIC.TI_SN74HC04': 'VMB.LOGIC.74XX04',
    'VMB.LOGIC.TI_SN74HC08': 'VMB.LOGIC.74XX08',
    'VMB.LOGIC.TI_SN74HC10': 'VMB.LOGIC.74XX10',
    'VMB.LOGIC.TI_SN74HC11': 'VMB.LOGIC.74XX11',
    'VMB.LOGIC.TI_SN74HC14': 'VMB.LOGIC.74XX14',
    'VMB.LOGIC.TI_SN74HC20': 'VMB.LOGIC.74XX20',
    'VMB.LOGIC.TI_SN74HC21': 'VMB.LOGIC.74XX21',
    'VMB.LOGIC.TI_SN74HC27': 'VMB.LOGIC.74XX27',
    'VMB.LOGIC.TI_SN74HC30': 'VMB.LOGIC.74XX30',
    'VMB.LOGIC.TI_SN74HC32': 'VMB.LOGIC.74XX32',
    'VMB.LOGIC.TI_SN74HC42': 'VMB.LOGIC.74XX42',
    'VMB.LOGIC.TI_SN74HC85': 'VMB.LOGIC.74XX85',
    'VMB.LOGIC.TI_SN74HC86': 'VMB.LOGIC.74XX86',
    'VMB.LOGIC.TI_SN74HC125': 'VMB.LOGIC.74XX125',
    'VMB.LOGIC.TI_SN74HC126': 'VMB.LOGIC.74XX126',
    'VMB.LOGIC.TI_SN74HC138': 'VMB.LOGIC.74XX138',
    'VMB.LOGIC.TI_SN74HC139': 'VMB.LOGIC.74XX139',
    'VMB.LOGIC.TI_SN74HC148': 'VMB.LOGIC.74XX148',
    'VMB.LOGIC.TI_SN74HC151': 'VMB.LOGIC.74XX151',
    'VMB.LOGIC.TI_SN74HC153': 'VMB.LOGIC.74XX153',
    'VMB.LOGIC.TI_SN74HC157': 'VMB.LOGIC.74XX157',
    'VMB.LOGIC.TI_SN74HC244': 'VMB.LOGIC.74XX244',
    'VMB.LOGIC.TI_SN74HC245': 'VMB.LOGIC.74XX245',
 }
_comb_cp130 = comb
def comb(module_id: str, pins: Dict[str, Bit]) -> Dict[str, Bit]:
    return _comb_cp130(_TI_HC_ALIAS.get(module_id,module_id),pins)
SUPPORTED.update(_TI_HC_ALIAS)
# CP156-180 TI HC identity aliases
_TI_HC_ALIAS.update({
    'VMB.LOGIC.TI_SN74HC51': 'VMB.LOGIC.74XX51',
    'VMB.LOGIC.TI_SN74HC73': 'VMB.LOGIC.74XX73',
    'VMB.LOGIC.TI_SN74HC74': 'VMB.LOGIC.74XX74',
    'VMB.LOGIC.TI_SN74HC75': 'VMB.LOGIC.74XX75',
    'VMB.LOGIC.TI_SN74HC76': 'VMB.LOGIC.74XX76',
    'VMB.LOGIC.TI_SN74HC90': 'VMB.LOGIC.74XX90',
    'VMB.LOGIC.TI_SN74HC92': 'VMB.LOGIC.74XX92',
    'VMB.LOGIC.TI_SN74HC93': 'VMB.LOGIC.74XX93',
    'VMB.LOGIC.TI_SN74HC123': 'VMB.LOGIC.74XX123',
    'VMB.LOGIC.TI_SN74HC132': 'VMB.LOGIC.74XX132',
    'VMB.LOGIC.TI_SN74HC133': 'VMB.LOGIC.74XX133',
    'VMB.LOGIC.TI_SN74HC136': 'VMB.LOGIC.74XX136',
    'VMB.LOGIC.TI_SN74HC161': 'VMB.LOGIC.74XX161',
    'VMB.LOGIC.TI_SN74HC163': 'VMB.LOGIC.74XX163',
    'VMB.LOGIC.TI_SN74HC166': 'VMB.LOGIC.74XX166',
    'VMB.LOGIC.TI_SN74HC174': 'VMB.LOGIC.74XX174',
    'VMB.LOGIC.TI_SN74HC175': 'VMB.LOGIC.74XX175',
    'VMB.LOGIC.TI_SN74HC190': 'VMB.LOGIC.74XX190',
    'VMB.LOGIC.TI_SN74HC191': 'VMB.LOGIC.74XX191',
    'VMB.LOGIC.TI_SN74HC192': 'VMB.LOGIC.74XX192',
    'VMB.LOGIC.TI_SN74HC193': 'VMB.LOGIC.74XX193',
    'VMB.LOGIC.TI_SN74HC194': 'VMB.LOGIC.74XX194',
    'VMB.LOGIC.TI_SN74HC221': 'VMB.LOGIC.74XX221',
    'VMB.LOGIC.TI_SN74HC251': 'VMB.LOGIC.74XX251',
    'VMB.LOGIC.TI_SN74HC273': 'VMB.LOGIC.74XX273',
})
SUPPORTED.update(_TI_HC_ALIAS)

# CP181-188: additional primary-TI HC/CD74HC identities
_TI_HC_ALIAS.update({
    'VMB.LOGIC.TI_SN74HC03': 'VMB.LOGIC.74XX03',
    'VMB.LOGIC.TI_SN74HC05': 'VMB.LOGIC.74XX05',
    'VMB.LOGIC.TI_SN74HC373': 'VMB.LOGIC.74XX373',
    'VMB.LOGIC.TI_SN74HC374': 'VMB.LOGIC.74XX374',
    'VMB.LOGIC.TI_SN74HC393': 'VMB.LOGIC.74XX393',
    'VMB.LOGIC.TI_SN74HC688': 'VMB.LOGIC.74XX688',
    'VMB.LOGIC.TI_CD74HC280': 'VMB.LOGIC.74XX280',
    'VMB.LOGIC.TI_CD74HC283': 'VMB.LOGIC.74XX283',
})
SUPPORTED.update(_TI_HC_ALIAS)
