#!/usr/bin/env python3
from pathlib import Path
root=Path(__file__).resolve().parents[1]
s=(root.parent/'generated_vmb/cp48_adapters/GeneratedBoardAdapters.vb').read_text()
assert 'ExecutableProtocolLinkCount As Integer = 1' in s
assert 'AddHandler U1.MicrowirePinsChanged, AddressOf ProtocolLink_0' in s
assert 'U3.SetSerialPins(chipSelect, clock, dataIn)' in s
assert 'U1.SetEepromDataOut(U3.SerialDataOut)' in s
for bad in ['RTL8019_BOOT_ROM','MICROWIRE_93LC46','If U1','If U3','AllocateModule','runtime.Link']:
 assert bad not in (Path(root/'tools/chimera_emit_vb_adapters.py').read_text()),bad
print('CP48 PASS: source-contract-driven executable Microwire glue emitted without board/part/adapter special cases')
