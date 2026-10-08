#!/usr/bin/env python3
"""Source-level conformance check for VMB Board Script prototype 0.

This does not replace a VB.NET compile.  It independently checks the grammar
specimens, metadata integrity, expected negative test, project inclusion, and a
small set of source-structure invariants in environments without dotnet.
"""
from __future__ import annotations
import json, re, sys
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
TOK = re.compile(r'''\s*(?:(?P<comment>\#.*|//.*)|(?P<string>"(?:\\.|[^"])*")|(?P<number>-?\d+(?:\.\d+)?)|(?P<ident>[A-Za-z_$][A-Za-z0-9_$-]*)|(?P<sym>[{}:.=;]))''')

class ParseError(RuntimeError): pass

def lex(text):
    out=[]; pos=0
    while pos < len(text):
        if text[pos:].strip()=="": break
        m=TOK.match(text,pos)
        if not m:
            line=text.count("\n",0,pos)+1
            raise ParseError(f"unlexable input at line {line}: {text[pos:pos+20]!r}")
        pos=m.end()
        if m.group('comment'): continue
        if m.group('string'):
            out.append(('string', bytes(m.group('string')[1:-1], 'utf-8').decode('unicode_escape')))
        elif m.group('number'): out.append(('number',m.group('number')))
        elif m.group('ident'): out.append(('ident',m.group('ident')))
        else: out.append((m.group('sym'),m.group('sym')))
    out.append(('eof',''))
    return out

class Parser:
    def __init__(self,text): self.t=lex(text); self.i=0
    def cur(self): return self.t[self.i]
    def eat(self,k,v=None):
        t=self.cur()
        if t[0]!=k or (v is not None and t[1].lower()!=v.lower()):
            raise ParseError(f"expected {v or k}, got {t}")
        self.i+=1; return t[1]
    def kw(self,s): return self.eat('ident',s)
    def ident(self): return self.eat('ident')
    def num(self): return float(self.eat('number'))
    def value(self):
        k,v=self.cur()
        if k=='string': self.i+=1; return v
        if k=='number': self.i+=1; return float(v)
        if k=='ident': self.i+=1; return {'true':True,'false':False}.get(v.lower(),v)
        raise ParseError('expected value')
    def parse(self):
        self.kw('board'); b={'id':self.ident(),'title':None,'units':'mm','outline':None,'components':[],'nets':[],'clocks':[],'power':[],'programs':[],'constraints':[]}; self.eat('{')
        while self.cur()[0]!='}':
            k=self.cur()[1].lower()
            if k=='title': self.i+=1; b['title']=self.eat('string'); self.eat(';')
            elif k=='units': self.i+=1; b['units']=self.ident(); self.eat(';')
            elif k=='outline': self.i+=1; b['outline']=(self.num(),self.num()); self.eat(';')
            elif k=='component': b['components'].append(self.component())
            elif k=='net': b['nets'].append(self.net())
            elif k=='clock': b['clocks'].append(self.clock())
            elif k=='power': b['power'].append(self.power())
            elif k=='program': b['programs'].append(self.program())
            elif k=='constraint': b['constraints'].append(self.constraint())
            else: raise ParseError(f'unknown board statement {self.cur()}')
        self.eat('}'); self.eat('eof'); return b
    def component(self):
        self.kw('component'); c={'ref':self.ident()}; self.eat(':'); c['part']=self.ident(); self.eat('{'); c['straps']={}; c['place']=None
        while self.cur()[0]!='}':
            k=self.cur()[1].lower()
            if k=='place':
                self.i+=1; x=self.num(); y=self.num(); self.kw('rot'); r=int(self.num()); self.kw('side'); side=self.ident(); self.eat(';'); c['place']=(x,y,r,side)
            elif k=='strap':
                self.i+=1; n=self.ident(); self.eat('='); c['straps'][n]=self.value(); self.eat(';')
            else: raise ParseError(f'unknown component statement {self.cur()}')
        self.eat('}'); return c
    def net(self):
        self.kw('net'); n={'name':self.ident(),'endpoints':[]}; self.eat('{')
        while self.cur()[0]!='}':
            self.kw('endpoint'); ref=self.ident(); self.eat('.'); pin=self.ident(); self.eat(';'); n['endpoints'].append((ref,pin))
        self.eat('}'); return n
    def endpoint(self):
        ref=self.ident(); self.eat('.'); return (ref,self.ident())
    def clock(self):
        self.kw('clock'); x={'name':self.ident()}; self.eat('{')
        while self.cur()[0]!='}':
            k=self.ident().lower()
            if k=='frequency': x['frequency']=(self.num(),self.ident()); self.eat(';')
            elif k=='source': x['source']=self.endpoint(); self.eat(';')
            else: raise ParseError('unknown clock statement '+k)
        self.eat('}'); return x
    def power(self):
        self.kw('power'); x={'name':self.ident(),'members':[]}; self.eat('{')
        while self.cur()[0]!='}':
            k=self.ident().lower()
            if k=='voltage': x['voltage']=(self.num(),self.ident()); self.eat(';')
            elif k=='member': x['members'].append(self.endpoint()); self.eat(';')
            else: raise ParseError('unknown power statement '+k)
        self.eat('}'); return x
    def program(self):
        self.kw('program'); x={'ref':self.ident()}; self.eat('{')
        while self.cur()[0]!='}':
            k=self.ident().lower()
            if k=='kind': x['kind']=self.ident(); self.eat(';')
            elif k=='payload': x['payload']=self.eat('string'); self.eat(';')
            elif k=='required': x['required']=self.value(); self.eat(';')
            else: raise ParseError('unknown program statement '+k)
        self.eat('}'); return x
    def constraint(self):
        self.kw('constraint'); x={'name':self.ident(),'values':{}}; self.eat('{')
        while self.cur()[0]!='}':
            k=self.ident(); self.eat('='); x['values'][k]=self.value(); self.eat(';')
        self.eat('}'); return x

