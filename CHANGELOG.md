# Change log

## 2026-09-03
- Reorganized IDE hardware configuration by physical topology: Channel 1/2, each with Drive 0 (master) and Drive 1 (slave), instead of separate master/slave device-class pages.
- Added selectable ATA hard-disk model profiles with capacity, CHS, ATA identity, timing identity, and `.meta` sidecar persistence. Initial profiles include the exact legacy Generic 64 MiB image, Generic 500 MB, Conner CP-2064, and Conner CP30064H.
- Model changes are staged until power cycle; incompatible platter images are disconnected intact instead of being resized, while exact-capacity images can adopt the selected physical-drive sidecar identity. New shelf drives are created at the selected model's exact capacity.
- Routed IDE activity telemetry by electrical socket so HDD0-HDD3 correspond to primary master/slave and secondary master/slave ATA activity instead of one lamp per channel.

### Front panel / media controls

- Rebuilt the left drive rail as a compact physical bay stack: two floppy drives, the fitted ATAPI CD-ROM, two honest blank 5.25-inch reserve plates, and a period MFM/RLL-style facade for the fitted IDE primary-master hard disk.
- Wired the ATAPI facade to current media state, ISO mount/eject actions, and the Sneaker Net Optical/Disc Box workbench.
- Wired the hard-disk facade to the live IDE Drive Shelf, mount/create/boot/disconnect controls, the shelf folder, and the Sneaker Net Hard Drive utilities (inspect, clone, backup, attach/disconnect, and image creation).
- Blank reserve bays remain inert and do not imply guest-visible IDE devices that are not implemented.

## 2026-09-02

### Fixed

- Restored the form's embedded "demon dreaming" chassis artwork while machine power is off. The S3 presentation worker no longer replaces the intended power-off face with a synthetic black CRT frame.
- Began the `working-beta` boundary cleanup by removing the disabled WinForms CGA/text rendering timers and their unreachable `VrMem`/bitmap/font-cache subsystem. The active Diamond Stealth Pro/S3 presentation path is unchanged. Host diagnostic viewers were moved from the main form implementation into `Form1.Diagnostics.vb` as the first behavior-preserving responsibility split.
- Lowered the host-only Diamond Stealth Pro 928 CRT raster worker to below-normal scheduling priority. Under host saturation, cosmetic presentation now yields to the coordinated CPU/bus/device timeline; guest VRAM, CRTC, ISA and interrupt behavior are unchanged.
- Corrected ATAPI media-change handling so `UNIT ATTENTION 06/28/00` is consumed when first reported as CHECK CONDITION while the sense tuple remains available for `REQUEST SENSE`. Legacy DOS drivers that retry `READ TOC` directly no longer receive the media-change failure forever.
- Corrected ATA/ATAPI ready-state status from `40h` to `50h` by including the device-seek-complete bit. VIDE-CDD's `DIR D:` path explicitly waits for this hardware status before issuing its next packet.
- Removed continuous 48 kHz zero-sample synthesis while port `61h` disconnects the PC speaker. The guest-visible PIT channel 2, gate, data-enable, and motherboard timing paths are unchanged; only the final host transducer now takes a silent fast path.

### Diagnostics

- Added PC-speaker port `61h` data/gate/node state, generated and skipped PCM samples, queued and dropped host buffers, and `waveOut` error state to Dump All Diagnostics.
- Removed continuous construction of full architectural forensic strings every 1,024 instructions. Dump All retains the lightweight rolling CS:IP sampler, while detailed register/code/stack captures remain available on actual fault and reset events.
- Bypassed dormant DOS-return and QB-execution forensic method calls unless their bounded recorders are armed.

### Removed

- Removed the obsolete `BiosCalls.vb` high-level BIOS prototype. Normal execution already uses the assembled AT BIOS ROM, guest IVT, processor interrupt mechanism, and bus-connected hardware; dormant CPU fallbacks now require an explicitly supplied host firmware handler instead of silently reaching the prototype.
- Added the missing AT BIOS IRQ14 / INT 76h fixed-disk completion handler.
- The handler now records completion in BDA `40:8E` bit 7 and ends the cascaded interrupt at the slave and master 8259A controllers in hardware order.
- Prevents an early IDE/ATAPI interrupt from leaving IRQ14 permanently in service and forcing DOS CD-ROM drivers into multi-minute timeouts.
- Removed the Disc Box Optical page's silent Mount no-op. With no selected path, Mount now opens an ISO picker rooted in Disc Box; missing callbacks/files and mount failures are reported visibly.

### Diagnostics and documentation

- Identified the bundled OAKCDROM failure as execution of a 386 operand-size-prefix / PCI BIOS probe on the emulated 80286.
- Identified BTCDROM as a BusLogic SCSI driver, not an IDE/ATAPI driver.
- Confirmed VIDE-CDD 2.14 reaches the emulated ATAPI device and isolated its wait to the missing BIOS IRQ14 contract.
- Preserved the evidence and required guest retest in `HardwareDocs/IDE_ATAPI_IMPLEMENTATION_NOTES.md` and `logs/2026-09-02-VIDE-CDD-IRQ14.log`.
