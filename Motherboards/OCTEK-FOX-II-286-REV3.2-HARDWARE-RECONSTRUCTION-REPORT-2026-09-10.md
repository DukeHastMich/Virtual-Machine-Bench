# Octek FOX II 286 Rev. 3.2 — Hardware Reconstruction Report

Date: 2026-09-10
Purpose: establish the best-documented motherboard target for the uploaded FOX II BIOS pair and enumerate the board's major logical/physical building blocks for future VMB worker-module implementation.

## 1. Best matching board for the uploaded BIOS

The uploaded `286-Fox2.zip` contains two 64 KiB files whose two 32 KiB halves are mirrored. Using one 32 KiB half from each file gives:

- EVEN lane CRC32: `54DC119F`; SHA-1: `4bc543beef0d2201fa20eac90a0a6ca38ebf0dbf`
- ODD lane CRC32: `E5DB7775`; SHA-1: `2bd0572b9f7c76eff51375b551586ca8484e2a74`

Those hashes exactly match MAME's `o286foxii` set, identified as **Octek FOX II — Headland HT12/A — BIOS string DH1X-6069-113090-K0 — HT-1X 286 BIOS**. The reconstructed linear ROM contains `HT-1X 286 BIOS` and the BIOS date `11/30/90`.

The photograph included with the archive is silkscreened **FOX II 286 REV 3.2** and is populated with an AMD `N80L286-16/S`, making the best physical target for this exact archive an **Octek FOX II 286 Rev. 3.2, 16 MHz configuration**.

The older Octek FOX-II manual covers the 12/16 MHz versions and is a closer generation match to Rev. 3.2 than the later Revision 4.0 / Rev. 3.4 board documentation. The later manual remains valuable for technical tables and layout evolution.

## 2. Core silicon / worker-module decomposition

### Headland HT12/A system controller

Photo marking: `Headland Technology HT12/A ED0004 ...`.

The HT12 is the dominant motherboard worker. Headland documentation describes it as a **160-pin flat-pack single-chip 286 AT controller** integrating or replacing the functions of:

- dual 8237-compatible DMA controllers;
- dual 8259-compatible interrupt controllers;
- 8254-compatible three-channel timer;
- DMA page-register function;
- AT bus controller / command generation;
- memory controller and mapper;
- DRAM control and parity support;
- system/video BIOS shadowing;
- split-memory / 384 KiB relocation;
- hardware EMS mapping;
- CPU/peripheral support;
- coprocessor interface;
- board address-buffer/data-transceiver/memory-driver glue that would otherwise require many discrete devices;
- hot reset / fast A20 support on the HT12 platform.

Important firmware-visible HT12 ports from Headland documentation include the standard AT ranges plus **configuration registers at 1EDh/1EFh** and **hot reset / fast A20 at port 92h**.

This chip should therefore become a dedicated `HeadlandHt12` VMB worker, not a set of per-board special cases.

### CPU

The archive photograph is populated with:

- **AMD N80L286-16/S**
- PLCC68 package
- 16 MHz system configuration

The FOX-II family was sold in 12 MHz and 16 MHz versions at this revision; later Rev. 3.4+ boards also supported 20 MHz.

### Numeric coprocessor

- Optional **80287**
- DIP40 socket visible at U12
- Older Octek manual specifies 80287-8 for a 12 MHz board and 80287-10 for a 16 MHz board.

### Keyboard controller

The board contains a dedicated AMI-labelled **keyboard BIOS/controller** device in a 40-pin DIP footprint. Public board databases describe the FOX II human-interface controller generically as an **8042-compatible keyboard controller**. The exact manufacturer/part hidden under the sticker in this photograph is not yet verified.

For VMB this should be a separate 8042 worker with board-specific firmware/provenance retained separately rather than folded into HT12.

### RTC / CMOS

The archive photograph clearly shows:

- **HM6818P** RTC/CMOS device
- 32.768 kHz watch crystal (`Y3`)
- battery circuitry and external battery connector

The FOX-II manual describes the RTC as a 146818-compatible device with 64 bytes of CMOS RAM and standard AT RTC/CMOS register layout. IRQ8 is the real-time-clock interrupt.

For VMB this should be a distinct `Hm6818/Mc146818-compatible` worker connected to HT12/NMI/IRQ8 through explicit board nets.

## 3. Clocking visible on the Rev. 3.2 photograph

The supplied photo gives unusually useful physical evidence:

