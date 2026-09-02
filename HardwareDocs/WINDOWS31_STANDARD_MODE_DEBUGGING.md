# Windows 3.1 Standard-mode debugging notes

## 2026-09-01 observed failure modes

- A completed failing run reached Windows module initialization, where a far
  initializer returned AX=0001; KRNL286 later terminated through INT 21h/4C01.
- A later run did not terminate. Two dumps five minutes apart showed the same
  registers and a tight execution window at selector 05D7, offsets 103F-105D.
- During those five minutes, 80286 shutdown resets increased from 42,951 to
  43,935 and ESC attempts increased in lockstep. Disk DMA, VGA, sound, and
  network activity did not advance. Port 64h polling continued.
- The ESC traffic captured around mode transitions is WAIT + DB E4 (FSETPM),
  which is valid 80287 behavior. No #NM trap or NPX ERROR was observed.

The 2026-09-01 13:18 dump resolved the loop. Selector 05DF mapped to physical
CS base 003F5B70 and executed this sequence at offsets 1053-105D:

    MOV DX,[000A]
    ADD DL,05
    IN  AL,DX
    AND AL,60h
    JNZ 103F

`[DS:000A]` supplied 03F8h, so the loop polls the COM1 16550 line-status
register at 03FDh. COM1 returned 60h (THRE + TEMT) and the conditional jump
therefore repeated. This excludes the 80287, S3/VGA, 8042, disk, and reset
substrate as the direct source of this particular stall. The surrounding code
later writes COM1 THR, consistent with a serial mouse/communications driver.

Do not invert or fabricate LSR bits to make this loop pass. On a real 16550,
60h is the correct idle-transmitter value. We still need the immediately
preceding UART history to distinguish a guest-driver/configuration problem from
an emulated transmit-state timing problem.

### Resolution from the next UART-instrumented dump

The trace showed the driver writing 2Ah to COM1 THR, followed by 1,172 reads of
LSR=20h while the shift register was busy, transmitter completion, and then
millions of LSR=60h reads. It never observed LSR=00h. The cause was
`WriteTransmitter` calling `BeginTransmitIfIdle` synchronously in the same ISA
OUT transaction. That made THR empty again before the CPU could read LSR.

`Uart16550A` now models the missing THR-to-TSR transfer interval. Loading an
idle THR clears THRE immediately; transfer to the shift register occurs at the
next divisor-derived 16x transmitter clock. This is bus-visible 16550 behavior,
not a driver-specific status override. The wake scheduler includes this event,
FIFO transmitter resets cancel it, and diagnostic output reports its remaining
time. The correction built successfully with zero warnings/errors.

### Result after the THR timing correction

The 13:49 dump proves the former COM1 stall is fixed. For every transmit, the
guest now observes LSR=00h during the THR holding interval, then LSR=20h while
TSR shifts, and finally LSR=60h when idle. The Logitech-selected driver passed
the former 2Ah wait and transmitted a subsequent 3Fh command.

Setup nevertheless terminated with AX=4C01 after a Windows module initializer
returned AX=0001. This matches the configured guest/peripheral mismatch:
`Declares.vb` attaches `MicrosoftSerialMouse` to COM1 and implements the
Microsoft two-button 1200-baud protocol, while Setup was configured for a
Logitech mouse driver. The driver sends Logitech identification/configuration
commands (the captured 2Ah and 3Fh bytes); a Microsoft-protocol mouse is not
supposed to answer them. The resulting module-load #NP/#GP cleanup is Windows'
normal movable-segment failure path, not evidence that the 80287 or protection
checks should be weakened.

Next installation test: choose a Microsoft serial mouse (COM1) driver matching
the emulated `MicrosoftSerialMouse`. Do not add Logitech command responses to
that peripheral unless a separate authentic Logitech device model is added and
selected in the machine configuration.

### Result with the Microsoft serial-mouse selection

The 14:13 dump contains no Logitech THR command writes, proving Setup followed
a different mouse-driver path. It nevertheless fails at the same absent LDT
selector 0B8F and exits from 048F:9586 with AX=4C01. Therefore the mouse-driver
mismatch was real but was not the cause of this termination.

The protected-mode forensic trace shows Windows entering its movable-segment
loader and constructing text containing "caused a General Protection" before
the failure cleanup. `ProcessorCore.vb` now records 192 stack bytes in only the
targeted 048F:5520-5580/56E0-5830 loader ranges, sufficient to retain the full
guest-generated message and module name on the next run. This is direct DRAM
observation only and does not change selector, exception, or bus behavior.

The first reproduction after that change revealed that
`ForensicStackWordsInBed` still imposed an internal 32-byte maximum, so the
record again ended at "caused a General Protection Fau". The helper cap is now
192 bytes; existing callers still request and receive their smaller 16-32 byte
windows. Compilation to an alternate output succeeded with zero warnings and
errors while the running emulator held the live executable open.

