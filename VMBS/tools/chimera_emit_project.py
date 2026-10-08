#!/usr/bin/env python3
import argparse,hashlib,json,shutil,subprocess,sys
from pathlib import Path
ap=argparse.ArgumentParser()
ap.add_argument("board_code"); ap.add_argument("manifest"); ap.add_argument("vmb_root"); ap.add_argument("out_dir")
ap.add_argument("--class-name",default="GeneratedVCB"); ap.add_argument("--board-id"); ap.add_argument("--display-name")
a=ap.parse_args()
bc=Path(a.board_code).resolve(); mf=json.load(open(a.manifest,encoding="utf-8"))
root=Path(a.vmb_root).resolve(); out=Path(a.out_dir).resolve()
if mf.get("format")!="VMB-CHIMERA-MANIFEST-1": raise SystemExit("unsupported chimera manifest")
if not mf.get("ready"): raise SystemExit("chimera manifest is not ready; no partial/fake board will be emitted")
out.mkdir(parents=True,exist_ok=True); srcdir=out/"modules"; srcdir.mkdir(exist_ok=True)
cmd=[sys.executable,str(Path(__file__).with_name("vb_board_emit.py")),str(bc),str(out/"GeneratedBoard.vb"),"--class-name",a.class_name]
if a.board_id: cmd += ["--board-id",a.board_id]
if a.display_name: cmd += ["--display-name",a.display_name]
subprocess.check_call(cmd)
compile_items=["GeneratedBoard.vb"]
for i,s in enumerate(mf.get("sources",[]),1):
    source=(root/s["source"]).resolve()
    if hashlib.sha256(source.read_bytes()).hexdigest()!=s["sha256"]: raise SystemExit("source changed after chimera planning: "+s["source"])
    name=f"M{i:04d}_{source.name}"
    shutil.copy2(source,srcdir/name); compile_items.append("modules/"+name)
proj='<Project Sdk="Microsoft.NET.Sdk">\n<PropertyGroup><TargetFramework>net8.0-windows</TargetFramework><RootNamespace></RootNamespace><OptionStrict>On</OptionStrict><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup>\n<ItemGroup>\n'
proj += "\n".join(f'  <Compile Include="{x}"/>' for x in compile_items)
proj += '\n<Reference Include="VirtualComputer.Modern"><HintPath>__VMB_REFERENCE__</HintPath><Private>false</Private></Reference>\n</ItemGroup>\n</Project>\n'
(out/"VCB.vbproj").write_text(proj,encoding="utf-8")
lock={"format":"VMB-CHIMERA-SOURCE-LOCK-1","board_code_sha256":hashlib.sha256(bc.read_bytes()).hexdigest(),"sources":mf["sources"],"instances":mf["instances"]}
(out/"chimera.lock.json").write_text(json.dumps(lock,indent=2)+"\n",encoding="utf-8")
print(json.dumps({"out_dir":str(out),"compile_items":len(compile_items),"module_sources":len(mf["sources"])},indent=2))
