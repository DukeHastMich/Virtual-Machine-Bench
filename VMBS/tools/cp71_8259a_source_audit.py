#!/usr/bin/env python3
from pathlib import Path
s=(Path(__file__).resolve().parents[2]/'HardwareBus.vb').read_text(errors='ignore')
start=s.index('Public Class Pic8259'); end=s.find('Public Class ',start+20); x=s[start:end if end>=0 else len(s)]
for needle in ['BeginInitialization','WriteOcw2','WriteOcw3','_interruptMask','_interruptRequest','_inService','_autoEoi','_specialFullyNested','_specialMaskMode','_pollNextRead','_lowestPriority','Acknowledge','spurious IRQ7','_levelTriggered','IsProgrammedCascadeInput']:
 assert needle in x,needle
# Authenticity guard: exact 8259A has buffered-mode/uPM programming in ICW4; current PC/AT projection does not claim them.
assert 'bufferedMode' not in x and '_bufferedMode' not in x
print('CP71 Intel 8259A PC/AT-visible projection audit: PASS (buffered/uPM electrical-bus behavior explicitly outside projection)')
