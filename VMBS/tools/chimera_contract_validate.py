#!/usr/bin/env python3
import argparse,json,re
ap=argparse.ArgumentParser(); ap.add_argument("board_ir"); ap.add_argument("code_store"); ap.add_argument("out"); a=ap.parse_args()
ir=json.load(open(a.board_ir)); cs=json.load(open(a.code_store)); mods={m["module_id"]:m for m in cs.get("modules",[])}
def expand(xs):
 out=set()
 for x in xs:
  m=re.fullmatch(r"([^0-9]*)([0-9]+)\.\.\1?([0-9]+)",x)
  if not m:
   m=re.fullmatch(r"([A-Za-z_#]+)([0-9]+)\.\.([A-Za-z_#]+)?([0-9]+)",x)
   if m and (m.group(3) is None or m.group(1)==m.group(3)):
    out.update(m.group(1)+str(n) for n in range(int(m.group(2)),int(m.group(4))+1)); continue
   out.add(x); continue
  out.update(m.group(1)+str(n) for n in range(int(m.group(2)),int(m.group(3))+1))
 return out
used={}
for l in ir.get("interface_links",[]):
 for e in l.get("endpoints",[]):
  if e.get("kind")=="MODULE_PIN":
   used.setdefault(e["ref"],set()).add((e.get("interface"),e.get("signal")))
diag=[]
bindings={b["ref"]:b for b in ir.get("module_bindings",[])}
for ref,eps in sorted(used.items()):
 b=bindings.get(ref); rec=mods.get((b or {}).get("module_id")); comp=(rec or {}).get("composition")
 if not comp:
  diag.append({"severity":"error","code":"COMPOSITION_CONTRACT_MISSING","ref":ref,"module_id":(b or {}).get("module_id")}); continue
 claimed=set()
 for ep in comp.get("endpoints",[]):
  for sig in expand(ep.get("signals",[])): claimed.add((ep.get("interface"),sig))
 for interface,signal in sorted(eps,key=lambda x:(str(x[0]),str(x[1]))):
  if (interface,signal) not in claimed:
   diag.append({"severity":"error","code":"COMPOSITION_ENDPOINT_UNCLAIMED","ref":ref,"module_id":b["module_id"],"interface":interface,"signal":signal})
out={"format":"VMB-CHIMERA-CONTRACT-VALIDATION-1","used_module_instances":len(used),"diagnostics":diag,"ready":not diag}
open(a.out,"w").write(json.dumps(out,indent=2)+"\n")
print(json.dumps({"instances":len(used),"errors":len(diag),"ready":not diag},indent=2))
raise SystemExit(0 if not diag else 2)
