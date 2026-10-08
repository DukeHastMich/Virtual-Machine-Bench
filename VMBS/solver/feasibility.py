R={"UNSAT":0,"TITS_UP_MISSING_EVIDENCE":1,"SOLVED_UNVERIFIED":2,"SOLVED_VERIFIED":3}
def combine(x):return min(x,key=lambda q:R[q]) if x else "TITS_UP_MISSING_EVIDENCE"
