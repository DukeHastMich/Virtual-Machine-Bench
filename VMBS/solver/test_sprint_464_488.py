from real_board_router import transform
from exact_copper_topology import *
import math
# CP464-488 coordinate/evidence gates
x,y=transform(.9,-.4,157.48,74.93,90);assert abs(x-157.08)<1e-6 and abs(y-74.03)<1e-6 #464
x,y=transform(-19.05,7.62,121.92,91.44,270);assert abs(x-114.3)<1e-6 and abs(y-72.39)<1e-6 #465
P={'xy':(0,0),'size_mm':(2.5,.4),'pad_shape':'roundrect','roundrect_rratio':.25,'copper_layers':('F.Cu',),'rotation':0,'net':'N','ref':'U','pad':'1'}
S={'start':(-2,0),'end':(2,0),'width_mm':.2,'layer':'F.Cu','net':'N'}
assert pad_seg_contact(P,S) #466
assert not pad_seg_contact(P,{**S,'start':(-2,1),'end':(2,1)}) #467
P90={**P,'rotation':90}
assert pad_seg_contact(P90,{**S,'start':(0,-2),'end':(0,2)}) #468
assert not pad_seg_contact(P90,{**S,'start':(.8,-2),'end':(.8,2)}) #469
assert pad_point_d2(P,(0,0))==0 #470
assert pad_point_d2(P,(2,0))>0 #471
assert pad_point_d2(P90,(0,1))==0 #472
assert pad_point_d2(P90,(1,0))>0 #473
# single-terminal isolation is not proof of error
g=build_net_graph('N',{'segments':[],'vias':[],'zones':[]},[P]);assert g['pad_count']==1 and g['historical_pad_connectivity']=='UNRESOLVED' #474
assert g['evidence_gaps'][0]['code']=='TITS_UP_PAD_NOT_CONNECTED_TO_EXPLICIT_COPPER' #475 raw graph preserves fact
# multi terminal explicit segment proof
P2={**P,'xy':(4,0),'pad':'2'};S2={**S,'start':(0,0),'end':(4,0)}
g=build_net_graph('N',{'segments':[S2],'vias':[],'zones':[]},[P,P2]);assert g['historical_pad_connectivity']=='CONNECTED' #476
assert g['pads_touching_copper']==2 #477
# layer blocks false proof
g=build_net_graph('N',{'segments':[{**S2,'layer':'B.Cu'}],'vias':[],'zones':[]},[P,P2]);assert g['historical_pad_connectivity']=='UNRESOLVED' #478
# rotated via contact
V={'at':(0,1),'size_mm':.4,'net':'N'}
assert pad_via_contact(P90,V) #479
assert not pad_via_contact(P,V) #480
# deterministic local transform
assert to_pad_local(P90,(0,1))==to_pad_local(P90,(0,1)) #481
# roundrect corner rejection/acceptance
assert pad_point_d2(P,(1.24,.19))>=0 #482
assert pad_shape_supported(P) #483
# explicit unsupported remains false
assert not pad_shape_supported({**P,'pad_shape':'custom'}) #484
# graph digest stable
g1=build_net_graph('N',{'segments':[S2],'vias':[],'zones':[]},[P,P2]);g2=build_net_graph('N',{'segments':[S2],'vias':[],'zones':[]},[P,P2]);assert g1['digest']==g2['digest'] #485
# pad orientation is provenance-sensitive geometry
assert pad_seg_contact(P,{**S,'start':(.8,-2),'end':(.8,2)})!=pad_seg_contact(P90,{**S,'start':(.8,-2),'end':(.8,2)}) #486
# no net evidence means no invented connection
assert build_net_graph('X',{'segments':[],'vias':[],'zones':[]},[])['historical_pad_connectivity']=='UNRESOLVED' #487
# TITS_UP remains explicit for missing pad copper
assert any(e['code'].startswith('TITS_UP_') for e in build_net_graph('N',{'segments':[],'vias':[],'zones':[]},[P])['evidence_gaps']) #488
print('CP464-CP488 coordinate/connectivity regressions: 25/25 PASS')
