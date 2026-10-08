from real_board_router import transform
from exact_copper_topology import *
# CP489-513 legacy pad-orientation + connectivity closure gates
P={'xy':(0,0),'size_mm':(2.5,.4),'pad_shape':'roundrect','roundrect_rratio':.25,'copper_layers':('F.Cu',),'rotation':90,'net':'P','ref':'U','pad':'1'}
Sv={'start':(0,.875),'end':(0,3),'width_mm':.3683,'layer':'F.Cu','net':'P'}
Sh={'start':(.875,0),'end':(3,0),'width_mm':.3683,'layer':'F.Cu','net':'P'}
assert pad_seg_contact(P,Sv) #489
assert not pad_seg_contact(P,Sh) #490
P0={**P,'rotation':0};assert pad_seg_contact(P0,Sh) #491
assert not pad_seg_contact(P0,Sv) #492
# U1-style 0.875 mm trace stop must contact 2.5x0.4 long-axis pad.
assert pad_seg_contact(P,{'start':(0,-.875),'end':(0,-3),'width_mm':.3683,'layer':'F.Cu','net':'P'}) #493
# layer still enforced
assert not pad_seg_contact(P,{**Sv,'layer':'B.Cu'}) #494
# orientation changes shape, not pad center
assert to_pad_local(P,(0,0))==(0.0,0.0) #495
assert pad_point_d2(P,(0,1.0))==0 #496
assert pad_point_d2(P,(1.0,0))>0 #497
# explicit trace-to-pad graph connectivity
P2={**P,'xy':(0,4),'pad':'2'}
S={'start':(0,.875),'end':(0,3.125),'width_mm':.3683,'layer':'F.Cu','net':'P'}
g=build_net_graph('P',{'segments':[S],'vias':[],'zones':[]},[P,P2])
assert g['historical_pad_connectivity']=='CONNECTED' #498
assert g['pads_touching_copper']==2 #499
assert g['component_count']==1 #500
# disconnected second pad remains unresolved
short={**Sv,'end':(0,1.5)}
g=build_net_graph('P',{'segments':[short],'vias':[],'zones':[]},[P,P2])
assert g['historical_pad_connectivity']=='UNRESOLVED' #501
# exact roundrect ratio remains required evidence
assert pad_shape_supported(P) #502
assert not pad_shape_supported({**P,'roundrect_rratio':None}) #503
# transform position convention remains inverse module rotation
x,y=transform(-9,-1,154.94,96.52,90);assert abs(x-153.94)<1e-9 and abs(y-105.52)<1e-9 #504
# pad orientation is deliberately independent field in contact geometry
assert P['rotation']==90 #505
# boundary trace widths matter deterministically
assert pad_seg_contact(P,{**Sv,'start':(.25,.875),'end':(.25,3)}) #506
assert not pad_seg_contact(P,{**Sv,'start':(.5,.875),'end':(.5,3)}) #507
# provenance digest stable
assert stable(g)==stable(g) #508
# no same-net copper => TITS_UP, never magical connection
q=build_net_graph('P',{'segments':[],'vias':[],'zones':[]},[P,P2])
assert q['historical_pad_connectivity']=='UNRESOLVED' #509
assert len(q['evidence_gaps'])==2 #510
# direct via contact still uses actual pad geometry
assert pad_via_contact(P,{'at':(0,1),'size_mm':.4,'net':'P'}) #511
assert not pad_via_contact(P,{'at':(1,0),'size_mm':.4,'net':'P'}) #512
# deterministic graph repeat
assert build_net_graph('P',{'segments':[S],'vias':[],'zones':[]},[P,P2])['digest']==build_net_graph('P',{'segments':[S],'vias':[],'zones':[]},[P,P2])['digest'] #513
print('CP489-CP513 legacy-pad/connectivity closure regressions: 25/25 PASS')
