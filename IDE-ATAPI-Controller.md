# Virtual Computer IDE / ATAPI Controller

## Purpose

`IdeController.vb` models one legacy primary IDE channel with an ATA hard disk as device 0 (master) and an ATAPI CD-ROM as device 1 (slave). The guest sees the conventional primary task file at `1F0h-1F7h`, alternate status/device control at `3F6h`, and IRQ14 through slave-PIC input 6.

This note documents the behavior that is easy to get subtly wrong in an emulator, especially during DOS CD-ROM driver discovery.

## The channel is not the device

There is one cable/channel but two devices. Device/Head (`1F6h`), Device Control (`3F6h`) and the physical IRQ14 line are channel-wide. The task-file register image and command-transfer state are per-device.

That distinction matters immediately after software reset. Both devices simultaneously own different signatures:

| Register | ATA master | ATAPI slave |
| --- | ---: | ---: |
| Sector Count / `1F2h` | `01h` | `01h` |
| Sector Number / `1F3h` | `01h` | `01h` |
| Cylinder Low / `1F4h` | `00h` | `14h` |
| Cylinder High / `1F5h` | `00h` | `EBh` |

Selecting the slave must therefore reveal `01 01 14 EB` without manufacturing that signature at selection time and without overwriting the master's register image. The old implementation had one shared task file and generated a signature only for whichever device happened to be selected when SRST was released.

## ATAPI PACKET phases

The `A0h` PACKET command is a transport command. It does not itself describe the CD operation; the guest subsequently writes a 12-byte SCSI/MMC command descriptor block to the 16-bit data register.

The low two bits of `1F2h` become the ATAPI Interrupt Reason bits during packet execution:

| C/D | I/O | Value | Meaning |
| ---: | ---: | ---: | --- |
| 1 | 0 | `01h` | command packet out: host writes the CDB |
| 0 | 1 | `02h` | data in: device supplies response/data |
| 1 | 1 | `03h` | command/status complete |

The important invariant is that `02h` means an actual data-in phase and therefore accompanies DRQ. A successful no-data command such as TEST UNIT READY finishes with `03h` and DRQ clear. The previous implementation returned `02h` for every successful packet command even when the response length was zero, which could leave a polling DOS driver waiting for a nonexistent data phase.

## PIO byte-count limit

Before issuing PACKET, the host writes its maximum acceptable PIO phase size into `1F4h/1F5h`. Those registers are captured when `A0h` begins. During each subsequent data-in phase, the device rewrites `1F4h/1F5h` with the number of bytes available in that *one* DRQ phase.

A large response is therefore not exposed as one giant DRQ. `IdeController` stores the complete logical response internally, slices it into bounded PIO phases, and advances to the next phase only after the guest consumes the current data buffer. After the final phase it reports Interrupt Reason `03h` with DRQ clear.

A host byte-count limit of zero is treated as a 64-KiB allowance. Individual device phases are capped at 65534 bytes so the live phase count remains non-zero in the 16-bit byte-count registers, which is friendlier to conservative DOS polling code.

## Interrupts versus polling

Regular Status (`1F7h`) acknowledges the IDE IRQ. Alternate Status (`3F6h`) does not. Device Control bit 1 (`nIEN`) suppresses the physical interrupt output.

Crucially, device state transitions do not depend on the guest accepting or acknowledging IRQ14. DOS storage drivers often mask IDE interrupts and poll BSY/DRQ during initialization. An emulated command that advances only from an IRQ callback will deadlock such software.

## Software reset

Device Control bit 2 is SRST.

While asserted, both devices enter BSY and transient transfer state is discarded. When SRST is released, both per-device reset signatures are restored. The ATAPI device also records UNIT ATTENTION / `29h` (reset occurred) unless a media-change UNIT ATTENTION is already pending.

## Sense and media-change lifecycle

Mount/eject records UNIT ATTENTION / `28h`. The next ordinary packet command reports CHECK CONDITION. REQUEST SENSE returns the fixed-format sense data and clears the pending UNIT ATTENTION. This avoids the contradictory old behavior where TEST UNIT READY could report success while REQUEST SENSE still claimed an unreported media change.

No-media operations report NOT READY / `3Ah`.

## Implemented packet commands

The CD-ROM currently implements the core command set needed for DOS discovery and read-only ISO use:

- `00h` TEST UNIT READY
- `03h` REQUEST SENSE
- `12h` INQUIRY
- `1Ah` MODE SENSE(6), CD-ROM capabilities page
- `1Bh` START STOP UNIT
- `1Eh` PREVENT/ALLOW MEDIUM REMOVAL
- `25h` READ CAPACITY(10)
- `28h` READ(10)
- `2Bh` SEEK(10)
- `42h` READ SUB-CHANNEL, minimal stopped/current-position response
- `43h` READ TOC/PMA/ATIP, format 0 single-data-track TOC
- `5Ah` MODE SENSE(10), CD-ROM capabilities page
- `A8h` READ(12)

Unsupported CDBs return ILLEGAL REQUEST / `20h`. Unsupported fields/pages return ILLEGAL REQUEST / `24h`.

## Scope and deliberate omissions

The emulated ATAPI drive is a read-only data CD-ROM backed by an ISO-9660 image. It does not currently model audio playback, multisession discs, raw-sector reads, CD-R/CD-RW recording, tray timing, seek latency, DMA packet data, bus-master IDE, or mechanical spin-up timing.

IDENTIFY PACKET DEVICE therefore does not advertise DMA. If a guest nevertheless sets the PACKET DMA feature bit, the controller aborts the ATA command instead of silently performing the wrong transport.

## Debugging a DOS driver hang

For a concise trace, log only:

- writes to `1F6h` and `1F7h`
- reads of `1F2h`, `1F4h`, `1F5h`, `1F7h`, and `3F6h`
- words written/read at `1F0h` while DRQ is set

Collapse repeated status polls, for example:

```text
OUT 1F6 = B0
IN  1F4 = 14
IN  1F5 = EB
OUT 1F4 = 00
OUT 1F5 = 08
OUT 1F7 = A0
IN  1F7 = 48
IN  1F2 = 01
OUTSW 1F0 = <12-byte CDB>
IN  1F7 = 48
IN  1F2 = 02
IN  1F4/1F5 = <phase byte count>
INSW 1F0 = <phase>
IN  1F7 = 40
IN  1F2 = 03
```

The last state is the invariant to look for: DRQ clear plus Interrupt Reason `03h` means the packet really completed.