def load_catalog():
    parts={}; bindings={}
    for p in ROOT.rglob('PARTS-METADATA.json'):
        d=json.loads(p.read_text(encoding='utf-8-sig'))
        for x in d.get('parts',[]):
            if not x.get('id'): continue
            if x['id'] in parts: raise AssertionError(f"duplicate part id {x['id']}")
            parts[x['id']]={'data':x,'source':p}
    for p in ROOT.rglob('WORKER-METADATA.json'):
        d=json.loads(p.read_text(encoding='utf-8-sig'))
        for x in d.get('bindings',[]):
            pid=x['part_id']
            if pid not in parts: raise AssertionError(f"worker metadata invents unknown part {pid}")
            bindings[pid]={'data':x,'source':p}
    return parts,bindings

def validate(board,parts,bindings):
    errors=[]
    if board['units'].lower()!='mm': errors.append('units')
    if not board['outline'] or min(board['outline'])<=0: errors.append('outline')
    refs={}
    for c in board['components']:
        if c['ref'] in refs: errors.append('duplicate ref '+c['ref'])
        refs[c['ref']]=c
        if c['part'] not in parts: errors.append('unknown part '+c['part']); continue
        if not c['place']: errors.append('missing placement '+c['ref']); continue
        x,y,r,side=c['place']; w,h=board['outline']
        if not (0<=x<=w and 0<=y<=h): errors.append('outside outline '+c['ref'])
        if r%90: errors.append('rotation '+c['ref'])
        if side.lower() not in ('top','bottom'): errors.append('side '+c['ref'])
        if c['straps'] and c['part'] not in bindings: errors.append('straps without worker '+c['ref'])
    netnames=set()
    for n in board['nets']:
        if n['name'] in netnames: errors.append('duplicate net '+n['name'])
        netnames.add(n['name'])
        if len(n['endpoints'])<2: errors.append('short net '+n['name'])
        for ref,pin in n['endpoints']:
            if ref not in refs: errors.append('unknown ref '+ref); continue
            pid=refs[ref]['part']
            if pid not in parts: continue
            pinset={x['name'].lower() for x in bindings.get(pid,{}).get('data',{}).get('pins',[])}
            if not pinset: errors.append(f'no pin metadata {ref}.{pin}')
            elif pin.lower() not in pinset: errors.append(f'unknown pin {ref}.{pin}')
    for x in board.get('clocks',[]):
        if x.get('frequency',(0,''))[0] <= 0: errors.append('clock frequency '+x['name'])
    for x in board.get('power',[]):
        if x.get('voltage',(0,''))[0] <= 0: errors.append('power voltage '+x['name'])
    for x in board.get('programs',[]):
        if x['ref'] not in refs: errors.append('program unknown ref '+x['ref'])
    return errors

