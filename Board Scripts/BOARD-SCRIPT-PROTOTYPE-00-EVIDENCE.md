# Board Script Prototype 00 — evidence ledger

Date: 2026-09-10

## Inputs

- Preserved FOX/HT12 construction checkpoint: `VMB-FOX-II-HT12-FULL-SOURCE-CHECKPOINT-00-WIP-2026-09-10.zip`.
- Chipset corpus part 1: `/Virtual Machine Bench/Chipset Library/chipset library 1 of 2.zip`.
- C&T source used for the second prototype: `CT-CS8221-NEAT/CS8221_NEAT_Data_Book_198710.pdf` inside corpus part 1.
- Existing VMB green NEAT behavior in `MotherboardDevices.vb` was used as the behavioral comparison target for the split prototype workers.  The prototype does not replace it.

## Why these two chipset shapes

### Headland HT12/A

The FOX work already has a reusable `HeadlandHt12` silicon worker.  It is a useful Board Script specimen because the physical motherboard contains one dominant integrated chipset package plus external CPU, RTC, BIOS, DRAM and glue.

The script therefore proves that a physical part ID can bind to an existing behavior worker and receive board-level power-on straps without placing HT12 semantics in the board description.

### Chips and Technologies CS8221 NEAT

The October 1987 data book explicitly describes CS8221 as a four-chip chipset:

- 82C211 CPU/Bus Controller;
- 82C212 Page/Interleave and EMS Memory Controller;
- 82C215 Data/Address Buffer;
- 82C206 Integrated Peripherals Controller.

Prototype 00 gives each physical package its own Board Script worker boundary.  This is intentionally parallel to the existing monolithic live VMB NEAT class until equivalence can be demonstrated.

## Sourced NEAT inter-chip pin subset used by the sample `.bs`

The first reference script only connects nets for which the 84-pin PLCC pin descriptions were directly recoverable from the data book:

| Signal | Source pin | Destination pin(s) |
|---|---|---|
| PROCCLK | 82C211 pin 2 | 82C212 pin 83 |
| RESET4 | 82C211 pin 38 | 82C212 pin 12 |
| HLDA1 | 82C211 pin 40 | 82C212 pin 19; 82C215 pin 53 |
| PAR ERR | 82C215 pin 83 | 82C211 pin 11 |
| ACEN | 82C211 pin 47 | 82C215 pin 14 |
| DLE | 82C212 pin 45 | 82C215 pin 76 |
| DRD | 82C212 pin 33 | 82C215 pin 74 |

The metadata deliberately marks `physical_pin_map_complete=false` because this is only a sourced subset, not a complete package model.

## Behavioral scope of split NEAT prototype

- 82C211: reset/register ownership for 60h, 61h and 62h; write masks retained from the live implementation. READY-timeout status and actual bus-state machinery are not migrated yet.
- 82C212: reset/register ownership for 64h–6Fh with current live write masks. DRAM/shadow/EMS decode behavior remains in the live implementation for now.
- 82C215: explicit physical worker boundary for address/data buffering and parity; no fake indexed-register surface is introduced.
- 82C206: indexed configuration byte 01h reset/write/read surface only. PIC/DMA/PIT/RTC functionality has not been split out of the live runtime by this prototype.

This is enough to test the Board Script component/worker geometry without claiming a completed NEAT refactor.

## Physical-truth rule

Coordinates in the two sample files are prototype board-space positions.  FOX coordinates are rough visual anchors only; the NEAT reference board is wholly synthetic.  Neither is claimed as historical copper or production placement.

`ManufacturingReady` must remain false until complete package pin maps, selected footprints, body dimensions/keepouts, board mechanics, power domains and required electrical rules are sourced.  Unknown parts and unknown pins are hard compile errors rather than guessed hardware.
