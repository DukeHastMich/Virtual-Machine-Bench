def best(c):return min(c,key=lambda x:(x[0],x[1])) if c else None
def accept(old,new):return new<old
