def reserve(cells,r):
 return {(x+dx,y+dy) for x,y in cells for dx in range(-r,r+1) for dy in range(-r,r+1) if dx*dx+dy*dy<=r*r}
