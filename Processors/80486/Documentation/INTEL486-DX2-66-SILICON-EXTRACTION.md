# Intel486 DX2/66 silicon extraction for Virtual Machine Bench

Date: 2026-09-09
Target package: Intel486 DX2, 66 MHz core / 33 MHz external bus, 5 V, 168-pin PGA.
Target motherboard: QDI V4P895P3/SMT V5.x, OPTi 82C895 + 82C602, Socket 3.

This note extracts only behavior which must influence the VMB processor/package model or the board-to-processor electrical contract. It is not a general 486 tutorial.

## Primary Intel sources used

1. Intel486 DX2 Microprocessor Data Book, Intel order 241245-002, July 1992.
2. Intel486 Microprocessor Family Programmer's Reference Manual, Intel order 240486-002, April 1992.
3. Intel486 Microprocessors and Related Products, Intel order 241731-002, January 1995.
4. Embedded Intel486 Processor Family Developer's Manual, Intel order 273021-001, October 1997, used only where later family documentation clarifies common 486 mechanisms.

## 1. Package / clock / power identity

- The DX2 package is a 168-pin PGA and is pin-compatible with the Intel486 DX package.
- The 66 MHz part uses a 33 MHz 1x external clock. The core, integer unit, FPU and internal cache operate at twice that frequency.
- The July 1992 device is a 5 V part. Functional range is VCC = 5 V +/-5%.
- Warm RESET must remain asserted for at least 15 external clocks. Power-up RESET remains asserted for at least 1 ms after VCC and CLK are stable.
- The processor does not need a host delay loop to emulate 66 MHz. VMB must expose an external 33 MHz board clock and grant two internal core cycles per external clock where the processor is not blocked by bus/timing dependencies.

Source anchors: 241245-002 PDF pp.2, 20, 207-209.

## 2. Major silicon blocks that must remain distinct in code

The Intel block diagram and description imply these internal units should be represented as separate state/logic domains even when they share one package:

- 32-bit integer execution unit / ALU / barrel shifter
- instruction decode/control pipeline
- segmentation unit and descriptor caches
- paging unit and TLB
- 8 KiB unified on-chip cache
- four 32-bit write buffers
- floating-point unit
- bus request / sequencing / transfer drivers
- debug registers and test-register surface
- interrupt/NMI/reset logic
- cache snoop/invalidation logic

This division is also a useful host-performance seam: pure calculation may be prepared on workers, but architectural and pin-visible retirement must be serialized by the board-owned time authority.

Source anchor: 241245-002 PDF p.2 block diagram.

## 3. Programmer-visible integer architecture

The DX2 carries the full Intel386-class 32-bit architecture plus 486 extensions:

- EAX, EBX, ECX, EDX, ESI, EDI, EBP, ESP
- 16-bit aliases AX..SP and byte aliases AL/AH etc. with normal partial-register semantics
- 32-bit EIP and EFLAGS
- CS, SS, DS, ES, FS, GS with hidden descriptor caches
- 16-bit and 32-bit operands and addresses selected by descriptor D-bit defaults and 66h/67h overrides
- 32-bit effective address form: Base + Index*Scale + Displacement, including SIB scales 1,2,4,8
- protected mode, real mode and virtual-8086 mode
- four privilege levels, 386-format TSS support and I/O-permission bitmap

486-specific application/system additions include BSWAP, XADD, CMPXCHG, INVD, WBINVD and INVLPG. The 486 also adds alignment checking through EFLAGS.AC + CR0.AM and exception 17.

Source anchors: 241245-002 PDF pp.21, 41-42, 181-182; 240486-002 compatibility chapter.

## 4. Control/system state

Required control registers/state:

- CR0: PE, MP, EM, TS, ET, NE, WP, AM, NW, CD, PG with 486 validity rules
- CR2: last page-fault linear address
- CR3: 4 KiB-aligned page-directory base plus PWT and PCD
- GDTR, IDTR: 32-bit linear base + 16-bit limit
- LDTR, TR and hidden system-segment state
- DR0-DR3 address breakpoints, DR6 status, DR7 control
- 486 test registers used for cache/TLB testability (TR3-TR7) if/when diagnostics require them

CR0 cache rule for the ordinary write-through DX2:
- CD=0,NW=0: normal fills/write-through/invalidation
- CD=1,NW=0: fills disabled; write-through/invalidation still active
- CD=1,NW=1: fills, write-through and invalidations disabled
- CD=0,NW=1: invalid and should #GP on load

