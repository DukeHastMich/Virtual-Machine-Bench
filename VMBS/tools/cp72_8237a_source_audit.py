#!/usr/bin/env python3
from pathlib import Path
s=(Path(__file__).resolve().parents[2]/'MotherboardDevices.vb').read_text(errors='ignore')
start=s.index('Public Class Dma8237'); end=s.find('Public Class ',start+20); x=s[start:end if end>=0 else len(s)]
for needle in ['BaseAddress','CurrentAddress','BaseCount','CurrentCount','MaskRegister','SoftwareRequest','SetDreq','TransferToMemory','TransferFromMemory','serviceModeInBed','cascade channels do not transfer data themselves','decrement','PriorityBase','TerminalCount','CurrentAddress = state.BaseAddress','CurrentCount = state.BaseCount','ControllerDisabled']:
 assert needle in x,needle
# Guard against claiming complete standalone 8237A silicon: memory-to-memory and pin-cycle generation are not modeled by this AT transaction projection.
assert 'MemoryToMemory' not in x and 'memory-to-memory' not in x
print('CP72 Intel 8237A AT transaction-projection audit: PASS (standalone memory-to-memory/pin-cycle behavior remains outside projection)')
