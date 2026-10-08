from ic_logic_models import comb,SUPPORTED
for m in ('125','126','51'): assert f'VMB.LOGIC.74XX{m}' in SUPPORTED
x=comb('VMB.LOGIC.74XX125',{'1G_N':0,'1A':1,'2G_N':1,'2A':1}); assert x['1Y']==1 and x['2Y'] is None
x=comb('VMB.LOGIC.74XX126',{'1G':1,'1A':1,'2G':0,'2A':1}); assert x['1Y']==1 and x['2Y'] is None
assert comb('VMB.LOGIC.74XX51',{'1A':1,'1B':1,'1C':1,'1D':0,'1E':0,'1F':0})['1Y']==0
assert comb('VMB.LOGIC.74XX51',{'1A':0,'1B':1,'1C':1,'1D':0,'1E':0,'1F':0})['1Y']==1
print('CP53 simple buffer/AOI manual-backed tests: PASS')
