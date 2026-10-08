from copper_referee import route_existing_copper_violations,audit_routes,stable
from existing_copper import existing_obstacles
B=(0,0,10,10); step=1.0
E={'segments':[{'start':(2,2),'end':(4,2),'width_mm':.2,'layer':'F.Cu','net':'B'}],'vias':[],'zones':[],'gaps':[]}
# 364 explicit other-net copper collision
v=route_existing_copper_violations('A',{(3,2,0)},E,step,0,B);assert len(v)==1
# 365 same-net exemption
assert not route_existing_copper_violations('B',{(3,2,0)},E,step,0,B)
# 366 layer separation
assert not route_existing_copper_violations('A',{(3,2,1)},E,step,0,B)
# 367 provenance source
assert v[0]['provenance']['source']=='kicad_explicit_copper'
# 368 state provenance
assert v[0]['state']==[3,2,0]
# 369 clearance expands collision envelope
assert route_existing_copper_violations('A',{(3,4,0)},E,step,1,B)
# 370 zero clearance avoids adjacent state
assert not route_existing_copper_violations('A',{(3,4,0)},E,step,0,B)
# 371 audit PASS
R={'A':{'occupancy':{(8,8,0)},'clearance_mm':0}};assert audit_routes(R,E,step,B)['state']=='PASS'
# 372 audit violation
R2={'A':{'occupancy':{(3,2,0)},'clearance_mm':0}};assert audit_routes(R2,E,step,B)['state']=='VIOLATION'
# 373 explicit evidence gap dominates state
EG={**E,'gaps':[{'code':'TITS_UP_EXISTING_SEGMENT_GEOMETRY'}]};assert audit_routes(R,EG,step,B)['state']=='TITS_UP'
# 374 missing route clearance is TITS_UP
assert audit_routes({'A':{'occupancy':set()}},E,step,B)['state']=='TITS_UP'
# 375 violation count stable
assert audit_routes(R2,E,step,B)['violation_count']==1
# 376 per-net count
assert audit_routes(R2,E,step,B)['per_net']['A']==1
# 377 deterministic digest
assert audit_routes(R2,E,step,B)['digest']==audit_routes(R2,E,step,B)['digest']
# 378 order-independent occupancy
assert route_existing_copper_violations('A',{(4,2,0),(3,2,0)},E,step,0,B)==route_existing_copper_violations('A',{(3,2,0),(4,2,0)},E,step,0,B)
# 379 bounds clipping
assert not route_existing_copper_violations('A',{(3,2,0)},E,step,0,(8,8,10,10))
# 380 via both layers
EV={'segments':[],'vias':[{'at':(5,5),'size_mm':.8,'net':'B'}],'zones':[],'gaps':[]};assert len(route_existing_copper_violations('A',{(5,5,0),(5,5,1)},EV,step,0,B))==2
# 381 same-net via exempt
assert not route_existing_copper_violations('B',{(5,5,0)},EV,step,0,B)
# 382 zone collision explicit polygon interior
EZ={'segments':[],'vias':[],'zones':[{'polygon_mm':[(1,1),(4,1),(4,4),(1,4)],'layer':'F.Cu','net':'B'}],'gaps':[]};assert route_existing_copper_violations('A',{(2,2,0)},EZ,step,0,B)
# 383 zone same-net exempt
assert not route_existing_copper_violations('B',{(2,2,0)},EZ,step,0,B)
# 384 stable hash changes on violation
assert audit_routes(R,E,step,B)['digest']!=audit_routes(R2,E,step,B)['digest']
# 385 multiple nets sorted provenance
M={'Z':{'occupancy':{(3,2,0)},'clearance_mm':0},'A':{'occupancy':{(3,2,0)},'clearance_mm':0}};a=audit_routes(M,E,step,B);assert list(a['per_net'])==['A','Z']
# 386 empty routes pass with complete evidence
assert audit_routes({},E,step,B)['state']=='PASS'
# 387 no invented electrical semantics
assert all(x['code']=='EXISTING_COPPER_COLLISION' for x in a['violations'])
# 388 deterministic primitive suite complete
assert stable({'x':1})==stable({'x':1})
print('CP364-CP388 primitive regressions: 25/25 PASS')