## Persistent targeted diagnostics

`ProcessorCore.vb` now retains a bounded detailed execution ring across
processor-only AT shutdown resets. Every 1,024 instructions it records:

- CS:IP, physical address, and 16 instruction bytes;
- general and segment registers, FLAGS, MSW, CPL;
- cached CS/SS/DS bases plus CS limit/access;
- LDTR, TR, GDTR, and IDTR;
- eight stack words read by direct DRAM peek.

It also records pre-reset and post-reset snapshots. Direct DRAM peeks avoid
adding guest-visible memory-bus transactions. `Dump All` includes this ring
under the existing recent-execution section.

`KeyboardController8042` now run-length-compresses port 64h status reads. Its
diagnostic trace reports each status transition and the current repeated-value
run without retaining millions of identical reads.

`Uart16550A` now retains a bounded 256-event bus-visible history across warm
resets. It records all guest register writes, transmit-shift start/completion,
and run-length-compressed LSR reads. `Dump All` includes this beneath each
UART's normal diagnostic summary. This history is deliberately observational:
it does not alter UART timing, register results, IRQ routing, or peripheral
traffic.

## Next reproduction

Run Windows until the lamps settle into the repeating pattern, then use Dump
All while the emulator is still running. Inspect the COM1 history immediately
before the long `LSR read ... value=60` run. In particular, determine whether a
THR write and TX shift start occurred, whether the shift completed before the
first poll, and which LCR/divisor/MCR writes preceded it.

## 2026-09-01 KRNL286 LAR failure

The full targeted stack capture decoded the guest error as a general
protection fault in `KRNL286.EXE` at `0010:58C8`.  Immediately before it,
Windows probed selector `0B8F`, whose LDT descriptor had access byte `7B`
(DPL 3 readable code, Present clear).  The descriptor was deliberately absent:
the original transfer had correctly raised `#NP(0B8C)` so KRNL286 could demand
load it.

At KRNL286 offset 58C2, `LAR BX,BX` left BX unchanged and cleared ZF because
the emulator incorrectly used its segment-load visibility test, including the
Present-bit requirement.  Windows subsequently followed the wrong path and
faulted while using an invalid ES cache.

Intel's 1987 *80286 and 80287 Programmer's Reference Manual*, pages 11-3 and
8-60, specifies that LAR checks table bounds, descriptor type, CPL/RPL versus
DPL (with conforming-code handling), returns the access byte in AH and zero in
AL, and causes no selector protection exception.  It does not test the Present
bit.  `ProcessorCore.vb` now has distinct non-faulting descriptor-probe rules
for LAR/LSL/VERR/VERW.  Actual segment loads retain the Present check.  LAR and
LSL also enforce their 80286 descriptor-type sets; LSL excludes gates.

The first run after that correction advanced to a visible `WINSETUP caused
Segment Load Failure in module SETUP.EXE at 0003:00B6` dialog.  The immediate
inward RETF at 048F:582B is Windows' deliberate handoff to its #GP error path
and must remain rejected.  The initiating failure is earlier: KRNL286
demand-loads selector 11DF, changes its descriptor to base 3C48F0, limit 042F,
access F3, and begins its relocation-link walk at offset 02A9.  The loaded word
there is 9AFF, so the next word access is correctly rejected for exceeding the
042F segment limit.  The raw loaded bytes at 02A0..02AF include
`16 50 1E 68 1F 0F 9A 34 01 FF 9A 03 0E 03 10 00`; the unresolved question is
whether the bad 9AFF link originates in the executable/relocation input or an
earlier guest-visible storage/copy operation.

`ProcessorCore.vb` now records a bounded `WINDOWS RELOCATION CHAIN` event for
048F:6D00-6D80 with exact bytes, registers, the DS hidden descriptor cache, and
the two link bytes read by direct DRAM peek.  It is diagnostic-only and does
not relax segment limits or add guest-visible bus transactions.

A later run reached interactive graphical Setup, then faulted in SETUP.EXE
segment 7 at offset 1C09.  The exact instruction was
`9A 89 16 00 00` (far CALL 0000:1689); the CPU correctly raised #GP because a
null selector cannot be a far-transfer target.  Relocation chains captured for
the preceding demand-loaded selector 0EBF were valid and ended in FFFF, so this
is distinct from the earlier invalid 9AFF chain.  The failing segment's
selector fixup was either skipped or overwritten later.

The forensic stream had reached its 65,536-instruction bound before the 120F
failure.  It now supersedes the existing stream at every architectural #NP
demand load, ensuring `cpu-protected-forensic.bin` retains the most recently
loaded movable segment and its fixup pass.  Restarting the diagnostic writer
does not alter exception delivery, descriptor contents, scheduler time, or
guest-visible bus traffic.

