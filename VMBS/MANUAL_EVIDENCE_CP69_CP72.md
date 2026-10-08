# Manual evidence — CP69 through CP72

## CP69 — Intel 8255A PPI
Primary Intel evidence:
- Intel Peripheral Design Handbook (1979), 8255A/8255A-5 section: https://www.bitsavers.org/components/intel/_dataBooks/1979_Intel_Peripheral_Design_Handbook.pdf
- Intel iSBC 519 Programmable I/O Expansion Board Hardware Reference Manual: https://www.bitsavers.org/pdf/intel/iSBC/9800385B_iSBC_519_Programmable_IO_Expansion_Board_Hardware_Reference_Manual_Feb79.pdf

Evidence used: 3 x 8-bit ports, CPU A0/A1 register selection, Mode 0/1/2 organization, Group A vs Group B mode limits, mode-set control word, Port C BSR, Mode 1/2 OBF#/ACK#/STB#/IBF/INTR handshaking, and BSR-controlled INTE flip-flops. The worker is a digital functional/handshake projection; AC timing and electrical thresholds remain Store evidence.

## CP70 — Intel 8254 PIT
Primary Intel evidence:
- Intel iSBC 80/24A Hardware Reference Manual, 8254 programming/read-back discussion: https://bitsavers.org/pdf/intel/iSBC/148437-001_iSBC_80_24A_Hardware_Reference_Manual_Nov85.pdf

Evidence used: three independent 16-bit counters, modes 0-5, binary/BCD operation, count latch, read-back command, status byte including OUT/NULL/RW/mode/BCD, and gate/output semantics. Existing `Pit8253` is intentionally retained as the historical runtime class name while its 8254 read-back behavior is audited and bound as `VMB.AT.PIT8254`.

## CP71 — Intel 8259A PIC
Primary Intel evidence:
- Intel Peripheral Design Handbook (1979), 8259A section: https://www.bitsavers.org/components/intel/_dataBooks/1979_Intel_Peripheral_Design_Handbook.pdf

Evidence used: IRR/ISR/IMR, ICW initialization sequence, OCW operation, edge/level request handling, fixed/rotating priority, EOI/auto-EOI, poll, special mask, cascade programming, special fully nested mode, and spurious IRQ7 behavior. Existing worker is explicitly described as a PC/AT-visible transaction projection; buffered-mode/uPM electrical-bus details are not promoted to modeled pin behavior.

## CP72 — Intel 8237A DMA
Primary Intel evidence:
- Intel iAPX 86/88/186/188 User's Manual 210912-001, 8237A section: https://www.bitsavers.org/components/intel/8086/210912-001_iAPX_86_88_186_188_Users_Manual_1985.pdf

Cross-check evidence:
- AMD Am9517A/8237A documentation describes demand/single/block/cascade modes, autoinitialize, rotating priority, memory-to-memory, increment/decrement and software requests.

Existing AT DMA worker was audited for the PC/AT-visible register/transaction subset. It models dual-controller address/count/mask/mode/request behavior, 8/16-bit AT addressing, transfer direction, demand/single/block/cascade treatment, autoinitialize, decrement, terminal count, priority rotation and HRQ abstraction. It does **not** claim standalone 8237A memory-to-memory or exact pin-cycle generation.

## Authenticity rule
A board-runtime transaction projection may be sufficient for an executable PC/AT destination while still being insufficient as an exact pin-timing model. Component Store records now say which resolution is actually supported instead of silently treating those as equivalent.
