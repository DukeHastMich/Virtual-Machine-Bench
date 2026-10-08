# VMB Intel486 DX2/66 + Intel487 SX construction plan

Date: 2026-09-09
Authoritative base: Motherboard Backplane Refit CP06 final.
Green 80286/80287 code is frozen and must remain byte-identical unless Eric explicitly opens a separate regression task.

## Architecture rule

The motherboard owns simulated time. Silicon components consume board-granted clock/cycle budgets. Multiple host workers may prepare pure computation, but all guest-visible retirement and all bus/pin effects commit deterministically on the machine owner thread.

## CP01 — archaeology + immutable target contract

Deliver:
- Intel486 DX2/66 silicon extraction dossier
- explicit 487SX separation note
- baseline hashes of protected green files
- construction plan

Gate:
- no executable behavior changed
- ProcessorCore286 and Intel80287 hashes unchanged

## CP02 — family-neutral machine/processor electrical contracts

Deliver:
- neutral processor-package interface
- neutral motherboard memory/bus transaction types supporting 8/16/32-bit transfers
- explicit cycle definition, ready/burst/cacheability/arbitration fields
- adapter over existing 286 path with no behavioral change
- QDI-facing 486 bus contract, still power-on gated

Gate:
- NEAT 286 build/source semantics untouched at the core
- QDI still cannot lie about being executable

## CP03 — Intel486 DX2/66 package shell + board-clock authority + host workers

Deliver:
- `Intel486Dx2` first-class package
- 168-pin/5V/33 MHz external/66 MHz core identity
- register file and documented reset state
- explicit input/output signal state
- deterministic worker pool for pure work only
- internal 2x core-clock accumulator controlled by external-clock grants
- integrated FPU object physically owned by the package

Gate:
- reset-state self-test
- worker determinism test: same input stream gives same architectural/pin state regardless of worker count
- no device/bus calls from worker threads

## CP04 — 486 memory-management silicon

Deliver:
- six segment caches with 32-bit bases/limits/attributes
- real/protected/v86 translation rules
- CR0/CR2/CR3
- two-level 4 KiB paging
- 32-entry four-way TLB
- access/dirty/protection/page-fault behavior
- A20M# applied at the processor physical-address boundary
- INVLPG and CR3/TLB invalidation

Gate:
- deterministic translation tests for real/protected/v86/paged cases
- exact page-fault CR2/error-code tests

## CP05 — instruction front end + 32-bit integer execution

Deliver:
- prefix scanner including segment, LOCK, REP, 66h, 67h
- 16/32-bit ModR/M + SIB decoding
- integer register partial-width semantics
- 386 instruction superset needed by 486 plus BSWAP/XADD/CMPXCHG
- 32-bit stack/control transfers/interrupt frames
- protected-mode gates/tasks and v86 behavior required by Windows/386 software
- #UD/#GP/#SS/#NP/#TS/#DF/#AC restart ordering

Gate:
- focused host-side semantic vectors
- real-mode BIOS instruction coverage before QDI boot attempt

## CP06 — bus engine, internal cache and external timing handshake

Deliver:
- 8 KiB, four-way, write-through, 16-byte-line internal cache
- four 32-bit write buffers
- KEN#/PCD/PWT cacheability
- INVD/WBINVD/FLUSH#/EADS#/AHOLD invalidation behavior
- 8/16/32 dynamic bus sizing
- BE3#:BE0#, A31:A2 transaction generation
- RDY#/BRDY#/BLAST# burst state machine and Intel burst ordering
- LOCK#/PLOCK#, HOLD/HLDA, BOFF#/BREQ
- two-cycle locked INTA transaction

Gate:
- bus trace tests against Intel timing/state tables
- board wait-state policy, not CPU host speed, determines bus duration

## CP07 — integrated DX2 FPU

Deliver:
- x87 80-bit stack/state inside the DX2 package
- x87 decode/execute surface required by Intel486
- FSAVE/FNSAVE/FRSTOR/FSTENV/FLDENV formats
- FERR#/IGNNE#/CR0.NE behavior
- vector 7 / vector 16 interactions
- concurrent pure FPU arithmetic worker with deterministic retirement

Gate:
- existing 80287 remains untouched
- DX2 FPU self-tests cover stack, exceptions, rounding, environment save/restore

## CP08 — QDI native fixed fabric + Socket 3 activation

Deliver:
- QDI DRAM/ROM controller independent of NEAT classes
- QDI local-bus bridge to existing shared device fabric
- OPTi 82C895 controls actual RAM/cache/timing behavior where documented
- board-specific firmware path and 256-byte CMOS path
- 33 MHz clock option + DX2/66 package compatibility binding
- runtime factory selects `MachineRuntime486`, never widens `MachineRuntime286`

Gate:
- QDI `FixedFabricAvailable=True` only after native memory/bus and DX2/66 are present
- applying QDI + DX2/66 physically constructs the new machine graph
- selecting NEAT reconstructs/retains the old graph without sharing processor-specific bridges

## CP09 — Intel487 SX separate package archaeology implementation

Deliver:
- `Intel487Sx` as its own 169-pin upgrade processor package
- explicit MP#/UP# takeover output
- active processor-bus ownership semantics for a future SX/upgrade-socket motherboard
- FPU-present detection behavior and reset identity
- no exposure as a slave NPX beside DX2/66

Gate:
- a synthetic SX dual-socket fixture proves original SX tri-states/powers down when 487SX is fitted
- removing 487SX restores SX ownership

## CP10 — conformance, performance and closure

Deliver:
- deterministic trace export for core clocks, external bus cycles and retirement
- worker-count A/B test (1 vs N workers) with identical guest-visible result
- CPU torture/instruction suites where available
- BIOS POST/QDI smoke
- DOS, HIMEM/EMM386, Windows 3.1 enhanced-mode smoke as progressive gates
- final archaeology mapping of every modeled feature to Intel source section

Gate:
- no timing derived from host wall-clock/thread scheduling
- no fake hardware paths
- green 286 hashes still match baseline
