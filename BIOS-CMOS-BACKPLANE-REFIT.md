# Virtual Machine Bench — BIOS/CMOS Backplane Refit

Date: 2026-09-09
Base: `Virtual-Machine-Bench-Hardware-Population-Refit-2026-09-09`
Firmware source: `Firmware/atbios.asm`

## Purpose

Finish the partially-landed Phalanx BIOS/CMOS upgrade against the removable hardware population model. Firmware must discover hardware through guest-visible interfaces; it must not query host configuration or treat a UI selection as if firmware had magically learned it.

## Hardware-population behavior

- CPU `None` is a real empty processor socket. No BIOS can execute in that state; the powered motherboard remains a valid board-level test configuration.
- An installed 80286 executes POST normally. The BIOS does not use a host-side CPU-population shortcut.
- The 80287 is optional. POST probes the processor-extension path with `FNINIT`/`FNSTSW` and synchronizes the standard AT equipment-word/CMOS NPX presence bit from the observed result.
- Video is not hardcoded as Diamond. Setup reports whether a C000h option ROM is actually present.
- IDE presence is discovered on both channels and both device positions from ATA/ATAPI reset signatures. Empty sockets are reported as Empty and do not receive IDENTIFY commands.
- Removable ISA audio/network population remains outside CMOS ownership. Their actual ISA decode is controlled by the backplane and they are not fabricated by firmware.

## POST/CMOS features restored or completed

- Real PIT channel 2 / port 61h POST success beep.
- CMOS Fast vs Thorough extended-memory POST policy.
- CMOS keyboard-error halt Yes/No policy.
- Existing boot Num Lock and floppy-first/HDD-first preferences retained.
- Setup tabs are functional pages (Main / Advanced / Boot / Exit) navigated with Left/Right; they no longer advertise dead decorative pages.
- First accepted Delete disables keyboard scanning during the POST-to-Setup handoff; Setup re-enables scanning and clears trigger/repeat residue after taking ownership.
- Four-position IDE inventory on POST and in Setup.
- Completed POST roll-up reports detected 80287/video-option-ROM population plus all four IDE positions before boot selection.
- 80287 detected/None reporting instead of hardcoded installed text.
- Video option-ROM present/absent reporting instead of hardcoded Diamond text.
- Existing CMOS checksum/defaulting, RTC/date/time editing, protected-mode memory sizing, 8042 keyboard POST, option-ROM scan, and Delete setup-entry windows retained.

## ROM BASIC fallback

`Firmware/basic110.rom` is the user-supplied 32 KiB IBM BASIC ROM:

- size: 32768 bytes
- SHA-256: `3033d1a54c99d7e2aa1fc7c8c2e51a56ae1b61bf4e70e8aa580f43c43e37a63e`

The Phalanx BIOS currently occupies less than the F6000h BASIC window. BASIC is therefore assembled directly into its historical physical window, F6000h-FDFFFh, rather than copied into RAM or enabled by a host-side magic jump. INT 18h transfers to `F600:0000`; INT 19h invokes INT 18h after ordinary boot attempts fail.

This intentionally follows the IBM-style semantics of ROM BASIC as the final no-bootable-media fallback. It also means an all-empty/no-media machine reaches BASIC instead of hanging at an error message.

## IDE limitation deliberately preserved

The BIOS INT 13h implementation currently exposes only primary-master ATA as BIOS drive 80h. Secondary/slave devices remain electrically real and are visible to IDE-aware DOS/Windows drivers and to POST/Setup, but this refit does **not** fabricate additional INT 13h drive numbers. ATAPI boot is likewise not invented.

## Source safety

An exact pre-edit backup is retained in:

`Firmware/Backup - pre BIOS CMOS backplane refit 2026-09-09/`

The CPU execution core was not modified by this BIOS pass.

## Build

The active generated `atbios.rom`/`atbios.lst` are intentionally omitted from the handoff so the old binary cannot masquerade as the new firmware. The exact pre-refit binary/listing remain in the backup directory.

Run from Windows:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Firmware\build-bios.ps1
```

The build script assembles with NASM, requires exactly 64 KiB output, checks the FFF0h far-jump reset vector, verifies the exact known BASIC ROM hash, and byte-compares the assembled F6000h-FDFFFh window against `basic110.rom` before replacing `atbios.rom`/`atbios.lst`.
