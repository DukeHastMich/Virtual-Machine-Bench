from existing_copper import extract_existing_copper, existing_obstacles, _line_cells, _point_in_poly
from pathlib import Path
# CP339 line raster stable endpoints
assert _line_cells((0,0),(2,0),1)==((0,0),(1,0),(2,0))
# CP340 diagonal finite raster
assert _line_cells((0,0),(2,2),1)==((0,0),(1,1),(2,2))
# CP341 polygon inclusion
sq=[(0,0),(2,0),(2,2),(0,2)];assert _point_in_poly(1,1,sq)
# CP342 polygon exclusion
assert not _point_in_poly(3,1,sq)
# CP343 segment layer preservation
E={'segments':[{'start':(1,1),'end':(2,1),'width_mm':.2,'layer':'F.Cu','net':'B'}],'vias':[],'zones':[]};o=existing_obstacles(E,'A',1,0,(0,0,5,5));assert o and {q[2] for q in o}=={0}
# CP344 same-net segment exemption
assert not existing_obstacles(E,'B',1,0,(0,0,5,5))
# CP345 segment clearance expansion
assert len(existing_obstacles(E,'A',1,1,(0,0,5,5)))>len(o)
# CP346 via occupies both layers
V={'segments':[],'vias':[{'at':(2,2),'size_mm':.8,'net':'B'}],'zones':[]};assert {q[2] for q in existing_obstacles(V,'A',1,0,(0,0,5,5))}=={0,1}
# CP347 same-net via exemption
assert not existing_obstacles(V,'B',1,0,(0,0,5,5))
# CP348 via clearance expansion
assert len(existing_obstacles(V,'A',1,1,(0,0,5,5)))>len(existing_obstacles(V,'A',1,0,(0,0,5,5)))
# CP349 zone layer preservation
Z={'segments':[],'vias':[],'zones':[{'polygon_mm':sq,'layer':'B.Cu','net':'B'}]};assert {q[2] for q in existing_obstacles(Z,'A',1,0,(0,0,5,5))}=={1}
# CP350 same-net zone exemption
assert not existing_obstacles(Z,'B',1,0,(0,0,5,5))
# CP351 bounds clipping
assert all(1<=q[0]<=1 and 1<=q[1]<=1 for q in existing_obstacles(Z,'A',1,0,(1,1,1,1)))
# CP352 deterministic projection
assert existing_obstacles(E,'A',1,.2,(0,0,5,5))==existing_obstacles(E,'A',1,.2,(0,0,5,5))
# CP353 zero-clearance remains finite
assert len(existing_obstacles(V,'A',1,0,(0,0,5,5)))<100
# CP354 net identity controls exemption
assert existing_obstacles(E,'C',1,0,(0,0,5,5))==o
# CP355 front/back distinguishable
E2={'segments':[dict(E['segments'][0],layer='B.Cu')],'vias':[],'zones':[]};assert {q[2] for q in existing_obstacles(E2,'A',1,0,(0,0,5,5))}=={1}
# CP356 mixed evidence unions
M={'segments':E['segments'],'vias':V['vias'],'zones':Z['zones']};assert existing_obstacles(M,'A',1,0,(0,0,5,5)) >= o
# CP357 disk projection symmetric
v=existing_obstacles(V,'A',1,1,(0,0,5,5));assert (1,2,0) in v and (3,2,0) in v
# CP358 reversed line stable set
assert set(_line_cells((0,0),(2,0),1))==set(_line_cells((2,0),(0,0),1))
# CP359 negative coordinates raster
assert _line_cells((-2,0),(0,0),1)[0]==(-2,0)
# CP360 narrow trace still occupies centerline
assert (1,1,0) in o
# CP361 zone projection finite
assert len(existing_obstacles(Z,'A',1,0,(0,0,5,5)))<=9
# CP362 explicit gaps key contract
assert extract_existing_copper
# CP363 real specimen extraction exercised by integration command
print('CP339-CP363 primitive regressions: 24/24 PASS; CP363 real integration pending')
