#!/usr/bin/env python3
"""Resolve arbitrary-N VMB construction modules into a static VB composition manifest.
Board-neutral by construction: only Board Code + Code Store metadata drive selection.
"""
import argparse,hashlib,json
from pathlib import Path

ap=argparse.ArgumentParser()
ap.add_argument("board_code"); ap.add_argument("code_store"); ap.add_argument("vmb_root"); ap.add_argument("out")
a=ap.parse_args()
bc=json.load(open(a.board_code,encoding="utf-8"))
cs=json.load(open(a.code_store,encoding="utf-8"))
root=Path(a.vmb_root).resolve()
known={m["module_id"]:m for m in cs.get("modules",[])}
instances=[]; sources={}; deps=set(); diagnostics=[]
for op in bc.get("instructions",[]):
    if op.get("op")!="ALLOC_MODULE": continue
    mid=op["module_id"]; rec=known.get(mid); impl=(rec or {}).get("implementation",{})
    item={"instance_id":op["id"],"part_id":op["part_id"],"module_id":mid,
          "implementation_status":impl.get("status","missing")}
    if rec is None:
        diagnostics.append({"severity":"error","code":"CHIMERA_MODULE_UNKNOWN","module_id":mid,"instance_id":op["id"]})
    elif impl.get("compile_allowed") is False or impl.get("status")=="stub":
        diagnostics.append({"severity":"error","code":"CHIMERA_MODULE_STUB","module_id":mid,"instance_id":op["id"]})
    elif impl.get("language")!="VB.NET" or not impl.get("source"):
        diagnostics.append({"severity":"error","code":"CHIMERA_SOURCE_REQUIRED","module_id":mid,"instance_id":op["id"],
                            "status":impl.get("status"),"language":impl.get("language")})
    else:
        rel=impl["source"].replace("\\","/")
        p=(root/rel).resolve()
        try: p.relative_to(root)
        except ValueError:
            diagnostics.append({"severity":"error","code":"CHIMERA_SOURCE_ESCAPES_ROOT","module_id":mid,"source":rel})
        else:
            if not p.is_file():
                diagnostics.append({"severity":"error","code":"CHIMERA_SOURCE_MISSING","module_id":mid,"source":rel})
            else:
                h=hashlib.sha256(p.read_bytes()).hexdigest()
                prior=sources.get(rel)
                if prior and prior["sha256"]!=h:
                    diagnostics.append({"severity":"error","code":"CHIMERA_SOURCE_HASH_CONFLICT","source":rel})
                sources[rel]={"source":rel,"sha256":h}
                item["source"]=rel
                item["entry_class"]=impl.get("class") or impl.get("entry_class")
                item["class_factory"]=impl.get("class_factory")
                for d in impl.get("dependencies",[]): deps.add(d)
    instances.append(item)

manifest={"format":"VMB-CHIMERA-MANIFEST-1",
          "source":{"board_code_sha256":hashlib.sha256(Path(a.board_code).read_bytes()).hexdigest()},
          "instances":instances,
          "sources":[sources[k] for k in sorted(sources,key=str.lower)],
          "dependencies":sorted(deps,key=str.lower),
          "diagnostics":diagnostics,
          "ready":not any(x["severity"]=="error" for x in diagnostics)}
Path(a.out).write_text(json.dumps(manifest,indent=2)+"\n",encoding="utf-8")
print(json.dumps({"instances":len(instances),"unique_sources":len(sources),"ready":manifest["ready"],
                  "errors":len(diagnostics)},indent=2))
raise SystemExit(0 if manifest["ready"] else 2)
