from historical_topology import net_copper_states,pad_states,components,audit_net,net_topology_groups,stable
from historical_reuse_router import connect_groups,route_one_reuse
B=(0,0,12,12);S=1.0
E={'segments':[{'start':(1,1),'end':(4,1),'width_mm':.2,'layer':'F.Cu','net':'A'}], 'vias':[], 'zones':[], 'gaps':[]}
# 389 explicit same-net segment raster
assert (2,1,0) in net_copper_states(E,'A',S,B)
# 390 other net excluded
assert not net_copper_states(E,'B',S,B)
# 391 layer identity retained
assert (2,1,1) not in net_copper_states(E,'A',S,B)
# 392 via bridges both layers
EV={'segments':[],'vias':[{'at':(5,5),'size_mm':.8,'net':'A'}],'zones':[],'gaps':[]};q=net_copper_states(EV,'A',S,B);assert (5,5,0) in q and (5,5,1) in q
# 393 zone interior explicit
EZ={'segments':[],'vias':[],'zones':[{'polygon_mm':[(1,1),(4,1),(4,4),(1,4)],'layer':'F.Cu','net':'A'}],'gaps':[]};assert (2,2,0) in net_copper_states(EZ,'A',S,B)
# 394 zone layer identity
assert (2,2,1) not in net_copper_states(EZ,'A',S,B)
P={'ref':'U1','pad':'1','net':'A','xy':[1,1],'size_mm':(1,1),'copper_layers':('F.Cu',)}
# 395 pad raster
assert (1,1,0) in pad_states(P,S,B)
# 396 pad layer identity
assert (1,1,1) not in pad_states(P,S,B)
# 397 finite bounds clipping
assert not pad_states({**P,'xy':[20,20]},S,B)
# 398 planar component adjacency
assert len(components({(1,1,0),(2,1,0)}))==1
# 399 disconnected islands remain separate
assert len(components({(1,1,0),(3,1,0)}))==2
# 400 via adjacency joins layers
assert len(components({(1,1,0),(1,1,1)}))==1
# 401 deterministic component ordering
assert components({(9,9,0),(1,1,0)})==components({(1,1,0),(9,9,0)})
# 402 pad touching copper identified
A=audit_net('A',E,[P],S,B);assert A['pads_touching_copper']==1
# 403 touching single pad topology connected
assert A['historical_pad_connectivity']=='CONNECTED'
# 404 unattached pad explicit TITS_UP
P2={**P,'ref':'U2','xy':[9,9]};A2=audit_net('A',E,[P,P2],S,B);assert A2['historical_pad_connectivity']=='UNRESOLVED' and A2['evidence_gaps'][0]['code'].startswith('TITS_UP_')
# 405 groups preserve unattached pad island
assert len(net_topology_groups('A',E,[P,P2],S,B))==2
# 406 connector can join islands
paths=connect_groups([{(1,1,0)},{(4,1,0)}],B,set(),{});assert paths and paths[0][0]==(1,1,0) and paths[0][-1]==(4,1,0)
# 407 hard obstacle respected
assert connect_groups([{(1,1,0)},{(3,1,0)}],(0,1,4,1),{(2,1,0),(2,1,1)},{}) is None
# 408 layer transition permits bypass when finite state space allows it
assert connect_groups([{(1,1,0)},{(3,1,0)}],(0,0,4,2),{(2,1,0)},{}) is not None
# 409 deterministic connector
assert connect_groups([{(1,1,0)},{(3,1,0)}],B,set(),{})==connect_groups([{(3,1,0)},{(1,1,0)}],B,set(),{})
# 410 already unified topology adds no branch
assert connect_groups([{(1,1,0),(2,1,0)}],B,set(),{})==[]
# 411 reuse router synthetic branch
rule={'name':'Default','trace_width':.2,'clearance':0,'via_dia':.8,'via_drill':.4};r,e=route_one_reuse('A',[P,P2],rule,[P,P2],B,S,{}, {},E);assert r is not None and e is None and r['new_branches']
# 412 reuse router refuses blocked synthetic topology
EB={'segments':E['segments']+[{'start':(5,0),'end':(5,12),'width_mm':2,'layer':'F.Cu','net':'B'},{'start':(5,0),'end':(5,12),'width_mm':2,'layer':'B.Cu','net':'B'}],'vias':[],'zones':[],'gaps':[]};far={**P2,'xy':[9,1]};r,e=route_one_reuse('A',[P,far],rule,[P,far],(0,0,10,2),S,{}, {},EB);assert r is None and e=='TITS_UP_ROUTE_NO_PATH'
# 413 provenance hash stable
assert stable({'mode':'historical_reuse','x':1})==stable({'x':1,'mode':'historical_reuse'})
print('CP389-CP413 primitive regressions: 25/25 PASS')
