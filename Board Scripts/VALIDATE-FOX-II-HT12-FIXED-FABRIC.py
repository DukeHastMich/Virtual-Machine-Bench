#!/usr/bin/env python3
"""Static conformance checks for the FOX II / HT12 fixed-fabric checkpoint.

This is intentionally source-level.  It protects lineage and architecture in an
environment without the Windows VB.NET compiler; it is not a compile/runtime claim.
"""
from __future__ import annotations
import hashlib, re, sys
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]

def text(rel: str) -> str:
    return (ROOT / rel).read_text(encoding="utf-8-sig")

def sha(rel: str) -> str:
    return hashlib.sha256((ROOT / rel).read_bytes()).hexdigest()

def require(cond: bool, message: str) -> None:
    if not cond:
        raise AssertionError(message)

def main() -> None:
    # Green-lineage BIOS must stay byte-identical to the audited 9/9 state.
    require(sha("Firmware/atbios.asm") == "bc7b7d99d5c2c996316e64306aefe1efeff4236885ac536a0a85ebda8f8e5bc3",
            "protected atbios.asm hash changed")
    require(sha("Firmware/atbios.rom") == "e5b5db0b89245322fef39bbc69ea118bc72a0577e7666a70840a029030f21888",
            "protected atbios.rom hash changed")
    print("PASS protected 9/9 BIOS hashes")

    fox_rom = ROOT / "Firmware/octek-fox-ii-286-rev3-2.rom"
    require(fox_rom.exists() and fox_rom.stat().st_size == 65536, "matched FOX ROM missing/not 64 KiB")
    require(hashlib.sha256(fox_rom.read_bytes()).hexdigest() ==
            "759a5c7006f0bb100c6e822f7c60deed6b00343ed77e4c20c6618ecb97130624",
            "matched FOX ROM hash changed")
    data = fox_rom.read_bytes()
    require(data[0xFFF0:0xFFF5] == bytes.fromhex("EA 5B E0 00 F0"), "FOX reset vector changed")
    require(b"HT-1X 286 BIOS" in data and b"11/30/90" in data, "FOX identifying strings missing")
    print("PASS matched FOX OEM ROM identity/reset vector")

    ht12 = text("Hardware/Chipsets/Headland/HT12/HeadlandHt12.vb")
    mem = text("Hardware/Chipsets/Headland/HT12/Ht12MemoryFabric.vb")
    fox = text("Motherboards/Octek/FOX II 286 Rev 3.2/FoxII286Rev32Board.vb")
    catalog = text("Motherboards/MotherboardCatalog.vb")
    runtime = text("Motherboards/MotherboardRuntime.vb")
    rtc = text("Hardware/Support Chips/RTC/Hualon HM6818/Hm6818Rtc.vb")
    common_rtc = text("MotherboardDevices.vb")

    # HT12-specific decode must not regress to NEAT register/interface semantics.
    require("ConfigurationIndexPort As UInt16 = &H1EDUS" in ht12 and "ConfigurationDataPort As UInt16 = &H1EFUS" in ht12,
            "HT12 1EDh/1EFh configuration interface missing")
    require("_registersInBed(&H14) = &H9" in ht12, "HT12 REG14 sourced reset default 09h missing")
    require("HotResetMask As Byte = &H1" in ht12 and "GateA20Mask As Byte = &H2" in ht12,
            "HT12 port 92 hot-reset/A20 masks missing")
    require("HotResetMinimumDelayPicoseconds As Long = 6720000L" in ht12 and
            "HotResetOperationPicoseconds As Long = 13400000L" in ht12,
            "HT12 port 92 timing constants missing")
    require("lastOrdinaryBlockInBed + 1UI" in mem and "* &H10000UI" in mem,
            "HT12 REG18 ordinary-memory exclusive-top formula missing")
    require("ShadowGloballyEnabledInBed" in mem and "ReadRegister(&H12)" in mem and "ReadRegister(&H13)" in mem,
            "HT12 per-window shadow state machine missing")
    print("PASS HT12 config/port92/shadow/REG18 source invariants")

    # FOX's physical RTC is 64 bytes; generic NEAT remains 128 by default.
    require("MyBase.New(picInBed, persistencePathInBed, 64, Nothing)" in rtc,
            "HM6818 worker is not using 64-byte CMOS")
    require("Optional storageBytesInBed As Integer = 128" in common_rtc,
            "generic CmosRtc 128-byte default changed")
    require("storageBytesInBed <> 64" in common_rtc and "storageBytesInBed <> 128" in common_rtc and
            "storageBytesInBed <> 256" in common_rtc,
            "CmosRtc supported storage sizes incomplete")
    print("PASS board-specific 64-byte HM6818 with unchanged generic 128-byte default")

    # Board/catalog/runtime IDs and firmware path must align exactly.
    impl = re.search(r'RuntimeImplementationId As String = "([^"]+)"', fox)
    catimpl = re.search(r'OctekFoxII286Rev32BackplaneImplementationId As String = "([^"]+)"', catalog)
    require(impl and catimpl and impl.group(1) == catimpl.group(1), "FOX runtime implementation IDs diverge")
    require('Return "Firmware\\octek-fox-ii-286-rev3-2.rom"' in catalog,
            "FOX profile does not point to dedicated OEM firmware")
    require('New ProcessorPackageProfile(AmdN80L28616Id, "AMD N80L286-16/S", "80286", False)' in catalog,
            "FOX physical AMD CPU package is not explicitly execution-gated")
    require("Return False" in fox[fox.index("Property ProcessorExecutionAvailable"):fox.index("Property ProcessorArchitecture")],
            "FOX processor execution gate is not closed")
    require("If Not nextInBed.ProcessorExecutionAvailable Then" in runtime and
            "If Not candidateInBed.ProcessorExecutionAvailable Then" in runtime,
            "runtime manager can activate fixed fabric without processor execution")
    print("PASS FOX catalog/runtime/firmware activation gates")

    # New FOX/HT12 code may not borrow the NEAT machine behind the label.
    forbidden = ("NeatMemoryController286", "NeatMotherboardBridge286", "CpuLocalBus286", "NeatCs8221Chipset")
    new_scope = mem + "\n" + fox + "\n" + ht12
    found = [name for name in forbidden if name in new_scope]
    require(not found, "FOX/HT12 source depends on NEAT types: " + ", ".join(found))
    print("PASS no NEAT backend dependency in FOX/HT12 implementation")

    # Project must compile the new fabric exactly once and deploy exact firmware.
    project = ET.parse(ROOT / "VirtualComputer.Modern.vbproj").getroot()
    compile_items = [e.attrib.get("Include", "") for e in project.iter() if e.tag.endswith("Compile")]
    content_items = [e.attrib.get("Include", "") for e in project.iter() if e.tag.endswith("Content")]
    fabric_item = r"Hardware\Chipsets\Headland\HT12\Ht12MemoryFabric.vb"
    rom_item = r"Firmware\octek-fox-ii-286-rev3-2.rom"
    require(sum(x.lower() == fabric_item.lower() for x in compile_items) == 1,
            "HT12 memory/fabric source not compiled exactly once")
    require(sum(x.lower() == rom_item.lower() for x in content_items) == 1,
            "FOX OEM ROM not deployed exactly once")
    require(len(compile_items) == len({x.lower() for x in compile_items}), "duplicate Compile entry")
    print(f"PASS project inclusion ({len(compile_items)} unique Compile entries)")

    print("NOTE no dotnet/MSBuild/VBC toolchain present; this is not a compile/runtime claim")

if __name__ == "__main__":
    try:
        main()
    except Exception as exc:
        print("FAIL:", exc, file=sys.stderr)
        sys.exit(1)
