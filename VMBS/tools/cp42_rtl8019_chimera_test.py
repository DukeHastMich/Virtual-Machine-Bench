#!/usr/bin/env python3
import json,subprocess,tempfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]; VMB=ROOT.parent; T=ROOT/"tools"; DB=ROOT/"compiler_db"
m=json.load(open(DB/"ISA8_Ethernet.chimera.cp42.json")); assert m["ready"] and len(m["instances"])==3 and len(m["sources"])==3
c=json.load(open(DB/"ISA8_Ethernet.composition.cp42.json")); assert c["ready"] and not c["diagnostics"]
rtl=next(x for x in json.load(open(DB/"code_store.json"))["modules"] if x["module_id"]=="VMB.NET.RTL8019AS")
assert rtl["implementation"]["class"]=="Rtl8019As" and rtl["implementation"]["source"].endswith("Rtl8019As.vb")
src=(VMB/rtl["implementation"]["source"]).read_text()
for required in ("Class Rtl8019As","ReadRegister","WriteRegister","RemoteDmaReadByte","RemoteDmaWriteByte","Event InterruptChanged","Event TransmitRequested"): assert required in src
for forbidden in ("Class Ne2000Isa","Pic8259","UdpClient","IsaBackplane","basePort As UInt16"): assert forbidden not in src
stage=ROOT/"generated_vmb/cp42_chimera"; lock=json.load(open(stage/"chimera.lock.json"))
assert len(lock["sources"])==3 and len(list((stage/"modules").glob("*.vb")))==3
print("CP42 RTL8019AS IC-boundary chimera readiness PASS")
