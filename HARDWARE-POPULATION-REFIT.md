# Hardware Population / Disconnect Refit — 2026-09-09

Baseline: user-supplied `9-9-20261AM.zip`.

## What changed

- CPU sources moved under `Processors/80286/`.
- Intel 80287 moved under `Math Coprocessors/Intel 80287/`.
- Diamond Stealth Pro 928 and NE2000 now compile only from their `Expansion Cards/...` locations; stale root duplicates were removed.
- Exact pre-refit CPU/timing/80287 source copies are retained in the local `Backup` folders and are excluded from compilation.
- Added persistent physical hardware population configuration for:
  - CPU socket: `None` or Harris CS80C286-25.
  - Numeric processor socket: `None` or Intel 80287.
  - Video ISA position: `None` or Diamond Stealth Pro 928.
  - Audio ISA position: `None` or Sound Blaster 16.
  - Network ISA position: `None` or NE2000.
- Population changes are staged and committed only through the chassis power-cycle path.
- CPU `None` is a genuinely empty processor socket: the machine runtime has no processor object to schedule while independently clocked motherboard/device time can continue.
- The 80287 is no longer permanently constructed inside `Processor286`; it is attached/detached at the processor-extension socket boundary.
- ISA `None` removes the device through the backplane/bus unregister path instead of leaving a hidden instantiated/decoded card.
- Video presentation, diagnostics, resource summaries, card menus, and jumper conflict checks were made population-aware.
- ISA conflict checking ignores resources of physically absent SB16/NE2000/80287 hardware.

## Preserved green-source backups

The following backup files were verified byte-for-byte against the corresponding files from the supplied baseline before packaging:

- `Processors/80286/Backup/ProcessorCore.green-9-9-2026-1AM.vb`
- `Processors/80286/Backup/ProcessorTiming.green-9-9-2026-1AM.vb`
- `Math Coprocessors/Intel 80287/Backup/Intel80287.green-9-9-2026-1AM.vb`

## Validation performed here

- `VirtualComputer.Modern.vbproj` parses as valid XML.
- 55 explicit `<Compile>` entries are unique.
- Every refit/moved source path referenced by the project exists.
- No stale root `ProcessorCore.vb`, `ProcessorTiming.vb`, `Intel80287.vb`, `DiamondStealthPro928.vb`, or `Ne2000Isa.vb` remains.
- CPU/NPX/ISA `None` paths and host diagnostic/presentation null guards were source-audited.

## Build note

This environment has no `dotnet`, `vbc`, or `msbuild`, so no VB compile/runtime test was possible here.

The supplied baseline itself contains two project compile references whose files are absent:

- `My Project/Resources.Designer.vb`
- `My Project/Settings.Designer.vb`

Those were already absent from the user's source archive and were deliberately not fabricated as part of this hardware refit.

## First local tests

1. Build the project from the ZIP and address only genuine compiler errors from this refit before touching execution logic.
2. Boot the default population and rerun the green real-mode single-step suite.
3. Test NPX = `None` and verify normal non-NPX DOS boot plus correct EM/TS behavior.
4. Test each ISA device = `None` independently and verify its I/O/IRQ/DMA/ROM presence disappears.
5. Test CPU = `None`: chassis/motherboard remains powered without CPU execution or host exceptions.
6. Restore all default hardware and verify the Diamond/Scorched Earth regression tests separately from this structural refit.
