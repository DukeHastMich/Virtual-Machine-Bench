# Virtual Machine Bench — Motherboard Runtime Boundary after CP06

## What the refit now owns

The configurator no longer treats “the motherboard” as a fixed decoration. A `MotherboardProfile` owns physical compatibility metadata and a matching `IMotherboardRuntimeBackplane` owns the live fixed-fabric implementation boundary.

A motherboard selection now controls:

- CPU socket and legal processor packages;
- legal numeric-coprocessor population;
- memory capacities/topology;
- L2-cache capacities;
- clock/jumper choices;
- fixed chipset identities and configuration ports;
- firmware image contract;
- CMOS/NVRAM capacity and persistence filename;
- expansion connector inventory and ISA width compatibility.

Per-board RAM/cache/clock state is stored independently under `Motherboard.<profile-id>` sections. Returning to a previously configured board restores that board's own physical context.

## Board 1 — current executable machine

`ct-cs8221-neat-at-286`

- C&T CS8221 NEAT-class AT board
- Harris CS80C286-25 execution core available
- Intel 80287 removable population supported
- proven NEAT memory/local-bus bridge remains authoritative
- current BIOS/CMOS and runtime behavior remain on the established paths

## Board 2 — QDI physical/silicon model

`qdi-v4p895p3-smt-v5`

- board profile and compatibility rules complete for this refit milestone;
- OPTi 82C895 + 82C602 indexed silicon model implemented;
- dedicated 256-byte CMOS contract and NVRAM path defined;
- board-specific firmware path defined as `Firmware\qdi-v4p895p3-smt-v5.rom`;
- QDI context has its own RAM/cache/clock settings;
- activation intentionally blocked rather than borrowing NEAT hardware.

## Why QDI power-on is still blocked

The remaining boundary is not a configurator problem. The active machine execution tree is genuinely 80286/NEAT-specific:

1. `Declares.MotherboardMemory` is `NeatMemoryController286`.
2. `Declares.MotherboardBridge` is `NeatMotherboardBridge286`.
3. `Declares.CpuBus` is `CpuLocalBus286`.
4. `CPU0` and `MachineRuntime286` are typed around `Processor286`.
5. DMA memory callbacks currently terminate in the NEAT bridge.
6. system-ROM loading currently targets the NEAT memory controller.
7. several reset/HOLD/READY/A20 paths are electrically specific to the 82C211/82C212 arrangement.

Replacing those with a fake generic shim would make the QDI selection cosmetically swappable while still running a NEAT motherboard underneath. CP06 explicitly refuses to do that.

## Exact next architecture seam for a future 486 core

Introduce a processor-family-neutral machine-fabric contract that owns:

- physical DRAM/ROM access;
- CPU local-bus cycles;
- DMA/master memory cycles;
- A20/reset/HOLD/READY-equivalent signals appropriate to that processor family;
- system-ROM mapping;
- power/reset lifecycle.

Then implement one adapter over the existing NEAT classes and one native OPTi/486 implementation. `MachineRuntime286` can remain the 286 execution runtime; a separate 486 runtime should be selected by the motherboard/runtime factory rather than widening `Processor286` until it becomes a fake universal CPU.

That future work is the correct point to make `QdiOpti895RuntimeBackplane.FixedFabricAvailable=True`.