- `Y1`: **14.31818 MHz** oscillator
- `Y2`: **32.000000 MHz** oscillator on the 16 MHz board
- `Y3`: **32.768 kHz** RTC crystal

The 32 MHz source is consistent with the installed 16 MHz 80286 configuration. Exact clock division/routing, including CPU, AT bus and 80287 clock selection, should be taken from HT12 documentation / board schematics rather than inferred beyond what the photograph proves.

The board supports Turbo/Normal switching by front-panel switch and keyboard (`Ctrl`+`Alt`+`+` / `-`). Later FOX II documentation also records selectable ISA/287 clock behavior. This must be represented as real clock-selection state in the board model.

## 4. Firmware hardware

The exact archive is a two-byte-lane system BIOS:

- two 32 KiB effective ROM lanes, physically represented in the archive as mirrored 64 KiB dumps;
- AMI-labelled `286 BIOS SETUP ODD` and `286 BIOS SETUP EVEN` EPROMs in the photograph;
- JP1 selects **27256 vs 27128** ROM devices according to the manual;
- the exact dump therefore corresponds naturally to a **27256 pair** (32 KiB per byte lane) producing a 64 KiB CPU-linear BIOS window.

Firmware identity:

- `HT-1X 286 BIOS`
- MAME BIOS ID: `DH1X-6069-113090-K0`
- date: `11/30/90`
- AMI 286 BIOS/diagnostics lineage

ROM sockets, lane wiring, chip-enable/address-decode and reset alias should be modeled explicitly so the board definition can later feed both emulation and PCB/netlist generation.

## 5. Memory subsystem

FOX-II provides two logical banks, each of which can be populated by DIP DRAM or a pair of 30-pin SIMMs.

Documented DIP organization per bank:

- 4 × `44256` data DRAMs
- 2 × `41256` parity DRAMs

Bank 0 locations: U22, U23, U27, U28 + parity U18/U19.
Bank 1 locations: U24, U25, U29, U30 + parity U20/U21.

For the 640 KiB special case, Bank 1 may use 4464/4164 parts. Parity positions may be omitted if parity checking is disabled.

Capacity:

- up to 1 MiB using DIP DRAM;
- up to 4 MiB using 256 KiB / 1 MiB 30-pin SIMMs;
- DIP and SIMM memory can be mixed, but the DIP and SIMM implementation of the same logical bank cannot both be populated simultaneously.

The chipset supports:

- 0/1 wait-state DRAM selection;
- system BIOS shadow;
- video BIOS shadow;
- 16 KiB shadow granularity in FOX-II documentation;
- hardware EMS with four registers;
- 384 KiB relocation/split memory above 1 MiB.

This is one of the most important HT12 worker areas because the BIOS actively configures these facilities.

## 6. AT-compatible integrated platform functions

FOX-II exposes standard AT-compatible behavior through HT12 and discrete support devices:

- 16-level interrupt structure, IRQ0–IRQ15, with slave PIC cascade on IRQ2;
- seven usable DMA channels, with DMA channel 4 serving the cascade;
- 3 programmable timer channels;
- NMI for parity / I/O-channel check;
- 80287 IRQ13 path;
- standard fixed-disk IRQ14 assignment;
- floppy IRQ6 / DMA2;
- keyboard IRQ1;
- RTC IRQ8;
- standard ISA memory, I/O, DMA, interrupt, refresh and bus-control signals.

Headland's HT12 datasheet maps configuration registers at 1EDh/1EFh and port 92h to hot reset/A20 gate. The ordinary AT ranges remain firmware-visible and must be faithfully exposed by the HT12 worker.

## 7. ISA / expansion fabric

The exact slot count appears revision-dependent in surviving references:

- the older/later Octek manuals describe six slots with a mix of 16-bit and 8-bit ISA;
- The Retro Web currently lists five 16-bit plus two 8-bit slots for FOX II;
- the supplied Rev. 3.2 photograph should be treated as the physical authority for this specific board and its slot connector population when the board is traced into the VMB physical model.

Do not hard-code a generic HT12 slot topology into the chipset worker. ISA slot count, connector placement and exact nets are motherboard-level composition data.

## 8. Discrete glue visibly present

The Rev. 3.2 photo proves that the board is not literally “HT12 plus nothing.” Visible support logic includes at least:

