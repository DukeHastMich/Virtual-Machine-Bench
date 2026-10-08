from geometry_kernel import *;from pose_domains import *;from collision_engine import *;from route_order import *;from congestion import *;from route_state import *;from placement_objective import *;from moves import *;from tabu import *;from optimizer import *;from timing_constraints import *;from interconnect import *;from logic_levels import *;from open_collector import *;from power_domains import *;from fanout import *;from feasibility import *;from provenance import *;from explain import *;from determinism import *;from solver_pipeline import *
p=[(0,0),(10,0),(10,10),(0,10)];assert inside((5,5),p);assert overlap([(1,1),(3,1),(3,3),(1,3)],[(2,2),(4,2),(4,4),(2,4)])
assert domain([(0,0),(.5,0),(.5,.5),(0,.5)],p,[1,2],[1,2]);assert collisions({"A":[(1,1),(3,1),(3,3),(1,3)],"B":[(2,2),(4,2),(4,4),(2,4)]})
assert order(["IRQ5","CLK","/D0"])[0]=="CLK";rs=Routes();rs.put("A",[(0,0),(1,0)]);rs.put("B",[(1,0),(2,0)]);assert rs.over();rs.rip("B");assert not rs.over()
assert hpwl([(0,0),(3,4)])==7;assert len(generate("U",(2,2,0)))==5;t=Tabu(2);t.add("a");t.add("b");t.add("c");assert not t.has("a")
assert best([(2,"b"),(1,"z")])[0]==1 and accept(2,1);assert slack(10,1,2,3)==4;assert rc_delay(10,.1,1e-12)>0
assert __import__("logic_levels").check(2.4,.4,2,.8)["pass"];assert __import__("open_collector").check(["U"],False)=="TITS_UP_MISSING_PULLUP";assert common([(4.5,5.5),(4.75,5.25)])==(4.75,5.25)
assert __import__("fanout").check(10,[1,2,3]);assert combine(["SOLVED_VERIFIED","TITS_UP_MISSING_EVIDENCE"])=="TITS_UP_MISSING_EVIDENCE"
assert fact(5,"d","authoritative")["value"]==5;assert violation("X",["U"],"bad")["code"]=="X";assert stable_hash({"b":2,"a":1})==stable_hash({"a":1,"b":2});assert next_stage("PLACE")=="ROUTE";print("CP214-CP238 integrated regressions: 21/21 PASS")
