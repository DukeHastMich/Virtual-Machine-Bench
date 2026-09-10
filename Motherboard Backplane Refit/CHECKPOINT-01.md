# Virtual Machine Bench — Motherboard Backplane Refit Checkpoint 01

Date: 2026-09-09
Status: foundation source checkpoint; external VB.NET build/runtime validation required.

## Working base

This checkpoint is built from:

- `VMB-BIOS-CMOS-BACKPLANE-REFIT-CHECKPOINT-03-2026-09-09.zip`
- base SHA-256: `aee63fffc3bfe7d6bc86dd4f44da4c04f4ef65c1b3708fb8068a7d9dbcdd6b73`

The OPTi/QDI research input supplied for the second motherboard is:

- `Opti 895.zip`
- research-pack SHA-256: `875f9ce347d64d4d233c7a5b15d0b60c3ebc5c361d2447620fcea58784a03f2f`

Checkpoint 01 intentionally does **not** activate the QDI board yet.  It installs the motherboard/profile seam first so the proven 286 machine can be tested before a second physical board is admitted to the catalog.

## Completed in Checkpoint 01

1. Added `Motherboards/MotherboardCatalog.vb`.
   - Introduces `MotherboardProfile` as host-side physical-board metadata.
   - Introduces `MotherboardCatalog` as the source of known motherboard models.
   - Registers the existing C&T CS8221 NEAT-class AT board as the sole live profile.
   - Records the board's fixed chipset pieces, configuration interface, BIOS identity, supported CPU packages and supported numeric-coprocessor packages.
   - Separates profile metadata from future runtime/backplane construction through `BackplaneImplementationId`.

2. Added persistent motherboard population identity.
   - `MachineHardwarePopulationConfiguration.MotherboardId` now participates in clone/load/save/summary state.
   - Existing `VirtualComputer.machine.ini` files remain compatible; a missing motherboard key defaults to the current NEAT board and is normalized on save.

3. Made motherboard replacement a staged cold-chassis configuration item.
   - Motherboard identity participates in pending-population equality.
   - Added `StageMotherboardSelectionInBed`.
   - No motherboard is hot-swapped into a running machine.

4. Converted the motherboard selector from a decorative one-item display into a real catalog-backed selector.
   - System Overview and Motherboard pages use `GetMotherboardChoicesInBed()`.
   - The current checkpoint still shows exactly one board, by design.

5. Began context-appropriate socket population.
   - CPU choices now come from the selected motherboard profile.
   - Numeric-coprocessor choices now come from the selected motherboard profile.
   - With only the existing board in the catalog, the visible CPU/80287 choices remain unchanged.

6. Split staged selection from running-machine reporting.
   - `MotherboardHardwareId` follows the staged selector state.
   - Chipset/BIOS descriptive fields continue to report the actually asserted running motherboard until the cold power-cycle commits a future replacement.

7. Added the new source file to `VirtualComputer.Modern.vbproj`.

## Deliberately unchanged

- 80286 CPU core and timing.
- Intel 80287 implementation.
- NEAT chipset runtime objects and decode.
- ISA backplane electrical behavior.
- ISA card `None` semantics.
- Diamond Stealth Pro 928, SB16 and NE2000 hardware.
- IDE/ATAPI, floppy, keyboard, BIOS/CMOS refit source and BASIC fallback.
- Current machine default hardware population.

This is an architecture seam checkpoint, not a motherboard emulation change.

## Static validation performed here

- `VirtualComputer.Modern.vbproj` parses as XML.
- 56 Compile entries / 56 unique after adding the catalog source.
- Only the two pre-existing generated Visual Basic files are absent:
  - `My Project\Resources.Designer.vb`
  - `My Project\Settings.Designer.vb`
- Required motherboard persistence/catalog/UI symbols are present.
- The QDI/OPTi board is not live in the catalog yet.
- No `dotnet`, MSBuild, Mono or VB compiler is installed in this assistant container, so a real compile was not fabricated.

## External validation gate before Checkpoint 02

Build the project in Visual Studio/Codex, then verify:

1. The project compiles with no new errors.
2. System Configuration → Motherboard shows exactly:
   - `C&T CS8221 NEAT-class AT motherboard`
3. System Overview shows the same motherboard.
4. CPU choices remain:
   - None
   - Harris/Intersil CS80C286-25
5. Numeric coprocessor choices remain:
   - None
   - Intel 80287
6. Existing stored `VirtualComputer.machine.ini` without a Motherboard entry opens normally and gains the normalized motherboard identity when saved.
7. Power-on/POST and ordinary 286 operation remain behaviorally unchanged.

## Next checkpoint after green validation

Checkpoint 02 will add the **QDI V4P895P3/SMT V5.x / OPTi 82C895 + 82C602** board as a real catalog definition and add its board-specific CPU/socket, memory, clock and expansion-topology metadata.  Runtime power-on for that board remains gated until the motherboard fabric/backplane implementation is ready; the configurator will not silently instantiate 286 silicon behind a 486 board selection.

Exact source delta: `Motherboard Backplane Refit/CHECKPOINT-01.diff`.
