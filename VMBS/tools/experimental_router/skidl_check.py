"""Bridge KiCad's exported pin-typed netlist to upstream SKiDL ERC."""
import argparse
import json
import logging
import os
from pathlib import Path
import sys
from collections import Counter
from sexpr import parse, child, children, atom

local = Path(os.environ.get('VMBS_SKIDL_DEPS',str(Path(__file__).resolve().parent/'deps-skidl')))
if local.is_dir(): sys.path.insert(0, str(local))
os.environ.setdefault('MPLCONFIGDIR',str(Path.cwd()/'.matplotlib'))
os.environ['APPDATA']=str(Path.cwd()/'appdata')
Path(os.environ['APPDATA']).mkdir(exist_ok=True)

def value(node, key):
    return atom(child(node, key)[1])

def check(netlist, board=None):
    from skidl import Circuit, Part, Pin, Net
    from skidl.pin import pin_types
    from skidl.logger import erc_logger
    from importlib.metadata import version
    root = parse(Path(netlist).read_text(encoding='utf-8-sig'))[0]
    if root[0] != 'export': raise ValueError('Expected KiCad exported netlist')
    types = dict(input='INPUT', output='OUTPUT', bidirectional='BIDIR', tri_state='TRISTATE',
                 passive='PASSIVE', unspecified='UNSPEC', power_in='PWRIN', power_out='PWROUT',
                 open_collector='OPENCOLL', open_emitter='OPENEMIT', NotConnected='NOCONNECT')
    types.update(BiDi='BIDIR', openCol='OPENCOLL', openEm='OPENEMIT')
    types['3state']='TRISTATE'
    libraries = {(value(p,'lib'),value(p,'part')):p for p in children(child(root,'libparts',[]),'libpart')}
    for p in list(libraries.values()):
        for alias in children(child(p,'aliases',[]),'alias'):
            libraries[value(p,'lib'),atom(alias[1])] = p
    circuit = Circuit(); pins = {}; counts = Counter()
    for comp in children(child(root,'components',[]),'comp'):
        ref = value(comp,'ref'); source = child(comp,'libsource')
        lib = libraries[(value(source,'lib'),value(source,'part'))]
        part_pins = []
        for p in children(child(lib,'pins',[]),'pin'):
            kind = value(p,'type'); counts[kind] += 1
            pin = Pin(num=value(p,'num'), name=value(p,'name'), func=getattr(pin_types,types[kind]))
            if (ref,pin.num) in pins: raise ValueError(f'Duplicate pin {ref}.{pin.num}')
            pins[ref,pin.num] = pin; part_pins.append(pin)
        Part(name=value(comp,'value'),tool='skidl',ref=ref,circuit=circuit,pins=part_pins)
    expected = {}; nets = children(child(root,'nets',[]),'net')
    if not nets or not pins: raise ValueError('Empty circuit cannot pass ERC')
    for n in nets:
        name = value(n,'name'); net = Net(name,circuit=circuit)
        for p in children(n,'node'):
            key = value(p,'ref'),value(p,'pin')
            if key in expected: raise ValueError(f'Pin appears on multiple nets: {key}')
            expected[key] = name; net += pins[key]
    parity = None
    if board:
        pcb = parse(Path(board).read_text(encoding='utf-8-sig'))[0]; actual = {}
        for fp in children(pcb,'module') + children(pcb,'footprint'):
            refs = [atom(t[2]) for t in children(fp,'fp_text') if t[1]=='reference']
            if not refs:
                refs = [atom(t[2]) for t in children(fp,'property') if atom(t[1])=='Reference']
            if not refs: raise ValueError('Footprint missing reference')
            for p in children(fp,'pad'):
                n = child(p,'net')
                if n and n[1]!='0': actual[refs[0],atom(p[1])] = atom(n[2])
        mismatches = [dict(ref=k[0],pin=k[1],netlist=expected.get(k),pcb=actual.get(k))
                      for k in sorted(expected.keys() | actual.keys()) if expected.get(k)!=actual.get(k)]
        parity = dict(status='failed' if mismatches else 'passed', mismatches=mismatches,
                      connected_pins=len(expected))
    issues = []
    class Capture(logging.Handler):
        def emit(self, record):
            if record.levelno >= logging.WARNING:
                issues.append(dict(severity='error' if record.levelno>=logging.ERROR else 'warning',
                                   description=record.getMessage()))
    handler = Capture(); erc_logger.addHandler(handler)
    try: circuit.ERC()
    finally: erc_logger.removeHandler(handler)
    errors = sum(i['severity']=='error' for i in issues)
    return dict(engine='SKiDL',version=version('skidl'),source='https://github.com/devbisme/skidl',
                status='failed' if errors or (parity and parity['status']=='failed') else 'review' if issues else 'passed',
                errors=errors,warnings=len(issues)-errors,findings=issues,parity=parity,
                nets=len(nets),pins=len(pins),pin_types=dict(counts),
                limitations=['Exported netlists do not preserve schematic no-connect markers or all ERC exclusions.',
                             'External connector power/drive assumptions have not been added.'])

if __name__=='__main__':
    ap=argparse.ArgumentParser(description=__doc__)
    ap.add_argument('netlist');ap.add_argument('--board');ap.add_argument('--output',required=True)
    a=ap.parse_args()
    try: result=check(a.netlist,a.board)
    except Exception as exc: result=dict(status='error',reason=f'{type(exc).__name__}: {exc}')
    Path(a.output).write_text(json.dumps(result,indent=2),encoding='utf-8')
    raise SystemExit(3 if result['status']=='error' else 0)
