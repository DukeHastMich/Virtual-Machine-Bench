class Routes:
 def __init__(self):self.r={};self.u={}
 def rip(self,n):
  for c in self.r.pop(n,[]):self.u[c]-=1
 def put(self,n,p):
  self.rip(n);self.r[n]=list(p)
  for c in p:self.u[c]=self.u.get(c,0)+1
 def over(self):return {c for c,v in self.u.items() if v>1}
