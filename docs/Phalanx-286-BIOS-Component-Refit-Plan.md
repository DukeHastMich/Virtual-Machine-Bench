# Phalanx 286 BIOS and component refit plan

Date: 2026-09-09
Status: plan only — no firmware or VM hardware changes in this pass
Board: C&T CS8221 NEAT AT (`ct-cs8221-neat-at-286`)
Firmware: `Firmware/atbios.asm` (production Phalanx, 64 KiB at `F0000h`)
CPU contract: Intel 80286 only. Optional 80287. No 386/387 functions.

This document is the work plan for finishing missing PC/AT BIOS services and repairing CMOS Setup appearance **without taking down the production machine again**.

---

## 1. Why this plan exists

The previous BIOS edit was applied directly to production `Firmware/atbios.asm` and the generated `atbios.rom`. That ROM is what the running VM loads (`Form1.vb` reads `Firmware/atbios.rom` from the app output directory). The machine then:

1. showed one video flash (option-ROM / mode-3 init, or a processor RESET#),
2. died in a reset/shutdown loop.

Production was reverted. **Do not edit production firmware until a sandbox ROM has POSTed.**

Root causes of that outage, in order of certainty:

| Cause | What closed | Symptom |
|---|---|---|
| INT 15h AH=87h wrote CMOS `0Fh = 09h` then RESET# | Cold POST now takes the AT block-move return before the IVT exists | IRET through `0040:0067` (often `0:0` or leftover RAM) → protection/shutdown → platform RESET# → one blink, then death. **This CMOS byte is battery-backed and survives a source revert.** |
| INT 15h AH=86h became an unbounded `IN 61h` bit-4 spin | Previously an immediate `CF=1` unsupported return | Hang if refresh detect never toggles in that loop |
| INT 1Eh pointed at `floppy_dpt` (`DF 02 …`) | Previously `IRET` | INT 1Eh is a **data pointer** (diskette parameter table). Calling it executes an 80287 ESC |
| DMA master-clear on cold POST | Working ROM never programmed the 8237s during POST; “DMA … OK” was a message | Extra HOLD/HRQ and mask state before the memory-test RESET# |
| Setup text via INT 10h AH=0Eh after VGA init | Teletype owned by `stealthpro_vgabios.asm` | Not a POST killer. Cosmetic: VGA teletype stores attribute `07h` (white on black) over the blue/gray chrome |

Intel 80286 facts that make (1) lethal (K286 audited edition, citing 210498-005 / 210760-002):

- LMSW can **set** PE; it cannot clear PE. Only RESET returns the 286 to real-address mode.
- After LMSW, a JMP is required to drop prefetch.
- Shutdown is **not** automatically RESET. The AT platform (8042 pulse `FEh` on port `64h`, NEAT shutdown detector) is what turns shutdown/PM-exit into RESET#.
- Reset CS visible selector is `F000h`, hidden base `FF0000h`, IP `FFF0h` — the ROM reset vector at image `FFF0h`.

So every incomplete AH=87h attempt that writes `0Fh=09h` and pulses RESET# will, on the **next** power-on, skip ordinary POST and try to unwind a stack that was never built.

---

## 2. Non-negotiable working rules

### 2.1 Sandbox, not production

Create an isolated tree before any assemble:

```
Firmware/sandbox-286-bios-refit/
  atbios.asm          (copy of production)
  atbios.rom          (sandbox build only)
  atbios.lst
  NOTES.md            (brick log)
```

- Assemble with a **copy** of `Firmware/build-bios.ps1` pointed at the sandbox source/output, or invoke NASM with explicit `-o` paths so production `atbios.rom` is never replaced.
- Do not copy a sandbox ROM into `bin\*\Firmware\` or the live output directory until the brick’s POST checklist passes.
- Keep `Firmware/Backup 9-9-26/` and `Firmware/Backup - pre BIOS CMOS backplane refit 2026-09-09/` untouched as known-good references.
- If CMOS NVRAM on disk still holds `0Fh=09h` from the failed edit, clear it **in the sandbox machine’s NVRAM file** (or add the BIOS guard in §5.1) before judging POST. A reverted ASM file does not clear battery-backed CMOS.

### 2.2 Do not change VM hardware

Out of scope for this refit (observe only):

- `ProcessorCore.vb` / `Processors/80286/`
- `MotherboardDevices.vb` (8237, PIT, 8259, port `61h`, CMOS/RTC)
- `HardwareBus.vb`, NEAT bridge/memory
- `FloppyController.vb`, `IdeController.vb`, `AtKeyboard101.vb`
- `stealthpro_vgabios.asm` except as a **read-only** explanation of Setup chrome (optional later video-ROM brick, separate plan)
- 80287 model, A20, HOLD/HLDA, RESET# detector

BIOS must use the guest-visible ports and INT contracts those devices already implement.

### 2.3 286-only instruction and service set

- Assemble with `cpu 286` and the existing `J286_*` macros. No `0F 8x` near Jcc, no 32-bit operands, no 386 descriptor granularity/limit fields.
- No INT 15h `E801h`/`E820h`, no V86, no APM, no PCI BIOS, no 387 opcodes (`FSIN`, `FCOS`, `FPREM1`, `FUCOM`, …).
- 80287 remains `FNINIT`/`FNSTSW` probe plus equipment-word/CMOS bit 1, as today.

### 2.4 BASIC window

- `basic110.rom` stays `incbin` at image `6000h` (physical `F6000h–FDFFFh`).
- SHA-256 must remain `3033d1a54c99d7e2aa1fc7c8c2e51a56ae1b61bf4e70e8aa580f43c43e37a63e`.
- Executable BIOS must stay below `6000h`. `times 0x6000-($-$$)` failing the build is a hard stop, not a reason to move BASIC.
- INT 18h remains `jmp 0xF600:0000`. INT 19h still falls back to INT 18h.

---

## 3. Current production inventory (read-only)

### 3.1 POST sequence (working)

1. EBDA scratch stack, CMOS `0Fh` shutdown dispatch.
2. IVT fill (`IRET` default), then 08, 09, 10–1A, 70, 76, 77.
3. CMOS validate/default, BDA, 80287 probe, PIC/PIT.
4. Protected-mode extended-memory size (LMSW, GDT in RAM at `8000h`, shutdown `02h`, 8042 RESET#).
5. 8042/keyboard POST, four-position IDE signature scan, primary-master IDENTIFY for INT 13h drive `80h`.
6. Option-ROM scan `C000h–DFFFFh` (Diamond Stealth Pro hooks INT 10h).
7. Mode 03h, PIT2/port 61h beep, quiet “Press DELETE”, full POST, INT 19h, else INT 18h BASIC.

INT 1Eh and INT 41h are **not** installed (remain `IRET`). Floppy geometry is returned only via INT 13h AH=08 `ES:DI = floppy_dpt`.

### 3.2 Services present

| Vector | Coverage |
|---|---|
| INT 10h | Fallback CGA-ish 00–03, 05–0A, 0E, 0F, 12, 1A. After option-ROM scan the VGA BIOS owns this |
| INT 11h/12h | Equipment word / 639 KiB |
| INT 13h | 00–04, 08, 09, 0C, 0D, 10, 11, 14, 15. Floppy DMA bounce in EBDA. HDD = primary master only |
| INT 14h/17h | COM1/2, LPT1/2 polled IBM-style |
| INT 15h | 4Fh intercept, 85h SysReq, 88h extended KiB, 90h/91h busy/complete. All else `AH=86h`, `CF=1` |
| INT 16h | 00–03, 05, 09, 0A, 10–12 |
| INT 1Ah | 00–07 ticks/RTC/alarm |
| INT 18h/19h | BASIC / bootstrap |

INT 15h AH=87h is **not** implemented. The **return** path (shutdown `09h`) already exists and is what Windows 3.1 Standard Mode expects. That is why a botched 87h is so dangerous: the resume dispatcher is live.

### 3.3 CMOS Setup (cosmetic defect)

`setup_draw_chrome` paints blue (`1Fh`) and gray (`70h`) with INT 10h AH=06h, then every string uses `print` → INT 10h AH=0Eh.

System-BIOS `video_tty` already preserves the existing cell attribute. After POST, **that handler is not used**. `Firmware/stealthpro_vgabios.asm` `video_tty` does `mov ah, 7` / `stosw`, punching black cells through the chrome. Help-pane strings also pad with dozens of spaces, so teletype walks the gray work area.

Fix Setup in the **system ROM** by writing `B800` cells with the pane attribute. Do not change the VGA BIOS in this refit (IBM VGA teletype historically uses `07h` in text mode; changing it can alter DOS/BASIC).

### 3.4 Hardware the BIOS may touch (do not modify)

| Device | Ports / path | BIOS use today |
|---|---|---|
| 8042 | `60h`/`64h` | Keyboard POST, A20, CPU reset pulse `FEh` |
| CMOS/RTC | `70h`/`71h` | Shutdown byte, checksum `10h–20h`, memory size, setup flags `20h` |
| 8259A | `20h`/`A0h` | Master/slave init, IRQ0/1/2/6/8/14/15 |
| 8253/8254 | `40h–43h` | IRQ0, DRAM refresh cadence on ch1, POST beep ch2 |
| Port 61h | AT PPI | Speaker gate/data, refresh detect bit 4, OUT2 bit 5 |
| 8237A | `00h–0Fh`, `C0h–DEh` | Programmed per floppy transfer, not POST-initialized |
| FDC | `3F2h`/`3F5h` | INT 13h floppy |
| ATA | `1F0h`/`170h` | POST inventory + INT 13h `80h` |
| VGA option ROM | `C000:0003` | INT 10h after scan |

---

## 4. Intel / AT contracts this refit must obey

From the local 286 library (K286 audited edition + 210498-005 *80286 and 80287 Programmer’s Reference*, 210760-002 *80286 Hardware Reference*) and IBM PC/AT BIOS practice already encoded in this ROM:

1. **Leave protected mode only by RESET#.** INT 15h AH=87h must LMSW → copy → 8042 reset, then CMOS `0Fh=09h` unwind. Never LMSW to clear PE.
2. **Shutdown `09h` frame** at `SS:SP` in `0040:0067` is `DS ES DI SI BP saved-SP BX DX CX AX IP CS FLAGS`, then IRET. This ROM already pops that order.
3. **Shutdown `02h`** remains the POST memory-test continuation. Do not reuse it for 87h.
4. **INT 1Eh / 41h / 46h are far pointers to tables**, not code. IVT entries must not be CALLed. Until DOS needs them, leaving `IRET` is safer than pointing at a table whose first bytes are executable (`floppy_dpt` begins `DFh 02h`, an 80287 ESC).
5. **AH=86h as an INT 15h *error* code** (`CF=1`) is the IBM “unsupported” return. Implementing wait as function AH=86h must not steal that fail-fast path without a bounded implementation, and must not hang if port `61h` bit 4 is static.
6. **GDT descriptors are 80286 format**: limit, base-low, base-high, access, two reserved zeros. Do not write 386 limit/G bits.
7. **Reset vector** stays `EA xx xx 00 F0` at `FFFF0h`.

---

## 5. Refit bricks (strict order)

Each brick: edit **sandbox only** → assemble 64 KiB + BASIC hash → run POST on a copy of the machine with known-good media → only then consider promoting the ROM.

Promotion means replacing production `Firmware/atbios.asm` and rebuilding `atbios.rom` in one controlled step, with a dated backup first.

### Brick 0 — Containment and CMOS hygiene (no feature work)

- Sandbox tree as in §2.1.
- Confirm production `atbios.asm` matches the reverted working tree (`Backup 9-9-26` / current GitHub copy).
- Locate the guest CMOS NVRAM file for board `ct-cs8221-neat-at-286`. If index `0Fh` is `09h` (or other non-zero leftover from the failed 87h run), set it to `00h` **or** rely on Brick 1’s guard. A reverted ROM will still die if CMOS is poisoned.
- POST checklist: quiet “Press DELETE”, full POST roll-up, boot or BASIC. No extra RESET# after the memory-test pulse.

### Brick 1 — Stale shutdown-byte guard

Still in sandbox. Harden `shutdown_resume_check` **before** any AH=87h implementation.

For code `09h` (and analogously `05h`/`0Ah`/`0Bh`/`0Ch` if `0040:0067` is a far pointer):

- If `0040:0067` / `0040:0069` is `0:0`, treat as stale → write `0Fh=00h` → full POST.
- If SS ≥ `9000h` or SP < 26, treat as stale.
- Only then perform the historical unwind.

This matches the ROM’s existing policy for `01h`/`03h`/`06h–08h`: unknown or alien frames do not get interpreted. It does **not** change the Windows 3.1 path when HIMEM/DOSX actually built a frame.

**Exit criterion:** a machine whose CMOS `0Fh` is `09h` and whose `40:67` is zero completes ordinary POST instead of blinking.

### Brick 2 — CMOS Setup drawing (no new INT services)

Replace Setup *output only*. Do not change POST `print` (black 03h screen, INT 10h teletype is correct there).

- Direct `B800:0000` writes with pane attributes:
  - header/footer/help: `1Eh`/`1Fh` (yellow/white on blue)
  - work pane: `70h`/`7Eh` (black/yellow on gray)
  - active tab: reverse (`30h` black on cyan)
- Box drawing from CP437 (`C9h/CDh/BBh/BAh/C8h/BCh/CBh/CAh`).
- Help strings without space-padding; write at column 59.
- Date/time entry uses the same plotter, not AH=0Eh.
- Keep letter shortcuts and Left/Right pages.
- Hide the CRTC cursor while menus are idle (`AH=01`, `CX=2000h`); restore on exit via mode 03h.

**Exit criterion:** Delete during POST shows blue/gray chrome with text *in* those colors, not black holes. F10/ESC still save/discard. POST without Delete still boots.

### Brick 3 — INT 15h AH=C0h / C1h (read-only queries)

Add only after Bricks 0–2 POST for a full session.

- C0h: `ES:BX` → 8-byte AT config (model `FCh`, 2nd 8259, RTC, 4Fh, EBDA, 8042). Feature bit 3 (event wait) stays **clear** until Brick 5 exists.
- C1h: `ES = 9FC0h`.
- Option-ROM scan must still complete. This machine’s VGA BIOS does not call INT 15h; other ROMs might. If a ROM hangs on C0h success, revert this brick and leave `CF=1`.

**Exit criterion:** POST + VGA init unchanged; a debug caller of C0h/C1h gets the table/segment and `CF=0`.

### Brick 4 — INT 15h AH=87h (286 block move)

The important missing AT function for HIMEM / Windows 3.1 Standard Mode. Implement only after Brick 1.

- Dispatch **only** on `AH=87h`. Do not run this path from POST.
- Fill BIOS-owned GDT slots 08h/20h/28h in the caller’s 48-byte GDT; leave source/dest (10h/18h) as 286 descriptors.
- Enable A20 via existing `post_enable_a20`.
- Build the exact 09h stack frame this ROM already pops.
- Write CMOS `0Fh=09h` with NMI masked (`70h` index `8Fh`).
- LGDT, CLI, LMSW, far JMP selector `20h` to a tiny copy stub in **this** ROM CS (`F0000h` + offset). `REP MOVSW`, then 8042 `FEh`.
- CX=0: success without PM.
- Never execute the copy stub except after PE is set.

**Exit criterion:** POST still cold-boots. A DOS/HIMEM or small real-mode test of AH=87h copies a known pattern above 1 MiB and returns with `AH=0`, `CF=0`, A20 restored as this ROM’s 09h handler already does (`post_disable_a20`). Windows 3.1 Standard Mode is a later guest test, not this brick’s gate.

### Brick 5 — INT 15h AH=86h wait (optional, bounded)

Do **not** replace the unsupported return until a bounded implementation exists.

- Prefer RTC periodic (already 1024 Hz in CMOS `0Ah`) or port `61h` bit 4 **with an inner `CX` timeout per edge**, same pattern as `post_beep`.
- If the edge never arrives, return success or `CF=1` — never spin forever.
- Keep inbound AH≠86h unsupported as `AH=86h`/`CF=1`.

### Brick 6 — Diskette table and INT 13h AT extras (DOS FORMAT)

Install INT 1Eh **after** option-ROM scan, not in the IVT fill.

- Point INT 1Eh at `floppy_dpt` in `F000h` only once VGA has initialized against `IRET`.
- INT 13h AH=16/17/18: cheap, fail-closed on bad drive numbers.
- AH=05 format: BIOS may issue FDC `4Dh` only if the existing `FloppyController` already accepts it. **Do not add FDC commands in this refit.** If command `0Dh` still returns `80h` (invalid), keep AH=05 as `CF=1` / controller error rather than faking success.
- INT 41h FDPT in EBDA after IDENTIFY: optional, after INT 1Eh is proven. Still not a callable interrupt.

### Brick 7 — DMA POST init (last, optional)

Working POST never master-clears the 8237s. Only add IBM-style master clear + cascade unmask if a guest is shown to require it, and only after Bricks 0–4. Watch HOLD/HRQ: this board’s DMA is a real bus master.

---

## 6. Explicitly out of scope

- Second INT 13h hard disk (`81h`) and ATAPI boot (already documented as preserved limitations).
- INT 15h AH=89h (switch to PM and stay there) — easy to hang a 286; not required for DOS or Windows 3.1 Standard.
- INT 15h AH=C2h PS/2 mouse (this machine uses a serial mouse).
- Changing VGA BIOS teletype default attribute.
- 486/QDI board firmware (`Firmware\qdi-v4p895p3-smt-v5.rom`).
- Any edit to the 80286/80287/NEAT/ISA device models.

---

## 7. POST / Setup verification checklist

Use a dedicated sandbox run, not the daily working image until promotion.

Cold power-on:

- [ ] One memory-test RESET# only (shutdown `02h`), then `post_memory_resume`
- [ ] No further RESET# before INT 19h
- [ ] “Press DELETE to enter Setup”
- [ ] Keyboard POST line, memory line, 80287/video/IDE inventory
- [ ] Boot HDD or floppy, or BASIC at `F600:0000` if no media
- [ ] BASIC banner still appears (F6000h image intact)

Poisoned CMOS (Brick 1):

- [ ] `0Fh=09h` and `40:67=0` still POSTs

Setup (Brick 2):

- [ ] Blue/gray chrome; glyphs share those backgrounds
- [ ] Tabs, N/B/F/K/A/T/D, F10, ESC
- [ ] RTC commit survives reset

Services (later bricks):

- [ ] INT 15h AH=88h still returns CMOS `30h/31h`
- [ ] INT 15h unknown still `CF=1`, `AH=86h`
- [ ] AH=87h test copy (Brick 4) without breaking the next cold POST

---

## 8. Promotion

1. Dated copy of production `Firmware/atbios.asm` + `atbios.rom` into a new backup folder.
2. Copy sandbox source over production `atbios.asm` only.
3. Run `Firmware/build-bios.ps1` from the repo (validates 64 KiB, reset vector `EAh`, BASIC window).
4. Rebuild the VB app so `CopyToOutputDirectory` publishes the new ROM.
5. One POST on the real machine before any further bricks.

If POST fails after promotion, restore the dated backup ROM **and** inspect CMOS `0Fh`; do not debug by stacking more features.

---

## 9. Source map

| Item | Path |
|---|---|
| Production BIOS | `Firmware/atbios.asm` |
| Build | `Firmware/build-bios.ps1` |
| BASIC | `Firmware/basic110.rom` |
| VGA BIOS (read-only for chrome) | `Firmware/stealthpro_vgabios.asm` |
| Last known-good tree | `Firmware/Backup 9-9-26/` |
| Pre-CMOS-refit tree | `Firmware/Backup - pre BIOS CMOS backplane refit 2026-09-09/` |
| 80286/80287 PRM | `HardwareDocs/Intel_80286_80287_Programmers_Reference_Manual_1987.pdf` (210498-005) |
| Local 286 library | `F:\Local Library\286 specific\` |
| K286 audited notes | `F:\Local Library\286 specific\K286 Manual Reconstruction\Manual\K286-Internal-Design-Specification-Audited.md` |

---

## 10. Recommended first execution step (later session)

Do **Brick 0 + Brick 1** in a sandbox. Do not implement 87h, wait, DMA, or Setup chrome until a poisoned-CMOS POST is proven. That is the difference between a missing AT function and a dead production machine.
