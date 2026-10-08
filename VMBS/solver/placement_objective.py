def hpwl(ps):
 x=[p[0] for p in ps];y=[p[1] for p in ps];return max(x)-min(x)+max(y)-min(y)
