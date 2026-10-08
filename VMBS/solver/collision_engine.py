from geometry_kernel import overlap
def collisions(p):
 r=sorted(p);return [(a,b) for i,a in enumerate(r) for b in r[i+1:] if overlap(p[a],p[b])]
