STAGES=["NORMALIZE","PLACE","ROUTE","ELECTRICAL","TIMING","VERIFY"]
def next_stage(s):return STAGES[STAGES.index(s)+1] if s!=STAGES[-1] else None
def feedback(stage,code):
 if code.startswith("ROUTE_"):return "ROUTE"
 return "PLACE" if stage in ("ROUTE","ELECTRICAL","TIMING") else None