- `MC74F04N` hex inverter (U1)
- `SN74LS244N` octal buffer/line driver (U2)
- `SN74LS245N` octal bus transceiver (U4)
- additional 74-series latches/buffers around U3/U5/U6 and the lower-left board area; several markings are not sufficiently legible in the current photograph to identify without risk of guessing
- resistor networks, transistor drivers, battery/charging components, decoupling network and connector conditioning

These should eventually become either explicit worker/glue primitives or netlist-level components where they affect behavior. Do not invent part identities for unreadable chips.

## 9. Physical-board evidence

The archive photograph is especially valuable because it provides a component-side physical reference for **FOX II 286 Rev. 3.2**. Visible major placement anchors include:

- HT12/A in the left-center quadrant;
- AMD PLCC68 80286 below it;
- paired BIOS EPROMs above/right of CPU;
- empty DIP40 80287 socket to the CPU's left;
- 8042/AMI keyboard-controller device below/right of CPU;
- HM6818P RTC along the bottom edge;
- 30-pin SIMM sockets in the lower-right quadrant;
- DIP DRAM field below the SIMMs;
- AT DIN keyboard connector at lower-right edge;
- AT P8/P9 power connector along bottom edge;
- front-panel/reset/speaker/turbo/keylock headers down the left edge;
- 14.31818 MHz and 32 MHz oscillators at upper-left; 32.768 kHz RTC crystal at right edge.

The Retro Web reports approximately 220 mm × 220 mm Baby-AT dimensions for the FOX II family. Exact Rev. 3.2 outline/hole coordinates should be measured from higher-resolution straight-on imagery or original mechanical documentation before fabrication data is claimed.

## 10. Best documents to collect next

Highest value, in order:

1. **Octek FOX-II 286 User's Guide — 12/16 MHz versions** — closest-generation board manual for Rev. 3.2.
2. **Octek FOX-II Revision 4.0 / 12/16/20 MHz User's Manual** — richer later reference; Appendix C depicts Rev. 3.4 and should not silently overwrite Rev. 3.2 differences.
3. **Headland Technologies HT12 / HT12+ datasheet, “Single 286 AT Chip”** — essential silicon contract; available from Bitsavers and indexed by The Retro Web.
4. **Generic HT12 286 motherboard manual / Quadtel HT12 material** — includes jumper/clock information, system-board block diagram, I/O maps and “chips technical information.”
5. **Hitachi HM6818/HM6818P datasheet**, plus MC146818A documentation for compatibility comparison.
6. **Exact 8042/keyboard-controller identification/dump** from an Octek FOX II if one can be sourced; the current sticker hides the underlying part.
7. High-resolution front/back photographs of a Rev. 3.2 board, preferably bare and orthographic, for net tracing and mounting/placement measurements.
8. Any actual Octek FOX II schematic, service manual, PAL/GAL equations, manufacturing drawing or trace photograph. These would dramatically reduce inference in the future PCB reconstruction.

## 11. Useful public sources located

- MAME `at.cpp`, `o286foxii` set: exact ROM lane hashes and board/chipset identification.
- The Retro Web: Octek FOX II 286 board page, BIOS IDs, chipset/RTC/8042 classification, manuals, HT12 driver and document links.
- Minus Zero Degrees: Octek FOX-II 12/16 MHz manual and 12/16/20 MHz manual.
- Device.report mirror: Octek FOX-II Revision 4.0 manual; generic HT12 286 manual.
- Bitsavers: `components/headland/Headland_HT12_1987.pdf`.
- VOGONS Wiki / VOGONSDrivers: Headland HT12 overview and generic motherboard manual.

## 12. VMB implementation boundary

Recommended worker split for this board:

- `HeadlandHt12` — chipset silicon and its exact register/state machine behavior
- `Processor286` — existing 286 worker
- `Intel80287` / compatible NPX — existing/selected coprocessor worker
- `KeyboardController8042` — discrete 8042 worker plus keyboard-controller ROM identity
- `Hm6818Rtc` — RTC/CMOS worker
- `Eprom27256` ×2 — system BIOS low/high byte lanes
- DRAM/SIMM bank workers or memory-array primitives
- 74-series glue primitives only where behavior/electrical topology requires them
- `FoxII286Rev32Board` — board composition, nets, straps, oscillators, jumpers, connectors, slots, ROM lanes, physical placement/provenance

The board class should contain wiring and configuration, not duplicate HT12 internals. The same HT12 worker can then be reused by SIIG/FOX/other donor boards to separate true silicon behavior from board-specific layout and glue.

