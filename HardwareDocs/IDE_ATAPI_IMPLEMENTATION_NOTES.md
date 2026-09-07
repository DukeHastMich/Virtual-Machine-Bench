# Dual-channel IDE / ATAPI implementation notes

## Physical model

- Primary ISA IDE channel: `1F0h-1F7h`, alternate status/device control at `3F6h`, IRQ14 / slave-PIC input 6.
- Secondary ISA IDE channel: `170h-177h`, alternate status/device control at `376h`, IRQ15 / slave-PIC input 7.
- Each channel is an independent `IdeController` instance with its own task files, media, reset state, device-control register, and IRQ wire.
- Each of the four electrical sockets is independently configurable as empty, an ATA hard disk, or an ATAPI CD-ROM. Master/slave position no longer implies device type; hardware changes are staged in System Configuration and applied on a power cycle.
- ATA and ATAPI devices retain independent task-file and transfer state. Selecting a device only changes which state is exposed.
- ATAPI is PIO-only. Packet DMA is neither advertised nor silently accepted.
- The existing BIOS fixed-disk boot contract remains primary-master only. Firmware installs the IBM-compatible IRQ14 / INT 76h completion handler for primary IDE and an IRQ15 / INT 77h cascaded-PIC EOI handler for secondary IDE. Secondary/slave storage is exposed through normal task-file ports for guest software/drivers; no claim is made that the current BIOS boots it.
- The FDC decode intentionally excludes `3F6h`; the previous broad `3F2h-3F7h` host decode could shadow the primary IDE alternate-status/device-control register.

## ATA hard-disk identity sidecars

A raw `.hdd`/`.img` contains platter sectors only. Its physical drive identity lives in the adjacent `<image>.meta` JSON sidecar handled by `HardDiskIdentity`. The sidecar already carries manufacturer, model, serial, firmware, native/BIOS geometry, ATA version/capabilities, spindle RPM, and average seek time. ATA IDENTIFY consumes this identity instead of fabricating a fixed model string. This is the intended seam for adding a catalog of real HDD models later without changing the raw disk format.

## Implemented packet behavior

- 12-byte PACKET command transport with host byte-count limits and multiple data-in DRQ phases.
- INQUIRY, TEST UNIT READY, REQUEST SENSE, READ CAPACITY(10), READ(10), READ(12), SEEK(10), READ TOC, READ SUB-CHANNEL, MODE SENSE(6/10), START STOP UNIT, and PREVENT/ALLOW MEDIUM REMOVAL.
- Media insertion/ejection reports unit attention through normal CHECK CONDITION / REQUEST SENSE behavior.
- Successful packet completion reports interrupt reason `03h` with DRQ clear.

## Diagnostic flight recorder

`IdeController.DiagnosticText()` is included in **Dump All**. Its bounded host-only recorder retains:

- media insertion/ejection;
- low-frequency ATA/channel events;
- ATAPI ATA commands and all 12 CDB bytes;
- READ LBA and sector count;
- each ATAPI data-in DRQ phase size and response offset;
- normal completion and CHECK CONDITION sense values;
- live slave task file, transfer kind/index, response offset, byte limit, media/unit-attention/removal state, and cumulative packet/read/phase counts.

The recorder observes existing state transitions. It does not perform guest bus cycles, add interrupts, or modify device timing.

**2026-09-05 performance note:** per-command and per-sector ATA hard-disk text entries are disabled by default. The cheap numeric ATA read/write phase counters remain active, while ATAPI and low-frequency channel events remain in the bounded text recorder. This avoids string formatting and queue churn on the FAT metadata hot path (`MD`, `RD`, directory updates) without changing ATA DRQ/IRQ phasing or disk behavior.

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

### Hard-disk identity sidecars

A raw `.hdd`/`.img` file is the platter contents only.  Its adjacent `<image>.meta` JSON file is the persistent physical-drive identity used by ATA IDENTIFY and by the host hardware UI.  It carries manufacturer, model, serial, firmware, native/BIOS geometry, ATA capability, spindle-speed, and seek-time data.  The Drive Shelf ignores `.meta` files as media entries but treats them as belonging to the image.  Sneaker Net inspection displays the identity, cloning writes the same identity beside the cloned image, and versioned backup snapshots the sidecar as `<generation>.<ext>.meta` so a model/identity change creates a new backup generation even when platter bytes are unchanged.

This is also the intended seam for real period HDD profiles: selecting/creating a specific virtual drive model should populate this sidecar; the IDE channel remains concerned only with ATA protocol and exposes the identity through normal guest-visible commands.

## 2026-09-03 — IDE topology and HDD model profiles

The System Configuration drawer now follows the electrical topology of the two-channel ATA controller rather than grouping sockets by the historical default device class:

- Channel 1 / Drive 0 (master)
- Channel 1 / Drive 1 (slave)
- Channel 2 / Drive 0 (master)
- Channel 2 / Drive 1 (slave)

Each socket independently selects Empty, ATA hard disk, or ATAPI CD-ROM. An ATA socket additionally selects a physical HDD model profile. The profile owns capacity, native CHS, BIOS CHS translation, ATA capability/timing identity, manufacturer/model/firmware strings, and spindle/seek identity. The raw `.hdd`/`.img` file remains platter data; its adjacent `.meta` file records the physical-drive identity presented by `IDENTIFY DEVICE`.

Initial HDD profiles include the exact legacy 64 MiB image shape, a Generic 500 MB drive, and period Conner CP-2064 / CP30064H profiles. The existing 64 MiB Cromwell-created image is deliberately migrated to the exact-size Generic 64 MiB profile rather than mislabeled as a Conner: the real Conner capacities use different sector counts. New drives created from the chassis utilities are sized from the selected model profile, so a real-model profile and its raw image cannot disagree about platter capacity.

Changing an installed HDD model is staged until power cycle. If an attached shelf image has the exact sector count required by the new profile, only its sidecar identity is rewritten and the drive is reattached. If capacity differs, the image is left untouched and is disconnected rather than silently resized/truncated.

The existing BIOS primary-master path already consumes ATA `IDENTIFY DEVICE`, so the selected sidecar identity is visible to POST/setup for Channel 1 Drive 0. Full BIOS enumeration of additional ATA disks and matching CMOS fixed-disk entries remains a separate firmware brick.
