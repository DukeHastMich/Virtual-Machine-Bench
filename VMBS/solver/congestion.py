def cost(cell,usage,history):return 1+5*usage.get(cell,0)+3*history.get(cell,0)
def bump(history,cells):
 h=dict(history)
 for c in cells:h[c]=h.get(c,0)+1
 return h
