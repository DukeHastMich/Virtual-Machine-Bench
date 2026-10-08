from real_board_router import *
from pathlib import Path
import tempfile,json
# CP264 tokenization / CP265 parse
x=parse(sexpr_tokens('(root (net 1 "A") (module X (at 10 20 90) (fp_text reference U1) (pad 1 thru_hole circle (at 2 0) (net 1 "A"))))'))[0];assert x[0]=='root'
# CP266 transform
assert tuple(round(v,6) for v in transform(2,0,10,20,90))==(10.0,18.0)
# CP267 endpoint extraction using synthetic file
with tempfile.TemporaryDirectory() as d:
 p=Path(d)/'x';p.write_text('(kicad_pcb (net 1 "A") (module X (at 10 20 90) (fp_text reference U1) (pad 1 thru_hole circle (at 2 0) (net 1 "A"))) (module X (at 20 20) (fp_text reference U2) (pad 1 thru_hole circle (at 0 0) (net 1 "A"))))')
 e=extract(p);assert len(e['pads'])==2 and len(endpoints(e)['A'])==2
# CP268 snap / CP269 occupancy
assert snap((1.2,1.3),.5)==(2,3);assert occupancy([{'ref':'U','courtyard_corners_mm':[(0,0),(1,0),(1,1),(0,1)]}],1)
# CP270 deterministic A* / CP271 obstacle avoidance
r=astar((0,0),(2,0),{(1,0)},(-2,-2,4,4));assert r and (1,0) not in r
# CP272 no path explicit
assert astar((0,0),(2,0),{(1,-1),(1,0),(1,1)},(0,-1,2,1)) is None
# CP273 usage cost / CP274 tree route
r2=astar((0,0),(2,0),set(),(-2,-2,4,4),{(1,0):9});assert (1,0) not in r2
rr=route_net([(0,0),(2,0),(2,2)],set(),(-3,-3,5,5),{});assert len(rr)==2
# CP275 repeatability
assert route_net([(0,0),(2,0)],set(),(-3,-3,5,5),{})==route_net([(0,0),(2,0)],set(),(-3,-3,5,5),{})
# CP276 stable endpoint hash
assert stable({'b':2,'a':1})==stable({'a':1,'b':2})
# CP277-CP288 are real-specimen assertions performed by integration command: pad identity, net fanout, finite bounds, component exclusion, ordering, occupancy, congestion, failure provenance, digest repeat, mutation, state feedback, package integrity.
print('CP264-CP288 primitive regressions: 13/13 PASS; real-specimen gates pending integration')
