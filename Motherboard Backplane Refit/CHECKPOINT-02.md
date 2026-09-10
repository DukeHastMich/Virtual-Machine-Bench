# Virtual Machine Bench — Motherboard Backplane Refit Checkpoint 02

Date: 2026-09-09
Status: second-board physical/configurator profile complete; QDI runtime intentionally gated until the backplane device factory exists.

## Added
- QDI V4P895P3/SMT V5.x / OPTi 895 Green PC as motherboard profile #2.
- General processor-package metadata distinct from execution-core availability.
- Manual-supported QDI CPU families: Intel 80486SX/DX/DX2/SL, P24C DX4, P24T, P24D, AMD Am486 DX/DX2/DX4, Cyrix Cx486 DX/DX2 (M7).
- QDI Socket 3 identity and no separate NPX socket choice (486DX-family FPU is integral; 487-style upgrades are CPU packages).
- QDI RAM choices: 1, 2, 4, 6, 8, 10, 12, 16, 20, 32, 64 MB.
- QDI L2 choices: 64, 128, 256, 512 KB.
- QDI JP38/JP39 base clocks: 25, 33, 40, 50 MHz.
- QDI expansion topology: 3 VLB, 6 16-bit ISA, 1 8-bit ISA.
- QDI fixed silicon/context: OPTi 82C895 + 82C602, IMI SC468, AMI WinBIOS family.
- Configurator motherboard page now shows socket, clock generator, RAM/cache topology, slots and runtime implementation status.
- Memory selector is motherboard-contextual instead of globally hard-coded to the 286 list.

## Safety boundary
The QDI profile is real and selectable, but Apply/Power Cycle refuses to commit it in this checkpoint because the executable still owns one hard-wired NEAT motherboard fabric. No NEAT silicon is silently presented behind the QDI label and no 286 CPU is substituted for a 486 package.

## Research basis
- QDI VL/ISA-PB486P3 / V4P895P3/SMT V5 manual supplied in `Opti 895.zip`.
- OPTi 82C895 System/Power Management Controller data book, September 1994.
- OPTi 82C601/82C602 Buffer Devices data book, November 1994.

## Next
Checkpoint 03 replaces the single hard-wired motherboard fabric assumption with a runtime motherboard/backplane factory while preserving the existing NEAT object graph byte-for-behavior.
