#!/usr/bin/env python3
import argparse,json,re
REQ=("physical","pins","interfaces","electrical","timing","thermal","support_mesh","implementation","evidence","validation_rules","synthesis_rules")
REL={"requires","provides","permits","recommends","observed_with","alternative"}
STRENGTH={"mandatory","recommended","reference","observed","inferred"}
KINDS={"capability","interface","signal","rail","clock","component","component_class","constraint"}
SCOPES={"part","package","family","interface","pin","board_observation"}
ap=argparse.ArgumentParser(); ap.add_argument("store"); a=ap.parse_args()
d=json.load(open(a.store,encoding="utf-8")); errors=[]

def err(where,msg): errors.append({"where":where,"error":msg})
def endpoint(v,where):
    if not isinstance(v,dict): return err(where,"endpoint must be object")
    if v.get("kind") not in KINDS: err(where+".kind","invalid endpoint kind")
    if not isinstance(v.get("name"),str) or not v["name"].strip(): err(where+".name","nonempty name required")

def edge(e,where):
    if not isinstance(e,dict): return err(where,"edge must be object")
    if e.get("relation") not in REL: err(where+".relation","invalid relation")
    if e.get("strength") not in STRENGTH: err(where+".strength","invalid strength")
    if e.get("scope","part") not in SCOPES: err(where+".scope","invalid scope")
    if not isinstance(e.get("id"),str) or not e["id"].strip(): err(where+".id","nonempty id required")
    endpoint(e.get("subject"),where+".subject")
    if "object" in e: endpoint(e["object"],where+".object")
    ev=e.get("evidence",[])
    if not isinstance(ev,list): err(where+".evidence","must be list")
    if e.get("strength") in {"mandatory","recommended","reference","observed"} and not ev:
        err(where+".evidence","evidence required for non-inferred support claim")
    if e.get("strength")=="inferred" and not (e.get("reason") or ev):
        err(where,"inferred edge requires reason or evidence")

def pattern(p,where):
    if not isinstance(p,dict): return err(where,"pattern must be object")
    for k in ("id","name","purpose"):
        if not isinstance(p.get(k),str) or not p[k].strip(): err(where+"."+k,"nonempty string required")
    if p.get("strength") not in STRENGTH: err(where+".strength","invalid strength")
    if p.get("strength") in {"mandatory","recommended","reference","observed"} and not p.get("evidence"):
        err(where+".evidence","evidence required for non-inferred pattern")
    parts=p.get("participants",[])
    if not isinstance(parts,list) or not parts: err(where+".participants","nonempty role list required")
    for i,x in enumerate(parts):
        q=f"{where}.participants[{i}]"
        if not isinstance(x,dict) or not isinstance(x.get("role"),str) or not x["role"].strip(): err(q,"role required")
        if isinstance(x,dict) and re.fullmatch(r"[URCJQDSW]\d+",str(x.get("role","")),re.I):
            err(q+".role","reference-designator roles are forbidden")
    if not isinstance(p.get("edges",[]),list): err(where+".edges","must be list")

def check(x,where):
    for k in REQ:
        if k not in x: err(where,"missing "+k)
    sm=x.get("support_mesh",{})
    for k in ("requirements","provides","alternatives","patterns"):
        if k not in sm: err(where,"missing support_mesh."+k)
    for bucket in ("requirements","provides","alternatives"):
        vals=sm.get(bucket,[])
        if not isinstance(vals,list): err(where+".support_mesh."+bucket,"must be list"); continue
        for i,e in enumerate(vals):
            # CP37 preserved legacy support_requirements as strings.  Keep them valid
            # during transition; every newly authored mesh edge must use the typed form.
            if bucket=="requirements" and isinstance(e,str):
                if not e.strip(): err(f"{where}.support_mesh.{bucket}[{i}]","legacy requirement string may not be empty")
                continue
            edge(e,f"{where}.support_mesh.{bucket}[{i}]")
    pats=sm.get("patterns",[])
    if not isinstance(pats,list): err(where+".support_mesh.patterns","must be list")
    else:
        for i,p in enumerate(pats): pattern(p,f"{where}.support_mesh.patterns[{i}]")

for i,x in enumerate(d.get("records",[])): check(x,f"records[{i}]")
families=d.get("variant_stub_policy",{}).get("families",[])
for i,x in enumerate(families): check(x,f"families[{i}]")
r={"valid":d.get("schema_version")==3 and not errors,"schema_version":d.get("schema_version"),
   "canonical_records":len(d.get("records",[])),"family_records":len(families),
   "recognized_variants":sum(len(x.get("variants",[])) for x in families),"errors":errors[:50]}
print(json.dumps(r,indent=2)); raise SystemExit(0 if r["valid"] else 1)
