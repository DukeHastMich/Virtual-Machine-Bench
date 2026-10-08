class Tabu:
 def __init__(self,n=32):self.n=n;self.q=[]
 def add(self,x):self.q=(self.q+[x])[-self.n:]
 def has(self,x):return x in self.q
