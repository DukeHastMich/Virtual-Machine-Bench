#!/usr/bin/env python3
import json, subprocess, tempfile, pathlib, sys
ROOT=pathlib.Path(__file__).resolve().parents[1]
base=json.load(open(ROOT/'compiler_db/ISA8_Ethernet.board_ir.cp16.json'))
# Mutate semantic input only: remove IOS0 user-control binding and force its Analyzer-level source/default to 0,
# then constrain IO_BASE decoded domain as a mutation fixture would. Constructor must faithfully change output.
mut=json.loads(json.dumps(base))
for mc in mut['module_configuration']:
 if mc['ref']=='U1':
  for p in mc['pin_configuration']:
   if p['pin']=='IOS0': p['source']={'kind':'INTERNAL_DEFAULT','value':0}
for d in mut['configuration_decode']:
 if d['ref']=='U1' and d['property']=='IO_BASE':
  for s in d['selectors']:
   if s['pin']=='IOS0': s['source']={'kind':'INTERNAL_DEFAULT','value':0}; s['possible_bits']=[0]
  d['allowed_values']=[x for x in d['allowed_values'] if ((x-0x300)//0x20)%2==0]  # fixture domain changed
with tempfile.TemporaryDirectory() as td:
 td=pathlib.Path(td); bi=td/'mut.json'; out=td/'plan.json'; bi.write_text(json.dumps(mut))
 subprocess.check_call([sys.executable,str(ROOT/'tools/board_constructor.py'),str(bi),str(ROOT/'compiler_db/code_store.json'),str(out)],stdout=subprocess.DEVNULL)
 orig=json.load(open(ROOT/'compiler_db/ISA8_Ethernet.constructor_ir.cp17.json')); new=json.load(open(out))
 def ios0(p):
  return next(x for x in p['configuration_bindings'] if x['instance_id']=='U1' and x['pin']=='IOS0')
 def io(p): return next(x for x in p['decoded_properties'] if x['instance_id']=='U1' and x['property']=='IO_BASE')
 result={'test':'constructor follows semantic IR mutation','original_ios0':ios0(orig),'mutated_ios0':ios0(new),
         'original_io_count':len(io(orig)['allowed_values']),'mutated_io_count':len(io(new)['allowed_values'])}
 result['pass']=result['original_ios0']!=result['mutated_ios0'] and result['original_io_count']!=result['mutated_io_count']
 (ROOT/'compiler_db/cp17_mutation_test.json').write_text(json.dumps(result,indent=2)+'\n')
 print(json.dumps(result,indent=2)); raise SystemExit(0 if result['pass'] else 1)