def vb_balance(path):
    text=path.read_text(encoding='utf-8-sig')
    pairs=[('Class','End Class'),('Module','End Module'),('Structure','End Structure'),('Enum','End Enum'),('Interface','End Interface')]
    failures=[]
    for start_kw,end_kw in pairs:
        a=len(re.findall(rf'^\s*(?:Public |Private |Friend |Protected |Shared |NotInheritable |MustInherit |Partial )*{start_kw}\b',text,re.M|re.I))
        b=len(re.findall(rf'^\s*{re.escape(end_kw)}\b',text,re.M|re.I))
        if a!=b: failures.append(f'{start_kw} {a}!={b}')
    return failures

def main():
    parts,bindings=load_catalog()
    good=['Octek-FOX-II-286-Rev3.2.bs','NEAT-CS8221-Reference.bs','BOARD-SCRIPT-PROTOTYPE-01.bs']
    for name in good:
        board=Parser((ROOT/'Board Scripts'/'Samples'/name).read_text()).parse()
        errs=validate(board,parts,bindings)
        if errs: raise AssertionError(f'{name} failed: {errs}')
        print(f'PASS grammar/catalog: {name} ({len(board["components"])} components, {len(board["nets"])} nets)')
    bad=Parser((ROOT/'Board Scripts'/'Samples'/'INVALID-Invented-Part-Must-Fail.bs').read_text()).parse()
    errs=validate(bad,parts,bindings)
    if not any(e.startswith('unknown part') for e in errs): raise AssertionError('negative invented-part test did not fail as required')
    print('PASS negative test: invented part rejected')

    # Exact sourced NEAT pins used by the sample must stay pinned to the captured PLCC84 numbers.
    expected={
      'ct-82c211':{'PROCCLK':2,'RESET4':38,'HLDA1':40,'PAR_ERR':11,'ACEN':47},
      'ct-82c212':{'PROCCLK':83,'RESET4':12,'HLDA1':19,'DLE':45,'DRD':33},
      'ct-82c215':{'HLDA1':53,'PAR_ERR':83,'ACEN':14,'DLE':76,'DRD':74},
    }
    for pid,pins in expected.items():
        got={x['name']:x.get('number') for x in bindings[pid]['data']['pins']}
        for name,num in pins.items():
            if got.get(name)!=num: raise AssertionError(f'{pid}.{name} expected pin {num}, got {got.get(name)}')
    print('PASS NEAT sourced pin subset')

    # FOX HT12 script's current 4 MiB power-on straps must encode the same REG10
    # value as Ht12PowerOnStraps.SystemConfigurationValue: 6 | 10h | (3 << 5) = 76h.
    fox=Parser((ROOT/'Board Scripts'/'Samples'/'Octek-FOX-II-286-Rev3.2.bs').read_text()).parse()
    ht12=next(c for c in fox['components'] if c['part']=='headland-ht12-a')
    st=ht12['straps']
    reg10=(int(st['RamConfigurationCode']) & 7)
    if st['AtBusLowSpeed']: reg10 |= 0x08
    if st['ZeroWaitMainMemory']: reg10 |= 0x10
    reg10 |= (int(st['ExtraWaitStateMode']) & 3) << 5
    if st['TcStrapHigh']: reg10 |= 0x80
    if reg10 != 0x76: raise AssertionError(f'FOX HT12 REG10 expected 76h, got {reg10:02X}h')
    print('PASS FOX HT12 strap encoding: REG10=76h')

    # Board Script must converge on the real FOX graph by reusing the compiled
    # HT12 worker, not by constructing a parallel HeadlandHt12 behind the scenes.
    adapter=(ROOT/'Hardware'/'Board Script'/'BoardScriptRuntimeAdapter.vb').read_text(encoding='utf-8-sig')
    foxboard=(ROOT/'Motherboards'/'Octek'/'FOX II 286 Rev 3.2'/'FoxII286Rev32Board.vb').read_text(encoding='utf-8-sig')
    cli=(ROOT/'Hardware'/'Board Script'/'BoardScriptCli.vb').read_text(encoding='utf-8-sig')
    if 'ht12WorkerInBed.PowerOnStraps, ht12WorkerInBed.Silicon' not in adapter:
        raise AssertionError('Board Script adapter does not feed compiled HT12 worker into FOX board')
    if 'Object.ReferenceEquals(boardInBed.Chipset, ht12WorkerInBed.Silicon)' not in adapter:
        raise AssertionError('Board Script adapter does not assert HT12 object identity reuse')
    if 'Public Sub New(strapsInBed As Ht12PowerOnStraps, ht12InBed As HeadlandHt12)' not in foxboard:
        raise AssertionError('FOX board cannot accept the compiled HT12 worker')
    if 'BoardScriptRuntimeAdapter.BuildMotherboardGraph(compiledInBed)' not in cli:
        raise AssertionError('Board Script CLI does not exercise runtime graph construction for FOX')
    print('PASS Board Script -> FOX runtime graph convergence seam')

    # The split NEAT prototype starts from the same reset register values as the
    # current green monolithic worker for the surfaces it owns.
    live=(ROOT/'MotherboardDevices.vb').read_text(encoding='utf-8-sig')
    split=(ROOT/'Hardware'/'Chipsets'/'Chips and Technologies'/'CS8221 NEAT'/'CtNeatBoardScriptWorkers.vb').read_text(encoding='utf-8-sig')
    for text,label in [('_ra1 = &H45','live 82C211 REG61'),('_ra2 = &H18','live 82C211 REG62'),('_rb(1) = &HE','live 82C212 REG65'),('_rb(6) = &H80','live 82C212 REG6A'),('_rb(7) = &H6B','live 82C212 REG6B'),('_ipcConfiguration = &HC0','live 82C206 REG01')]:
        if text not in live: raise AssertionError('missing '+label)
    for text,label in [('_reg61 = &H45','split 82C211 REG61'),('_reg62 = &H18','split 82C211 REG62'),('_registers(1) = &HE','split 82C212 REG65'),('_registers(6) = &H80','split 82C212 REG6A'),('_registers(7) = &H6B','split 82C212 REG6B'),('_configuration = &HC0','split 82C206 REG01')]:
        if text not in split: raise AssertionError('missing '+label)
    print('PASS split NEAT reset-surface parity with live worker')

    project=ET.parse(ROOT/'VirtualComputer.Modern.vbproj').getroot()
    inc=[e.attrib.get('Include','') for e in project.iter() if e.tag.endswith('Compile')]
    if len(inc)!=len(set(x.lower() for x in inc)): raise AssertionError('duplicate Compile entry')
    required=[
      r'Hardware\Board Script\BoardScriptModel.vb',r'Hardware\Board Script\BoardScriptLexer.vb',
      r'Hardware\Board Script\BoardScriptParser.vb',r'Hardware\Board Script\BoardScriptCatalog.vb',
      r'Hardware\Board Script\BoardScriptWorkers.vb',r'Hardware\Board Script\BoardScriptCompiler.vb',
      r'Hardware\Board Script\BoardScriptRuntimeAdapter.vb',r'Hardware\Board Script\BoardScriptCli.vb',r'Hardware\Chipsets\Headland\HT12\HeadlandHt12.vb',
      r'Hardware\Chipsets\Chips and Technologies\CS8221 NEAT\CtNeatBoardScriptWorkers.vb']
    missing=[x for x in required if x not in inc]
    if missing: raise AssertionError(f'missing Compile entries: {missing}')
    print(f'PASS project Compile list: {len(inc)} unique entries')

    files=[ROOT/x.replace('\\','/') for x in required]
    failures=[]
    for p in files:
        f=vb_balance(p)
        if f: failures.append(f'{p.relative_to(ROOT)}: {f}')
    if failures: raise AssertionError('VB structural balance failed: '+'; '.join(failures))
    print('PASS coarse VB type/module block-balance scan')
    print('NOTE no dotnet/MSBuild/VBC toolchain present; this is not a compile claim')

if __name__=='__main__':
    try: main()
    except Exception as e:
        print('FAIL:',e,file=sys.stderr); sys.exit(1)
