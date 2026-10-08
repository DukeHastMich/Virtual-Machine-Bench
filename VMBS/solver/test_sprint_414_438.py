from exact_copper_topology import *
# CP414-438 exact-contact primitive gates: analytic geometry, no routing-grid coincidence.
P={'ref':'U1','pad':'1','net':'A','xy':(0,0),'size_mm':(2,2),'copper_layers':('F.Cu',),'pad_shape':'rect'}
S={'net':'A','start':(-3,0),'end':(3,0),'width_mm':.2,'layer':'F.Cu'}
SB={**S,'layer':'B.Cu'}; V={'net':'A','at':(0,0),'size_mm':.8}; VF={**V,'at':(4,4)}
Z={'net':'A','layer':'F.Cu','polygon_mm':[(-1,-1),(1,-1),(1,1),(-1,1)],'filled_polygons_mm':[[(-1,-1),(1,-1),(1,1),(-1,1)]]}
ZF={**Z,'polygon_mm':[(5,5),(6,5),(6,6),(5,6)],'filled_polygons_mm':[[(5,5),(6,5),(6,6),(5,6)]]}
assert abs(point_seg_d2((0,1),(0,0),(2,0))-1)<1e-9 #414
assert seg_intersect((0,0),(2,2),(0,2),(2,0)) #415
assert not seg_intersect((0,0),(1,0),(2,0),(3,0)) #416
assert point_in_poly((0,0),Z['polygon_mm']) #417
assert not point_in_poly((3,3),Z['polygon_mm']) #418
assert pad_seg_contact(P,S) #419
assert not pad_seg_contact(P,SB) #420
assert pad_via_contact(P,V) #421
assert not pad_via_contact(P,VF) #422
assert pad_zone_contact(P,Z) #423
assert not pad_zone_contact(P,ZF) #424
assert seg_seg_contact(S,{**S,'start':(0,-2),'end':(0,2)}) #425
assert not seg_seg_contact(S,{**S,'start':(0,-2),'end':(0,2),'layer':'B.Cu'}) #426
assert seg_via_contact(S,V) #427
assert not seg_via_contact(S,VF) #428
assert seg_zone_contact(S,Z) #429
assert not seg_zone_contact(S,{**Z,'layer':'B.Cu'}) #430
assert via_via_contact(V,{**V,'at':(.5,0)}) #431
assert not via_via_contact(V,VF) #432
assert via_zone_contact(V,Z) #433
assert not via_zone_contact(V,ZF) #434
assert zone_zone_contact(Z,{**Z,'polygon_mm':[(.5,.5),(2,.5),(2,2),(.5,2)],'filled_polygons_mm':[[(.5,.5),(2,.5),(2,2),(.5,2)]]}) #435
assert not zone_zone_contact(Z,{**Z,'layer':'B.Cu'}) #436
E={'segments':[S],'vias':[],'zones':[]};g=build_net_graph('A',E,[P]);assert g['historical_pad_connectivity']=='CONNECTED' and g['pads_touching_copper']==1 #437
g2=build_net_graph('A',E,[{**P,'xy':(10,10)}]);assert g2['historical_pad_connectivity']=='UNRESOLVED' and g2['evidence_gaps'][0]['code'].startswith('TITS_UP_') #438
print('CP414-CP438 exact-contact regressions: 25/25 PASS')
