def generate(ref,p,s=1):
 x,y,r=p;return [(ref,q) for q in [(x-s,y,r),(x+s,y,r),(x,y-s,r),(x,y+s,r),(x,y,(r+90)%360)]]
