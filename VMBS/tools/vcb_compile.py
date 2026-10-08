#!/usr/bin/env python3
"""Compile generated *.vcb.vb to a VCB plugin DLL using the installed .NET SDK."""
import argparse,subprocess,shutil,tempfile
from pathlib import Path
ap=argparse.ArgumentParser(); ap.add_argument('source'); ap.add_argument('output'); ap.add_argument('--vmb-root'); a=ap.parse_args()
src=Path(a.source).resolve(); out=Path(a.output).resolve(); root=Path(a.vmb_root).resolve() if a.vmb_root else Path(__file__).resolve().parents[2]
if not shutil.which('dotnet'): raise SystemExit('dotnet SDK not found on PATH; install/build prerequisites are required to manufacture VCB DLLs.')
# VCB ABI lives in the built VMB assembly. Prefer Release, then Debug.
refs=list(root.glob('bin/Release/net8.0-windows/*.dll'))+list(root.glob('bin/Debug/net8.0-windows/*.dll'))
vmb=next((p for p in refs if p.name.lower()=='virtualcomputer.modern.dll'),None)
if not vmb: raise SystemExit('Built VMB assembly not found. Build VirtualComputer.Modern.vbproj once, then rerun VMBS.')
out.parent.mkdir(parents=True,exist_ok=True)
with tempfile.TemporaryDirectory(prefix='vmbs_vcb_') as td:
 td=Path(td); local=td/src.name; local.write_bytes(src.read_bytes()); proj=td/'VCB.vbproj'
 proj.write_text(f'''<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net8.0-windows</TargetFramework><RootNamespace></RootNamespace><OptionStrict>On</OptionStrict><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><ItemGroup><Compile Include="{local.name}"/><Reference Include="VirtualComputer.Modern"><HintPath>{vmb}</HintPath><Private>false</Private></Reference></ItemGroup></Project>''',encoding='utf-8')
 subprocess.check_call(['dotnet','build',str(proj),'-c','Release','--nologo'],cwd=td)
 built=td/'bin/Release/net8.0-windows/VCB.dll'; shutil.copy2(built,out)
print(out)