Source anchors: 240486-002; 273021-001 section on CR0 cache modes.

## 5. Reset identity/state

Required reset-visible state includes:

- EFLAGS = 00000002h (documented defined bits)
- EIP = 0000FFF0h
- visible CS selector = F000h
- hidden CS base = FFFF0000h and limit = FFFFh until the first qualifying intersegment control transfer/reload semantics
- DS/SS/ES/FS/GS selectors start at zero; real-mode data segment hidden bases start at zero with FFFFh limits where defined
- IDTR base = 00000000h, limit = 03FFh
- DR6 = FFFF0FF0h, DR7 = 00000000h
- paging disabled
- caching disabled after reset (CR0.CD set)
- normal reset numerics path uses external/DOS-compatible reporting until software changes CR0.NE

The CPU identification/stepping value present after RESET must be package/stepping metadata rather than a board constant. Do not hard-code a generic 486 ID into BIOS-facing board logic.

Source anchors: 240486-002 initialization chapter; 241245-002 BIOS/software notes.

## 6. Segmentation and paging

### Segmentation

- Real mode segment base = selector << 4, limit = FFFFh after ordinary segment reloads.
- Protected-mode descriptors have 32-bit base, effective limit with byte/page granularity, D/B size, type, privilege and present state.
- Virtual-8086 segment caches use real-mode-style base/limit but execute at CPL3 semantics.

### Paging

- 4 KiB pages, two-level page directory/page table hierarchy.
- CR3 stores the physical page-directory base.
- CR2 records the linear address that caused the last page fault.
- PTE/PDE accessed/dirty/protection semantics must be architectural writes, not host metadata only.
- CR0.WP changes supervisor write protection behavior.
- PWT/PCD participate in external cacheability signaling.
- The TLB is 32 entries, four-way set associative, covering 128 KiB with 4 KiB pages.
- Reloading CR3 invalidates cached translations according to Intel rules; INVLPG invalidates the addressed page.

Source anchors: 241245-002 PDF pp.74 and paging chapter; 240486-002.

## 7. Internal cache

Target cache:

- 8 KiB unified instruction/data
- four-way set associative
- write-through
- 16-byte cache lines
- read miss can allocate; write miss does not allocate
- write hit updates the internal line and, in ordinary NW=0 operation, is also presented externally
- software control via CR0.CD/NW and INVD/WBINVD
- page-level cacheability via PCD/PWT
- hardware cacheability via KEN#
- hardware line invalidation/snoop interface via AHOLD/EADS# and FLUSH# as documented

A cache fill reads the complete 16-byte line and follows Intel's critical-word-first burst ordering. Example first dword at offset 4 produces offsets 4,0,C,8.

Source anchors: 241245-002 PDF pp.20, 113-119; 273021-001 cache chapter.

## 8. External bus and dynamic sizing

The processor-facing bus must expose actual cycle definition rather than direct byte-array calls:

Outputs/bi-directional groups to model include:
- A31:A2 plus BE3#:BE0#
- D31:D0 plus data parity DP3:DP0 / PCHK# where modeled
- M/IO#, D/C#, W/R#, ADS#
- LOCK#, PLOCK#
- BLAST#
- BREQ, HLDA
- PWT, PCD
- FERR#

Inputs/control include:
- CLK, RESET
- RDY#, BRDY#
- BS8#, BS16#
- KEN#
- HOLD, AHOLD, BOFF#
- EADS#, FLUSH#
- A20M#
- INTR, NMI
- IGNNE#

The external system may choose 8, 16 or 32-bit transfer width on a cycle-by-cycle basis through BS8#/BS16#. Narrow transfers produce additional cycles and byte-routing order; this is essential for ISA/VLB/device interaction.

A non-burst transfer is at least two external clocks. RDY# inserts arbitrary wait states. Burst transfers can return the first item in two clocks and subsequent items every external clock through BRDY#. BLAST# identifies the final transfer.

Source anchors: 241245-002 hardware-interface and bus-operation chapters, especially pp.105-122.

## 9. Bus locking / arbitration / restart

- XCHG with a memory operand is automatically locked.
- LOCK-prefixed legal read-modify-write operations assert LOCK# across the atomic transfer.
- descriptor/page-table updates and interrupt acknowledge use locked cycles where specified.
- PLOCK# marks pseudo-locked multi-cycle transfers where the bus must not be granted away between component cycles.
- HOLD/HLDA yields the bus to another master.
- BOFF# permits immediate bus backoff and later restart.
- AHOLD/EADS# permit address-only snoop/invalidation participation without simply treating DMA as a host memory write.