## 2026-09-01 moving module failures and ATA sector phasing

Subsequent reproductions reached graphical Setup but failed at different valid
modules and offsets: SETUP.EXE 0007:1C09 and USER.EXE 001A:0D61.  The moving
failure site, together with an earlier malformed relocation link, is evidence
for a load/copy-path problem rather than permission to weaken 80286 protection.

The host scheduler audit found that wall time supplies only a bounded CPU
execution budget.  Motherboard devices advance from guest T-states actually
retired by the CPU, so diagnostic overhead may change throughput and slice
boundaries but must not directly advance a guest device clock.

The primary ATA controller did contain a software-visible protocol defect:
READ/WRITE SECTORS exposed every requested sector as one command-sized DRQ
array.  An ATA drive presents one 512-byte PIO data phase per sector, then
decrements Sector Count, advances the CHS/LBA task file, and presents the next
DRQ/IRQ phase.  `IdeController.vb` now implements those sector boundaries
through the existing 1F0h data-port path.  No direct RAM or DOS/Windows file
shortcut was added.  Dump All now contains `ATA / ATAPI STORAGE`, including a
bounded, host-only history of commands and sector DRQ phases retained across
processor-only reset.  This is the next reproduction's primary evidence.

## 2026-09-01 deterministic SETUP.EXE corruption resolved

Read-only FAT extraction proved that floppy `/SETUP.EXE`, HDD
`/WINDOWS/CABS/SETUP.EXE`, and HDD `/WINDOWS/SETUP.EXE` are byte-identical
(SHA-256 `DD5A9288A0267D0476DA5B7697963708B27611B10134410496025D6E4F1FD7CB`).
The media and installed file therefore were not corrupt.

The forensic stream showed the correct source word `FFFF` arrive from disk.
During later 80286 protected-mode exits, the reset BIOS used
`SS:SP=0000:7000` before checking CMOS shutdown status. BIOS calls pushed six
bytes at physical `006FFA-006FFF`, which overlapped Windows' still-live loader
buffer. A later `REP MOVSW` copied those BIOS stack bytes into the demand-loaded
segment and changed its relocation terminator from `FFFF` to `9AFF`.

The reset entry now uses the BIOS-owned EBDA scratch region for its small
shutdown-dispatch stack. Only a normal POST switches to the conventional
`0000:7000` scratch stack; protected-mode resume paths never do. This keeps the
real AT reset/CMOS/warm-vector path and removes no guest-visible protection.

`Tools/InspectFatImage.ps1` is the saved read-only FAT12/FAT16 image inspector
used to verify the media while the emulator held the HDD image open.

## 2026-09-01 serial mouse movement latency

Host movement previously armed the Microsoft serial mouse for a full 25 ms of
guest time, while a button transition armed it immediately. On a slow guest,
pending movement therefore appeared only when a click flushed the report.
`MicrosoftSerialMouse.AddHostMovement` now wakes an idle report immediately.
The report still enters through the mouse's 1200-baud 7N1 transmitter, the
NS16550A receive path, IRQ4, and the installed guest driver; queued follow-up
packets retain the hardware sampling interval and UART backpressure.

## 2026-09-01 shutdown RESET# detector re-arm race

