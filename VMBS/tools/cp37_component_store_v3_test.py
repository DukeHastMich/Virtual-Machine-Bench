#!/usr/bin/env python3
import json,subprocess,sys,tempfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]; DB=ROOT/"compiler_db"; TOOLS=ROOT/"tools"
v2=json.load(open(DB/"component_store_v2.cp36.json",encoding="utf-8"))
v3=json.load(open(DB/"component_store.json",encoding="utf-8"))
assert v3["schema_version"]==3 and v3["format"]=="VMBS-COMPONENT-STORE-3"
assert len(v2["records"])==23 and len(v3["records"])>=23
# CP37 proved the v2->v3 migration preserved the original 23 canonical records.
# Later Store walks may append documented canonical parts; they must not invalidate that historical proof.
for old,new in zip(v2["records"],v3["records"][:23]): assert old["part_id"]==new["part_id"]
f2=v2["variant_stub_policy"]["families"]; f3=v3["variant_stub_policy"]["families"]
assert len(f2)==len(f3)==2581
assert sum(len(x.get("variants",[])) for x in f2)==sum(len(x.get("variants",[])) for x in f3)==10867
for x in v3["records"]+f3:
    for k in ("physical","pins","interfaces","electrical","timing","thermal","support_mesh","implementation","evidence","validation_rules","synthesis_rules"): assert k in x
    assert set(("requirements","provides","alternatives","patterns")) <= set(x["support_mesh"])
subprocess.check_call([sys.executable,str(TOOLS/"component_store_v3_validate.py"),str(DB/"component_store.json")])
print("CP37 Component Store v3 migration PASS")
