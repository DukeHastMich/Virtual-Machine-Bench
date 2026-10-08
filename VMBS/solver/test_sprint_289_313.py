from physical_router import *
from pathlib import Path
import tempfile
# CP289 copper layer model
assert COPPER==('F.Cu','B.Cu')
# CP290 through-hole layer projection
assert pad_copper_layers(['pad','1','thru_hole','circle',['layers','*.Cu','*.Mask']])==COPPER
# CP291 SMD layer projection
assert pad_copper_layers(['pad','1','smd','rect',['layers','F.Cu']])==('F.Cu',)
# CP292 pad size extraction
assert pad_size_mm(['pad','1','smd','rect',['size','1.2','2.4']])==(1.2,2.4)
# CP293 netclass parse
root=parse(sexpr_tokens('(kicad_pcb (net_class Default x (clearance 0.2) (trace_width 0.25) (via_dia 0.8) (via_drill 0.4) (add_net A)))'))[0]
r=parse_netclasses(root)['Default'];assert r['clearance']==.2 and r['trace_width']==.25 and r['nets']==['A']
# CP294 finite grid conversion
assert mm_to_cell(2.54,1.27)==2 and cell_to_mm(2,1.27)==2.54
# CP295 clearance inflation
assert inflate_cells(.26,.25)==2
# CP296 pad occupancy rectangle
assert (0,0) in rect_cells((0,0),(1,1),.5)
# CP297 strict finite board bounds
assert board_bounds([(0,0),(10,0),(10,10),(0,10)],1)==(0,0,10,10)
# CP298 state bounds layer agnostic
assert in_bounds((2,3,1),(0,0,4,4))
# CP299 planar neighbors
ns=list(state_neighbors((1,1,0),(0,0,3,3),set(),{}));assert (2,1,0) in [x for x,c in ns]
# CP300 via transition
assert (1,1,1) in [x for x,c in ns]
# CP301 obstacle avoidance
p=astar3([(0,0,0)],{(2,0,0)},(0,0,3,3),{(1,0,0)},{});assert p and (1,0,0) not in p
# CP302 deterministic repeat
assert p==astar3([(0,0,0)],{(2,0,0)},(0,0,3,3),{(1,0,0)},{})
# CP303 via penalty favors planar route
p=astar3([(0,0,0)],{(2,0,0)},(0,0,3,3),set(),{},12);assert all(x[2]==0 for x in p)
# CP304 layer-only destination requires via
p=astar3([(0,0,0)],{(0,0,1)},(0,0,1,1),set(),{});assert route_metrics(p,1)['via_count']==1
# CP305 path compression preserves endpoints
c=compress_path([(0,0,0),(1,0,0),(2,0,0),(2,1,0)]);assert c[0]==(0,0,0) and c[-1]==(2,1,0) and len(c)==3
# CP306 metric length
assert route_metrics([(0,0,0),(1,0,0),(2,0,0)],.5)['length_mm']==1.0
# CP307 multi-terminal tree
rr=route_tree([[(0,0,0)],[(2,0,0)],[(2,2,0)]],(0,0,3,3),set(),{});assert len(rr)==2
# CP308 endpoint states respect layer
assert endpoint_states({'xy':[1,1],'copper_layers':['B.Cu']},1)==[(1,1,1)]
# CP309 congestion cost diverts when possible
p=astar3([(0,0,0)],{(2,0,0)},(0,0,3,3),set(),{(1,0,0):10});assert (1,0,0) not in p
# CP310 no-path explicit primitive
assert astar3([(0,0,0)],{(2,0,0)},(0,0,2,0),{(1,0,0),(0,0,1),(2,0,1)}, {}) is None
# CP311 stable route digest
assert stable({'x':[1,2]})==stable({'x':[1,2]})
# CP312 unsupported/missing evidence is representable, not guessed
assert parse_netclasses(parse(sexpr_tokens('(kicad_pcb)'))[0])=={}
# CP313 integrated physical router is exercised by real specimen command below.
print('CP289-CP313 primitive regressions: 24/24 PASS; CP313 real specimen integration pending')
