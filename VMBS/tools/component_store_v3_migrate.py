#!/usr/bin/env python3
"""Deterministic VMBS Component Store v2 -> v3 migration.
Additive only: preserves every v2 record/family/variant and never invents missing facts.
"""
import argparse,copy,json,hashlib
from pathlib import Path

def domainize(x):
    y=copy.deepcopy(x)
    # Additive normalized domains. Legacy fields intentionally remain.
    y.setdefault("physical",{})
    if y.get("package") is not None:
        y["physical"].setdefault("package",y["package"])
    y["physical"].setdefault("footprints",[])
    y["physical"].setdefault("dimensions",{})
    y["physical"].setdefault("mounting",{})
    y.setdefault("pins",[])
    y.setdefault("interfaces",[])
    y.setdefault("electrical",{})
    y.setdefault("timing",{})
    y.setdefault("thermal",{})
    y.setdefault("validation_rules",[])
    y.setdefault("synthesis_rules",[])
    y.setdefault("sources",[])
    y.setdefault("support_requirements",[])
    y.setdefault("support_mesh",{
        "requirements":copy.deepcopy(y.get("support_requirements",[])),
        "provides":[],
        "alternatives":[],
        "patterns":[]
    })
    y.setdefault("implementation",{
        "vmb_binding":copy.deepcopy(y.get("vmb_binding")),
        "behavior_model":y.get("behavior_model"),
        "electrical_model":y.get("electrical_model")
    })
    y.setdefault("evidence",{
        "sources":copy.deepcopy(y.get("sources",[])),
        "confidence":None
    })
    return y

def migrate(d):
    if int(d.get("schema_version",2)) not in (2,3):
        raise ValueError("unsupported component-store schema")
    if int(d.get("schema_version",2))==3:
        return copy.deepcopy(d)
    out=copy.deepcopy(d)
    out["format"]="VMBS-COMPONENT-STORE-3"; out["schema_version"]=3
    out["records"]=[domainize(x) for x in out.get("records",[])]
    pol=out.get("variant_stub_policy",{})
    pol["format"]="VMBS-COMPONENT-VARIANT-STUBS-2"
    pol["families"]=[domainize(x) for x in pol.get("families",[])]
    out["migration"]={
      "from_schema":2,"method":"additive-domain-wrap",
      "fact_policy":"preserve-known-leave-unknown",
      "canonical_records":len(out["records"]),
      "family_records":len(pol.get("families",[])),
      "recognized_variants":sum(len(x.get("variants",[])) for x in pol.get("families",[]))
    }
    return out

if __name__=="__main__":
    ap=argparse.ArgumentParser(); ap.add_argument("src"); ap.add_argument("dst"); a=ap.parse_args()
    src=Path(a.src); dst=Path(a.dst); d=json.load(open(src,encoding="utf-8"))
    out=migrate(d); dst.write_text(json.dumps(out,indent=2)+"\n",encoding="utf-8")
    print(json.dumps({"output":str(dst),"records":len(out.get("records",[])),
      "families":len(out.get("variant_stub_policy",{}).get("families",[])),
      "variants":sum(len(x.get("variants",[])) for x in out.get("variant_stub_policy",{}).get("families",[])),
      "sha256":hashlib.sha256(dst.read_bytes()).hexdigest()},indent=2))
