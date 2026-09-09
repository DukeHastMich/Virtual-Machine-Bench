# Intel 80287 implementation notes

Primary source preserved with the emulator:

- `HardwareDocs/Intel_80286_80287_Programmers_Reference_Manual_1987.pdf`
- Intel order number 210498-005, *80286 and 80287 Programmer's Reference Manual* (1987).

## Compatibility audit - 2026-09-01

The target is a physical Intel 80287 attached to an 80286. Later 80387 instructions must not execute successfully or influence guest feature detection.

### Corrected register opcode decoding

- D9 F5, D9 FB, D9 FE, and D9 FF are reserved on 80287. They no longer execute the 80387 `FPREM1`, `FSINCOS`, `FSIN`, or `FCOS` operations.
- DD E0-EF no longer execute 80387 `FUCOM`/`FUCOMP` forms.
- Reserved register and memory encodings now enter the 80287 invalid-operation exception path instead of silently doing nothing or executing a later-generation operation. The two Intel-documented 8087 compatibility encodings, FENI and FDISI, remain effective no-ops.
- Intel Appendix A legacy encodings are implemented: DC register `FCOM`/`FCOMP`, DD and DF `FXCH`, DE register `FCOMP`, DF `FFREE` plus pop, and the documented FSTP compatibility forms.

### Corrected operating-mode and saved-state behavior

- `FSETPM` now puts the NPX into protected mode and the state remains protected across `FINIT`/`FNINIT` and `FSAVE`, as Intel documents.
- Only the physical 80287 RESET path returns the NPX to real-address mode.
- `FLDENV`, `FSTENV`, `FRSTOR`, and `FSAVE` now distinguish the two 14-byte environment layouts:
  - protected mode stores instruction/data offset and selector pairs;
  - real-address mode stores 20-bit physical pointers and the 11-bit ESC opcode image.
- The stored instruction pointer begins at any prefix preceding the ESC opcode.

### Diagnostics added

- The CPU records every ESC attempt before applying the 80286 `EM`/`TS` trap rule.
- Separate total ESC-attempt and exception-7 counts survive processor-only shutdown resets used by protected-to-real mode transitions.
- `dumpall` reports the totals and the most recent ESC CS:IP, primary opcode, and MSW. A trapped ESC is recorded at the CPU boundary but is not falsely entered in the 80287 flight recorder because it never reached the NPX.

## Known approximation still present

- NPX register values are stored as host `System.Double`, not bit-exact 80-bit temporary-real values. This affects precision, denormals, NaN payloads, unsupported formats, and exact exception/rounding edge cases.
- Execution remains synchronous, so BUSY timing and overlap with the 80286 are not modeled.
- The motherboard ERROR/BUSY latch and IRQ13 electrical path remains incomplete; the architectural status and ERROR output are retained for diagnosis.

Do not replace any of these with host math shortcuts when guest-visible results, flags, tags, exceptions, or saved-state bytes would differ from an 80287.

## Projective infinity comparisons - 2026-09-08

`CompareSt0With` now honors control-word IC (bit 12). Intel 210498-005,
Table 1-12, printed page 1-28 (PDF page 378), specifies:

- IC=0: any signed infinity compared with either signed infinity is equal.
- IC=0: infinity compared with a finite value (including zero) is invalid;
  a masked exception reports unordered. An unmasked exception prevents pops.
- IC=1: affine comparison retains the signed ordering -infinity < finite < +infinity.

The supplied SCORCH.EXE's startup routine at image offset 032Ah executes
FNINIT, FLD +infinity (CS:024Bh), FCHS, FCOMP +infinity, FSTSW AX, SAHF.
Its JZ at 033Eh retains the 80287 classification (2); falling through increments
it to 80387 (3), stored at DS:83B0h. The math runtime checks this variable before
selecting FCOS (0561h), FSIN (0D78h), or FSINCOS (0ECAh). Ignoring IC caused
false 80387 detection. Honoring IC selects the game's older-coprocessor path.

Validation executes the extracted detection routine on the existing isolated
SingleStep CPU/memory bed: before the fix it stores 3, after the fix it stores 2.
`Tools/Test-80287Infinity.ps1 -AssemblyPath <built emulator DLL>` covers both
IC modes, all infinity signs and operand orders, finite/zero comparisons,
masked/unmasked exceptions, pop behavior, and the actual detection sequence.
It also verifies that FSINCOS/FSIN/FCOS remain rejected. All 504 checks pass.
The 80286 core, S3 card, and ROMs are unchanged. Full in-game verification of
the corrected hardware path remains a separate live check.