After printer setup, Windows appeared to stall while searching `C:\` and the
CPU RUN lamp remained dark. The dump showed the CPU held in the intentional
zero-IDT 80286 shutdown at `0078:0C62`, while preceding ATA reads had completed
normally and advanced through consecutive LBAs.

The NEAT motherboard shutdown detector was re-armed only by an end-of-slice
telemetry sample which did not contain the SHUTDOWN bit. A large slice can span
the complete reset/BIOS/Windows resume and reach the next intentional shutdown;
there is then no sampled running-only slice, so the second shutdown is mistaken
for the already serviced one and RESET# is suppressed.

`ResetProcessorAfterShutdown` now re-arms the detector immediately after the
processor RESET# callback completes. That is the actual electrical deassertion
boundary and does not depend on Windows, host wall time, or slice size.
# 2026-09-01 TIMER.DRV copy-denied diagnostic follow-up

Windows Setup reached its graphical copy phase and reported that `TIMER.DRV`
could not be opened/created because access was denied.  The first Dump All was
valid, but it occurred after a reset and the general INT 2Fh ring had been
overwritten by AX=1689h idle/yield traffic.  The preserved INT 21h file-service
trace contained no `TIMER.DRV` operation, indicating that the relevant Windows
I/O may have used DOS's internal INT 2Fh/11xx redirector interface.

`ProcessorCore.vb` now preserves INT 2Fh AX=11xx entry/return records in the
existing bounded DOS file-service trace.  This is diagnostic observation only:
it peeks already-backed low DRAM without issuing bus cycles and does not
intercept, answer, accelerate, or otherwise alter a guest request.  The noisy
AX=1689h yield calls remain outside this preserved queue.  The saved read-only
`Tools/InspectFatImage.ps1` utility also bounds-checks FAT offsets and cluster
chains and reports file attributes, so malformed chains cannot abort the scan.

Automatic Windows/DPMI trace arming now retains the bounded interrupt, DPMI,
and file-service queues without automatically opening the 65,536-instruction
binary forensic stream.  The full binary stream remains available when tracing
is explicitly started by the operator or by a narrowly targeted fault trigger.
This removes diagnostic disk and serialization work from ordinary Windows runs
without changing any CPU, bus, interrupt, or device timing visible to the guest.

## Mode 12h planar colour corruption

The Windows 3.1 splash and desktop showed correct independent red and blue logo
panes but a blue background contaminated by the red plane (magenta).  This ruled
out a RAMDAC RGB/BGR swap.  The VGA write-mode ledger showed substantial mode 3
use.  `WriteVgaMemory` formed mode 3's effective mask but omitted the Data Rotate
raster operation between Set/Reset and the destination latch.  VGA write mode 3
now performs that operation before the masked merge, matching the hardware data
path.  The diagnostic snapshot also records GC0 through GC8 so future captures
retain Set/Reset, Enable Set/Reset, Rotate/ROP, Read Map, Mode, Miscellaneous,
Color Don't Care, and Bit Mask state.

The next Windows splash capture remained magenta and its lifetime ledger showed
zero mode-3 writes (mode 0: 182414, mode 1: 1, mode 2: 37212, mode 3: 0).
Therefore the authentic mode-3 correction is retained, but it cannot be the
cause of this particular splash corruption. The same dump retained separate
pure-blue and pure-red DAC entries, again excluding a host RGB/BGR swap. Dump
All now records all Attribute Controller palette registers 00h-0Fh, every
logical-colour-to-DAC/RGB resolution, and a visible planar logical-colour
histogram. This will distinguish an AC 3C0h flip-flop/PAS programming fault from
incorrect plane data without changing any guest-visible state.

The completed ledger proved that mode-2 writes used replacement ROP 0 and that
the CPU deliberately supplied logical colour Dh 20,388 times. Inspection of
Windows' local `VGALOGO.RLE` and disassembly of `VGALOGO.LGO` then exposed the
actual fault: RLE colour Dh is grey/white, and the loader calls BIOS INT 10h
AX=1002h with an identity 00h-0Fh Attribute Controller table before programming
DAC entries 00h-0Fh. The option BIOS lacked AX=1002h, leaving mode 12h's EGA
compatibility mappings 38h-3Fh active; logical Dh therefore selected DAC 3Dh
(magenta) instead of the newly loaded DAC 0Dh grey/white.

`stealthpro_vgabios.asm` now implements IBM VGA INT 10h AX=1002h through the
real 3DAh/3C0h Attribute Controller sequence: reset the flip-flop, program all
16 palette registers and overscan from ES:DX with PAS cleared, then restore PAS.
`Firmware/build-video-bios.ps1` reproducibly assembles the 32 KiB option ROM,
patches its checksum, validates it, and replaces the ROM/listing atomically.

## Host pointer to Microsoft serial mouse

The display frontend formerly discarded every `MouseMove` until a click enabled
Win32 capture, and consumed that first click. The PictureBox now converts
successive hover positions to relative movement immediately and sends button
edges without requiring capture. Leaving the display resets the host reference
point and releases any held buttons. Optional captured/center-warp operation is
still available from the Cards menu for unlimited relative travel. Both paths
terminate at `MicrosoftSerialMouse.AddHostMovement`/`SetHostButtons`; packets
continue through 1200-baud framing, the 16550A receive path, and IRQ4.

Follow-up testing exposed two host-boundary mistakes. The fitted Windows 3.1
driver's observed serial-mouse quadrature polarity is opposite to WinForms
client coordinates on both axes. The adapter now converts both axes together,
once, before Microsoft packet framing. The host pointer is now
hidden, with balanced hide/show state, whenever it is over the emulated CRT and
is restored on leave, capture release, deactivation, or shutdown. Neither fix
bypasses the emulated serial transport.

## Automatic-trace performance

A desktop dump showed more than one million Windows `INT 2Fh AX=1689h`
idle/yield calls and about 278,000 repetitions of the same protected-mode
dispatch fault at `0078:0C62`. Although their queues were bounded, every event
still paid for register formatting, strings, and queue replacement. Automatic
tracing now ignores AX=1689h and suppresses that one known repetitive DPMI
dispatch pair. Other INT 2Fh services, DOS/redirector file calls, and distinct
CPU exceptions remain captured. This is telemetry filtering only; both guest
operations still execute through the original CPU and interrupt paths.
