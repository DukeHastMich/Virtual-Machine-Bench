# Primary IDE / ATAPI implementation notes

## Physical model

- Primary ISA IDE channel: `1F0h-1F7h`, alternate status/device control at `3F6h`.
- ATA hard disk is device 0 (master).
- ATAPI CD-ROM is device 1 (slave).
- The shared interrupt is IRQ14, wired to local input 6 of the slave 8259A.
- ATA and ATAPI devices retain independent task-file and transfer state. Selecting a device only changes which state is exposed.
- ATAPI is PIO-only. Packet DMA is neither advertised nor silently accepted.

## Implemented packet behavior

- 12-byte PACKET command transport with host byte-count limits and multiple data-in DRQ phases.
- INQUIRY, TEST UNIT READY, REQUEST SENSE, READ CAPACITY(10), READ(10), READ(12), SEEK(10), READ TOC, READ SUB-CHANNEL, MODE SENSE(6/10), START STOP UNIT, and PREVENT/ALLOW MEDIUM REMOVAL.
- Media insertion/ejection reports unit attention through normal CHECK CONDITION / REQUEST SENSE behavior.
- Successful packet completion reports interrupt reason `03h` with DRQ clear.

## Diagnostic flight recorder

`IdeController.DiagnosticText()` is included in **Dump All**. Its bounded host-only recorder now retains:

- media insertion/ejection;
- ATA command and sector DRQ boundaries;
- ATAPI ATA commands and all 12 CDB bytes;
- READ LBA and sector count;
- each ATAPI data-in DRQ phase size and response offset;
- normal completion and CHECK CONDITION sense values;
- live slave task file, transfer kind/index, response offset, byte limit, media/unit-attention/removal state, and cumulative packet/read/phase counts.

The recorder observes existing state transitions. It does not perform guest bus cycles, add interrupts, or modify device timing.

## 2026-09-01 boot-lock finding

The pre-instrumentation dump that appeared to stop during CD-ROM-driver loading did not capture an active ATAPI packet phase. The immediate lock was a repeated 80286 invalid-opcode fault on byte `66h` at `0B22:0DFF`; the guest exception handler returned to the same instruction. Earlier port `64h` activity was accumulated keyboard-controller polling and was not the final CPU loop. A new reproduction with the ATAPI recorder is required to determine whether CD data loaded the bad byte or execution reached it for another reason.
