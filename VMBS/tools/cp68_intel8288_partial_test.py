#!/usr/bin/env python3
from intel8288_model import Intel8288StatusDecoder as D
exp=['INTA#','IORC#','IOWC#',None,'MRDC#','MRDC#','MWTC#',None]
for n,e in enumerate(exp):assert D.decode((n>>2)&1,(n>>1)&1,n&1)==e
print('CP68 Intel 8288 status-decode manual-backed test: PASS (clocked command/control timing remains TITS UP)')
