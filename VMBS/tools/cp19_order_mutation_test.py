#!/usr/bin/env python3
import json,tempfile,pathlib,subprocess,sys
R=pathlib.Path(__file__).resolve().parents[1]; c=json.load(open(R/'compiler_db/ISA8_Ethernet.board_code.cp19.json'))
# Move U1 allocation after first U1 config binding. Validator must reject dependency inversion.
ops=c['instructions']; ai=next(i for i,x in enumerate(ops) if x['op']=='ALLOC_MODULE' and x['id']=='U1'); alloc=ops.pop(ai); bi=next(i for i,x in enumerate(ops) if x['op']=='BIND_CONFIG_PIN' and x['module']=='U1'); ops.insert(bi+1,alloc)
with tempfile.TemporaryDirectory() as td:
 p=pathlib.Path(td)/'bad.json'; p.write_text(json.dumps(c)); q=subprocess.run([sys.executable,str(R/'tools/board_code_validate.py'),str(p)],capture_output=True,text=True)
 out=json.loads(q.stdout); result={'test':'module allocation moved after config binding','validator_exit':q.returncode,'errors':out['errors'],'pass':q.returncode!=0 and any(e['code']=='CONFIG_BEFORE_MODULE' for e in out['errors'])}; (R/'compiler_db/cp19_order_mutation_test.json').write_text(json.dumps(result,indent=2)+'\n'); print(json.dumps(result,indent=2)); raise SystemExit(0 if result['pass'] else 1)
