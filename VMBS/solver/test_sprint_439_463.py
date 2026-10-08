from exact_copper_topology import *
from existing_copper import extract_existing_copper
# CP439-463 actual-zone-fill evidence gates.
Z={'net':'G','layer':'F.Cu','polygon_mm':[(0,0),(10,0),(10,10),(0,10)],'filled_polygons_mm':[[(1,1),(4,1),(4,4),(1,4)]],
   'fill_present':True,'thermal_gap_mm':.5,'thermal_bridge_width_mm':.5,'connect_pad_clearance_mm':.3}
P={'ref':'J','pad':'1','net':'G','xy':(2,2),'size_mm':(1,1),'copper_layers':('F.Cu',),'pad_shape':'rect'}
PO={**P,'xy':(8,8)}
S={'net':'G','start':(1,2),'end':(3,2),'width_mm':.2,'layer':'F.Cu'}
V={'net':'G','at':(2,2),'size_mm':.8}
assert zone_regions(Z)==Z['filled_polygons_mm'] #439
assert pad_zone_contact(P,Z) #440
assert not pad_zone_contact(PO,Z) #441 outline is NOT fill
assert seg_zone_contact(S,Z) #442
assert not seg_zone_contact({**S,'start':(7,8),'end':(9,8)},Z) #443
assert via_zone_contact(V,Z) #444
assert not via_zone_contact({**V,'at':(8,8)},Z) #445
Z2={**Z,'filled_polygons_mm':[[(3,3),(6,3),(6,6),(3,6)]]}
assert zone_zone_contact(Z,Z2) #446
assert not zone_zone_contact(Z,{**Z2,'layer':'B.Cu'}) #447
assert not pad_zone_contact(P,{**Z,'filled_polygons_mm':[]}) #448
g=build_net_graph('G',{'segments':[],'vias':[],'zones':[Z]},[P]);assert g['historical_pad_connectivity']=='CONNECTED' #449
g=build_net_graph('G',{'segments':[],'vias':[],'zones':[Z]},[PO]);assert g['historical_pad_connectivity']=='UNRESOLVED' #450
g=build_net_graph('G',{'segments':[],'vias':[],'zones':[{**Z,'filled_polygons_mm':[]}]},[P]);assert any(x['code']=='TITS_UP_ZONE_FILL_NOT_EXPLICIT' for x in g['evidence_gaps']) #451
assert Z['thermal_gap_mm']==.5 #452
assert Z['thermal_bridge_width_mm']==.5 #453
assert Z['connect_pad_clearance_mm']==.3 #454
# Filled polygon may contain detailed thermal bridge geometry: contact derives only from stored boundary.
thin={**Z,'filled_polygons_mm':[[(-2,-.1),(2,-.1),(2,.1),(-2,.1)]]}
assert pad_zone_contact({**P,'xy':(0,0)},thin) #455
assert not pad_zone_contact({**P,'xy':(0,2)},thin) #456
# Multiple disjoint fills remain separate zone node only because KiCad records them as one zone;
# do not use a zone node to bridge disjoint fill islands. This is surfaced for split expansion.
assert len(zone_regions({**Z,'filled_polygons_mm':[[(0,0),(1,0),(1,1),(0,1)],[(3,0),(4,0),(4,1),(3,1)]]}))==2 #457
# provenance determinism
assert stable({'fills':Z['filled_polygons_mm']})==stable({'fills':Z['filled_polygons_mm']}) #458
# exact boundary contact
assert circle_poly_contact((1,2),0,Z['filled_polygons_mm'][0]) #459
assert seg_poly_contact((0,1),(2,1),Z['filled_polygons_mm'][0],0) #460
# layer enforcement
assert not pad_zone_contact(P,{**Z,'layer':'B.Cu'}) #461
assert not seg_zone_contact(S,{**Z,'layer':'B.Cu'}) #462
# explicit fill beats permissive outline
assert point_in_poly((8,8),Z['polygon_mm']) and not any(point_in_poly((8,8),q) for q in zone_regions(Z)) #463
print('CP439-CP463 zone-fill evidence regressions: 25/25 PASS')
