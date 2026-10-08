#!/usr/bin/env python3
import json
from pathlib import Path
R=Path(__file__).resolve().parents[1]; V=R.parent
p=json.load(open(R/'compiler_db/ISA8_Ethernet.glue.cp44.json'))
assert p['format']=='VMB-GLUE-IR-1' and p['ready'] and len(p['operations'])==56
assert p['statistics']=={'platform_net':41,'internal_net':15}
assert not p['errors']
# Ensure multiplexed real nets survive rather than being flattened into guessed point-to-point adapters.
bd7=next(x for x in p['operations'] if x['net']=='/BD7')
assert set(bd7['binding_kinds'])=={'external_memory_bus','method_data','serial_protocol'}
t=json.load(open(R/'library_templates/programmable_logic_fpga.template.json'))
assert t['class']=='fpga' and t['status']=='template_not_a_part_record'
s=(V/'Board Scripts/Samples/BOARD-SCRIPT-PROTOTYPE-02-PROGRAMMABLE.bs').read_text()
for w in ['program U_FPGA','pinmap U_FPGA','io_bank U_FPGA.BANK0','clock SYSCLK']: assert w in s
print('CP44 backend-neutral glue classification + FPGA component-language model PASS')