This means QDI DMA/VLB mastering must be expressed through the processor/local-bus arbitration contract rather than bypassing the processor cache model.

## 10. Interrupts / NMI

- INTR is level sensitive after synchronization and is accepted at instruction/REP iteration boundaries when IF permits.
- The processor responds with two locked interrupt-acknowledge bus cycles; the vector is captured from the interrupt controller during the second cycle.
- NMI is edge sensitive, vector 2, internally masked while its handler is active and re-enabled by IRET semantics.
- Interrupt timing is expressed in emulated clocks, never host-thread timing.

Source anchors: 241245-002 pp.6-5 and bus interrupt-ack chapter; family hardware manuals.

## 11. Integrated floating-point unit

For DX2/66 the FPU is not an optional daughter chip. It is part of the CPU package and operates at the doubled core rate.

Required state/behavior:
- eight-register x87 stack using 80-bit extended internal format
- control word, status word, tag word
- last instruction pointer/opcode and data-operand pointer
- IEEE 754-era exception/status behavior
- FN/FWAIT distinction and TS/MP/EM interaction
- FERR# external compatibility path and CR0.NE interrupt-16 path
- IGNNE# semantics when NE=0
- FSAVE/FNSAVE/FRSTOR/FSTENV/FLDENV mode-dependent environment formats
- FPU may execute concurrently with integer logic, but architecturally visible completion/exceptions obey Intel ordering

An unmasked numeric exception sets the status exception + summary state and asserts FERR#. With NE=1 it is reported as vector 16 at the documented next floating-point/WAIT boundary; with NE=0 the DOS-compatible FERR#/IGNNE# external path applies.

Source anchors: 241245-002 floating-point chapter and pp.2-10 onward; 240486-002 chapters 14-19.

## 12. Parallel host implementation rule

The real chip overlaps fetch/decode/execution/address translation and the FPU can progress in parallel with integer work. VMB may therefore use multiple host workers, but host parallelism must never define emulated time.

Rules:
1. Motherboard runtime is the sole authority for elapsed emulated picoseconds/external clocks.
2. CPU derives two internal core ticks per accepted 33 MHz board clock for the DX2/66.
3. Worker threads may prepare pure results (decode templates, arithmetic/FPU results, address-translation candidates, cache-tag lookup results).
4. Memory, I/O, interrupt, cache fill/invalidate, register retirement, exception delivery and pin transitions commit in one deterministic sequence.
5. A worker may not read mutable device state without a board-issued transaction.
6. A worker may not advance clocks or sleep to simulate hardware timing.
7. If a prepared result is invalidated by an interrupt, self-modifying code, cache invalidation, page-table write or control transfer, discard it rather than forcing it to retire.

The design goal is host throughput with board-authentic timing, not a host-thread race masquerading as a pipeline.

## 13. Intel487 SX — separate historical chip, not companion to DX2/66

The Intel487 SX belongs to the Intel486 SX upgrade architecture. Intel documents a two-socket SX/487 system and BIOS detection by FPU behavior. Upgrade-present logic causes the original processor to relinquish the bus. Later Intel OverDrive devices use the same general takeover idea.

Therefore:
- do not expose Intel487SX as a numeric-coprocessor drop-down for a DX2/66 Socket 3 configuration;
- model Intel487SX later as a separate processor package with its own 169-pin upgrade-package identity and upgrade-present/takeover output;
- on a board which actually has the SX + 487/upgrade socket topology, installing the 487 selects it as the active processor-bus owner and forces the original SX into upgrade power-down/tri-state behavior;
- software then sees a 486-class processor with a functioning FPU, not an 80287-like slave protocol.

Intel's 1992 programmer's reference explicitly describes the interface as two distinct sockets and gives the FSTCW detection distinction (FFFFh on SX-only vs 037Fh when the Intel487 SX is present).

Source anchors: 240486-002 initialization section 10.2; 241731-002 OverDrive/socket sections.

## 14. Explicit non-goals for the first DX2/66 milestone

These are real family features but should not be allowed to expand the first target indefinitely:

- DX4 write-back extensions
- SMM/SL-enhanced-only signals not present on the plain target stepping
- Pentium OverDrive / P24T execution
- AMD/Cyrix implementation differences
- full JTAG boundary-scan execution unless diagnostics demand it
- transistor-level CHMOS simulation

The code should leave package/signal extension points for those without pretending they already exist.
