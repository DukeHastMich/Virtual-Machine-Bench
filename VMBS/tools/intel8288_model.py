"""CP68 Intel 8288 status decoder only. Full clocked command timing intentionally remains unresolved."""
class Intel8288StatusDecoder:
 TABLE={0:'INTA#',1:'IORC#',2:'IOWC#',3:None,4:'MRDC#',5:'MRDC#',6:'MWTC#',7:None}
 @classmethod
 def decode(cls,s2,s1,s0):
  if any(x not in (0,1,False,True) for x in (s2,s1,s0)):raise ValueError('status')
  return cls.TABLE[(int(bool(s2))<<2)|(int(bool(s1))<<1)|int(bool(s0))]
