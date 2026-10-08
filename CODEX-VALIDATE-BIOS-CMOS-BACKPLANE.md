# Codex handoff — validate/assemble/compile BIOS/CMOS backplane refit

Please validate the current source tree; do not replace it with an older Virtual Machine Bench archive.

## 1. Source identity and scope

Primary changed firmware/source artifacts:

- `Firmware/atbios.asm`
- `Firmware/basic110.rom`
- `Firmware/build-bios.ps1`
- `Firmware/build-bios.cmd`
- `BIOS-CMOS-BACKPLANE-REFIT.diff`
- `BIOS-CMOS-BACKPLANE-REFIT.md`

Pre-edit BIOS reference:

- `Firmware/Backup - pre BIOS CMOS backplane refit 2026-09-09/atbios.asm`
- corresponding `.lst` and `.rom`

Do not regress the hardware-population refit or CPU/80287 green source while fixing assembler/compiler findings.

## 2. Assemble BIOS first

The active `Firmware/atbios.rom` and `Firmware/atbios.lst` are intentionally absent in this handoff so a stale pre-refit ROM cannot be copied into the output by mistake. The exact old ROM/listing are preserved under the explicit pre-refit backup directory.

From repository root:

```powershell
.\Firmware\build-bios.ps1
```

Expected:

- NASM succeeds with `cpu 286`.
- `Firmware/atbios.rom` is exactly 65536 bytes.
- byte at ROM offset FFF0h is EAh.
- ROM offsets 6000h-DFFFh exactly equal `Firmware/basic110.rom`.
- BASIC ROM SHA-256 is `3033d1a54c99d7e2aa1fc7c8c2e51a56ae1b61bf4e70e8aa580f43c43e37a63e`.

If NASM objects to the 80287 probe mnemonics under `cpu 286`, preserve the exact intended **no-wait** instructions (`FNINIT`, `FNSTSW m16`) rather than changing the probe semantics. Do not convert absence into an artificial host-side flag.

Confirm the assembled Phalanx code/data remains below offset 6000h; a negative `times 0x6000-($-$$)` must be treated as a real ROM-layout failure, not worked around by moving BASIC arbitrarily.

## 3. Compile the VB.NET solution/project

Build the same project normally used for VMB. Resolve only genuine compile errors introduced by this refit. Do not resurrect root duplicate card sources or old ProcessorCore/Intel80287 locations.

## 4. Static architecture checks

Confirm:

- CPU, NPX, video, audio, network `None` choices remain physical-population states.
- BIOS has no host-side query of those selectors.
- IDE discovery probes command/control I/O only and does not issue IDENTIFY to Empty/ATAPI primary-master slots.
- POST beep programs PIT2 and port 61h through guest I/O.
- INT 18 vector points to the BIOS BASIC trampoline and ultimately jumps to F600:0000.
- INT 19 falls through to INT 18 only after normal boot attempts fail.
- CMOS setup flags are checksum-covered and include Num Lock, boot order, Fast/Thorough memory POST, keyboard-error halt.
- Main / Advanced / Boot / Exit Setup pages actually navigate with Left/Right and preserve staged CMOS state until F10.
- First Delete suppresses further scan traffic during the Setup handoff and scanning is explicitly re-enabled in Setup.
- detected 80287 state updates CMOS/equipment word rather than being hardcoded.

## 5. Runtime smoke matrix

At minimum test:

1. 80286 + 80287 + ordinary disk configuration: POST beep, POST display, Windows/DOS baseline boot.
2. 80286 + NPX=None: POST detects no 80287 and normal OS boot still works.
3. Four IDE positions Empty and no bootable floppy media: POST does not stall on storage discovery; INT 18 enters IBM BASIC.
4. Primary master ATA only: POST sees ATA and INT 13 drive 80h boots as before.
5. Primary slave and/or secondary ATA/ATAPI populated: POST/Setup inventory reports the correct physical slots; existing IDE-aware OS drivers still see them.
6. Video adapter=None: no fabricated C000h option ROM is reported.
7. Fast POST toggle and Thorough POST toggle both complete memory sizing correctly.
8. Keyboard error halt Yes/No policy behaves as selected.
9. Repeated Delete during POST enters Setup once without leaving buffered Delete keystrokes driving Setup.
10. Power cycle/reset removes transient runtime state and re-probes physical population.

## 6. Known deliberate boundary

INT 13h still exposes only primary-master ATA as BIOS drive 80h. Do not silently assign BIOS drive numbers to the other three IDE sockets as part of a compile fix. If extending BIOS disk numbering is desired, treat it as a separate reviewed feature.
