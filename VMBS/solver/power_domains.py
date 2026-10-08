def common(rs):
 lo=max(x[0] for x in rs);hi=min(x[1] for x in rs);return (lo,hi) if lo<=hi else None
