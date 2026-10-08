#!/usr/bin/env python3
from pathlib import Path
p=Path(__file__).resolve().parents[2]/'HardwareBus.vb'; s=p.read_text(errors='ignore')
start=s.index('Public Class Pit8253'); end=s.find('Public Class ',start+20); x=s[start:end if end>=0 else len(s)]
for needle in ['WriteReadBack','LatchStatus','LatchCount','DecodeBcdCounter','EncodeBcdCounter','Case 0','Case 1','Case 2, 3','Case 4','Case 5','channel.Mode > 5','NullCount','SetGate']:
 assert needle in x, needle
assert 'Public Class Pit8253' in x and 'ReadPort' in x and 'WritePort' in x
print('CP70 Intel 8254 existing-worker manual/source audit: PASS')
