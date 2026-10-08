def order(nets):
 def k(n):
  u=n.upper();return (0 if "CLK" in u else 1 if u.startswith("/A") or u.startswith("/D") else 2 if "IRQ" in u else 3,u)
 return sorted(nets,key=k)
