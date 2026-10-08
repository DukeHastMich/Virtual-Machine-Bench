def orient(a,b,c):return (b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])
def inside(p,q):
 x,y=p;c=False
 for i,a in enumerate(q):
  b=q[(i+1)%len(q)]
  if (a[1]>y)!=(b[1]>y) and x<(b[0]-a[0])*(y-a[1])/(b[1]-a[1])+a[0]:c=not c
 return c
def cross(a,b,c,d):
 o=[orient(a,b,c),orient(a,b,d),orient(c,d,a),orient(c,d,b)]
 return o[0]*o[1]<=0 and o[2]*o[3]<=0
def overlap(a,b):return any(inside(p,b) for p in a) or any(inside(p,a) for p in b) or any(cross(a[i],a[(i+1)%len(a)],b[j],b[(j+1)%len(b)]) for i in range(len(a)) for j in range(len(b)))
