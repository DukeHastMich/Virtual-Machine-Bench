#!/usr/bin/env python3
import json,tempfile,pathlib,subprocess,sys,hashlib
R=pathlib.Path(__file__).resolve().parents[1]; c=json.load(open(R/'compiler_db/ISA8_Ethernet.board_code.cp19.json'))
# Change one property domain in board code; generated VB must change without emitter edits.
for x in c['instructions']:
 if x['op']=='DEFINE_PROPERTY' and x['module']=='U1' and x['property']=='IO_BASE': x['allowed_values']=x['allowed_values'][:8]
with tempfile.TemporaryDirectory() as td:
 p=pathlib.Path(td); src=p/'code.json'; out=p/'board.vb'; src.write_text(json.dumps(c)); subprocess.check_call([sys.executable,str(R/'tools/vb_board_emit.py'),str(src),str(out)],stdout=subprocess.DEVNULL)
 orig=(R/'generated_vmb/ISA8_Ethernet.GeneratedBoard.vb').read_text(); new=out.read_text(); result={'test':'board-code property mutation changes generated VB','original_sha256':hashlib.sha256(orig.encode()).hexdigest(),'mutated_sha256':hashlib.sha256(new.encode()).hexdigest(),'pass':orig!=new}
 (R/'compiler_db/cp20_vb_mutation_test.json').write_text(json.dumps(result,indent=2)+'\n'); print(json.dumps(result,indent=2)); raise SystemExit(0 if result['pass'] else 1)
