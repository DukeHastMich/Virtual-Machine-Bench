#!/usr/bin/env python3
"""Deterministic existing-copper collision referee for VMBS CP364+.
No inference: only explicit KiCad copper evidence is judged. Unknown evidence remains TITS_UP.
"""
import hashlib,json
from existing_copper import existing_obstacles

def stable(x): return hashlib.sha256(json.dumps(x,sort_keys=True,separators=(',',':')).encode()).hexdigest()

def route_existing_copper_violations(net, occupancy, evidence, step, clearance, bounds):
    blocked=existing_obstacles(evidence,net,step,clearance,bounds)
    hits=sorted(set(occupancy)&blocked)
    return [{'code':'EXISTING_COPPER_COLLISION','net':net,'state':[x,y,l],
             'provenance':{'source':'kicad_explicit_copper','grid_mm':step,'clearance_mm':clearance}}
            for x,y,l in hits]

def audit_routes(routes,evidence,step,bounds):
    violations=[]; gaps=list(evidence.get('gaps',[])); per_net={}
    for net in sorted(routes):
        r=routes[net]; occ=r.get('occupancy',set()); clearance=r.get('clearance_mm')
        if clearance is None:
            gaps.append({'net':net,'code':'TITS_UP_ROUTE_CLEARANCE'});continue
        vv=route_existing_copper_violations(net,occ,evidence,step,clearance,bounds)
        per_net[net]=len(vv);violations.extend(vv)
    serial={'violation_count':len(violations),'gap_count':len(gaps),'per_net':per_net,
            'violations':violations,'evidence_gaps':gaps}
    return {**serial,'state':'TITS_UP' if gaps else ('VIOLATION' if violations else 'PASS'),'digest':stable(serial)}
