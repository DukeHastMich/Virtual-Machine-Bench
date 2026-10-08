from search_kernel import *
p=PlacementProblem(8,5,{"U1":[Pose(x,y) for x in range(7) for y in range(4)],"U2":[Pose(x,y) for x in range(7) for y in range(4)]},{"J1":(1,2),"U1":(2,2),"U2":(2,2)},{"J1":Pose(0,1)},[("J1","U1",3),("U1","U2",4)],[(3,0,4,2)])
r=solve_placement(p); assert r["result"]=="SOLVED_VERIFIED"
r2=solve_placement(p); assert r["placement"]==r2["placement"] and r["score"]==r2["score"]
bad=PlacementProblem(4,4,{"U":[]},{"U":(2,2)},{"U":Pose(3,3)},[],[]); assert solve_placement(bad)["result"]=="UNSAT"
wall={(2,y,0) for y in range(4)}; rr=astar_route(6,5,(0,2),(5,2),wall,layers=1); assert rr["result"]=="ROUTED" and all(n not in wall for n in rr["path"])
wall2={(2,y,0) for y in range(5)}; assert astar_route(6,5,(0,2),(5,2),wall2,layers=1)["result"]=="UNSAT"
print("CP211 regressions: 5/5 PASS"); print("placement score:",r["score"],"route cost:",rr["cost"])
