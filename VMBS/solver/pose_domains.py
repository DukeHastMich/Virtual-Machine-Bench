from geometry_kernel import inside
def domain(poly,board,xs,ys):
 return [(x,y,0) for x in xs for y in ys if all(inside((px+x,py+y),board) for px,py in poly)]
