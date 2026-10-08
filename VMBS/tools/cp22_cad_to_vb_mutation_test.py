#!/usr/bin/env python3
"""Full anti-smuggling regression: mutate KiCad topology and require final generated VB/constructor domain to change."""
import re,subprocess,json,tempfile,sys,pathlib,hashlib,shutil
R=pathlib.Path(__file__).resolve().parents[1]; src=(R/'upstream/isa8_eth/KiCad/ISA8_Ethernet.kicad_pcb').read_text()
start=src.find('(module My_Components:Switch_DIP_4_Positions'); end=src.find('\n  (module ',start+1); block=src[start:end]
if start<0 or '(fp_text reference SW3' not in block: raise SystemExit('SW3 not found')
pat=r'(\(pad 1 [\s\S]*?\(net )117( "Net-\(RN2-Pad8\)"\)\))'; nb,n=re.subn(pat,r'\g<1>32 "GND"))',block,count=1)
if n!=1: raise SystemExit('mutation failed')
mut=src[:start]+nb+src[end:]
with tempfile.TemporaryDirectory() as td:
 td=pathlib.Path(td); cad=td/'mut.kicad_pcb'; out=td/'out'; cad.write_text(mut)
 subprocess.check_call([sys.executable,str(R/'tools/build_board_module.py'),str(cad),'--outdir',str(out),'--class-name','ISA8_Ethernet_Mutated'],stdout=subprocess.DEVNULL)
 orig=R/'generated_vmb/e2e_cp21'; op=json.load(open(orig/'construction.ir.json')); mp=json.load(open(out/'construction.ir.json'))
 def io(p): return next(x for x in p['decoded_properties'] if x['instance_id']=='U1' and x['property']=='IO_BASE')['allowed_values']
 ov,mv=io(op),io(mp); ovb=(orig/'GeneratedBoard.vb').read_bytes(); mvb=(out/'GeneratedBoard.vb').read_bytes()
 result={'test':'KiCad topology mutation propagates through BS/Analyzer/Constructor/BoardCode into VB','original_io_count':len(ov),'mutated_io_count':len(mv),'original_vb_sha256':hashlib.sha256(ovb).hexdigest(),'mutated_vb_sha256':hashlib.sha256(mvb).hexdigest(),'mutated_reference_runtime_valid':json.load(open(out/'reference.runtime.json'))['valid']}
 result['pass']=len(ov)==16 and len(mv)==8 and ovb!=mvb and result['mutated_reference_runtime_valid']
 (R/'compiler_db/cp22_cad_to_vb_mutation_test.json').write_text(json.dumps(result,indent=2)+'\n'); print(json.dumps(result,indent=2)); raise SystemExit(0 if result['pass'] else 1)
