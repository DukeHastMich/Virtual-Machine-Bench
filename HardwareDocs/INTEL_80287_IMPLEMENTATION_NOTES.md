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
