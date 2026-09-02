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

## 2026-09-02 VIDE-CDD IRQ14 finding and correction

- `BTCDROM.SYS` is a BusLogic SCSI driver and correctly reported no compatible device; it does not exercise this IDE/ATAPI controller.
- `VIDE-CDD.SYS` 2.14 detected the primary IDE/ATAPI CD-ROM, proving task-file discovery and PACKET command submission worked.
- Live dumps captured completed `MODE SENSE(10)` and `TEST UNIT READY` device states while slave IRQ6 / system IRQ14 remained asserted and in service on both cascaded PICs.
- A later `Not ready reading drive D:` dump showed mounted media and three consecutive packet commands receiving the same `UNIT ATTENTION 06/28/00`: one `TEST UNIT READY` followed by two `READ TOC` retries. The controller had incorrectly kept the unit-attention event pending until `REQUEST SENSE`. Unit attention is now consumed when first reported as CHECK CONDITION while its sense tuple remains available for REQUEST SENSE, allowing legacy drivers that retry the failed command directly to proceed.
- The next `DIR D:` hang showed clear unit attention, no active transfer, idle PICs, and the driver polling status port `1F7h` at `0255:1324`. Its polling routine masked `BSY|DSC` and waited for `10h`, while the controller returned only `DRDY` (`40h`). Ready/reset, DRQ, successful completion, and error task-file states now include the ATA `DSC` bit, producing the expected idle status `50h`.
- VIDE-CDD's decoded wait loop polls the IBM AT fixed-disk completion flag at BDA `40:8E`, bit 7.
- The system BIOS had left INT `76h` at the generic bare-`IRET` handler. An early IRQ14 was therefore acknowledged but never issued EOI, wedging both the slave ISR bit and master cascade ISR bit.
- `Firmware/atbios.asm` now installs an INT `76h` handler which sets `40:8E` bit 7 and sends EOI to the slave PIC followed by the master PIC. This is firmware behavior, not an IDE-controller shortcut.

Validation: the rebuilt system ROM is exactly 65,536 bytes, the Debug output contains the same ROM hash as the source firmware, and the .NET solution builds with zero warnings and zero errors. Guest validation still required: cold boot with VIDE-CDD, MSCDEX installation, ISO root `DIR`, and file reads.
