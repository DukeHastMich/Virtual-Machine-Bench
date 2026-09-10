# QDI V4P895P3/SMT V5.x — board model notes

This directory is the second motherboard profile/backplane target for Virtual Machine Bench.

Physical identity used by the profile:

- QDI V4P895P3/SMT V5.x / VL-ISA-PB486P3 family
- Socket 3 486-class CPU population
- OPTi 82C895 System/Power Management Controller
- OPTi 82C602 buffer / internal RTC companion
- IMI SC468 clock generator
- 1–64 MB documented board memory configurations
- 64/128/256/512 KB socketed L2 cache
- three VESA Local Bus connectors, six 16-bit ISA connectors and one 8-bit ISA connector
- AMI WinBIOS family firmware; exact board ROM is an external required artifact and is **not fabricated** by this source tree

Register model implemented in `Opti895Chipset486.vb`:

- 22h shared index latch
- 23h integrated IPC data path (index 01h, reset C0h)
- 24h main 82C895 data path
- main control indexes 20h–2Fh with 2Ch intentionally not decoded
- power management indexes E0h–EFh
- 82C602 F7h–FAh write-only companion state
- 82C602 F9h bit 5 as the upper 128-byte CMOS bank selector

The motherboard profile intentionally does not claim 80486 execution.  A real 80486-family processor core plus a non-NEAT local-memory/bus bridge are required before this board can be powered as the live machine.
