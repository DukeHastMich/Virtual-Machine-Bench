# SingleStepTests/80286 host integration — 9-6-26 12:30 AM checkpoint rebase

This copy was rebuilt from the authoritative **9-6-26 vm 12:30 AM checkpoint full folder.zip**,
not from `new source root.zip` and not from any earlier generated VMB snapshot.

Upstream test suite: https://github.com/SingleStepTests/80286

## Host UI

`Machine -> Diagnostics -> 80286 hardware single-step tests...`

The host tool can:

- browse to an existing `SingleStepTests/80286` checkout;
- clone/update the public repository using the host `git` executable;
- discover and read `v1_real_mode/*.MOO.gz` directly;
- run a selected number of cases (`0` means the entire opcode-form file);
- honor the upstream `revocation_list.txt`;
- reconstruct expected architectural state by overlaying the MOO final delta on the initial register/RAM state;
- report aggregate pass/fail/revoked counts and retain the first 100 failures.

The upstream corpus itself is deliberately **not bundled** into VMB.

## Isolation

The test host does not mutate the running virtual PC. It owns an isolated test fabric consisting of:

- `HardwareBus`
- 16 MiB `NeatMemoryController286`
- `NeatMotherboardBridge286`
- `CpuLocalBus286`
- `Processor286`
- a test-only all-port I/O fixture that replays I/O read values captured in the hardware cycle trace (latched port from IOR/Ts, data from asserted I/O-read/Tc)

A20 is enabled because the upstream real-mode corpus assumes the full 16 MiB address space is mapped and writable.

## MOO handling

`SingleStep80286TestHost.vb` contains a native VB/.NET reader for the MOO chunks needed by the 80286 corpus.
`.MOO.gz` files are read directly with `GZipStream`; no Python conversion step is required.

The host compares architectural state. The upstream per-T-state bus/pin trace remains available in the corpus for
later cycle-exact diagnostics; it is not used as the pass/fail criterion in this first host integration.

## Source changes from the full checkpoint

Only these production/source-project files are changed or added by this integration:

- `Form1.vb` — adds the Diagnostics menu entry and opens the host form
- `VirtualComputer.Modern.vbproj` — compiles the new host source
- `SingleStep80286TestHost.vb` — new isolated runner/UI/MOO reader
- `THIRD-PARTY-NOTICE-SINGLESTEPTESTS-80286.txt` — attribution/license notice
- this notes file and the baseline verification text file

`ProcessorCore.vb`, `HardwareBus.vb`, and the rest of the emulator are not modified by the tester integration.

## Build validation

The generation container does not have `dotnet`, MSBuild, Mono, or `vbc`, so no compile result is claimed here.
Static validation checks project XML, all explicit Compile paths, duplicate Compile includes, source-diff scope, and
baseline hashes. Visual Studio on the Windows host remains the compile authority for this snapshot.

## Visual Studio compile fix 2026-09-06

The initial rebased package had one VB compile error (BC30577) because the local Browse button variable
`browseInBed` shadowed the event-handler method `BrowseInBed` (VB identifiers are case-insensitive).
The local control is now named `browseButtonInBed`; the handler method and behavior are unchanged.


## Attribution / About update
- Added an `About...` button to the 80286 test host.
- Upstream test author is explicitly credited as Daniel Balsom / SingleStepTests.
- About dialog links directly to Daniel Balsom's GitHub profile, the SingleStepTests/80286 repository, and the upstream MIT license.
- VMB host integration is credited separately as adapted by ChatGPT by OpenAI.
- The dialog explicitly distinguishes the VMB adapter from the upstream test corpus, methodology, and tooling.

## Full-suite torture / running-summary / export update

The host now includes a full-suite regression mode intended to run the entire `v1_real_mode` corpus without manual opcode-file selection:

- **TORTURE ME TILL I DIE — ENTIRE SUITE** walks every `v1_real_mode/*.MOO.gz` file in filename order.
- The tall left-hand pane keeps a compact live per-file summary: PASS / FAIL / SKIP and current state.
- The headline above the list keeps running file/test/pass/fail/skip totals.
- **Stop on first failure** now stops on the first failing *test case*, not merely after the rest of that failing file finishes.
- Selected-file runs with a nonzero test limit are marked `PARTIAL` rather than being falsely presented as a whole-file pass.
- **Export report...** writes a UTF-8 plain-text report designed to be readable by both a human and an AI assistant.

The exported report contains stable key/value metadata, aggregate totals, a checkbox-style per-file TODO list, retained failure details, and explicit AI handoff instructions. Failed files become concrete unchecked fix items; WAIT/PARTIAL/CANCELLED files are distinguished from architectural failures so they are not misdiagnosed.

The runner retains the first 100 detailed failing cases per opcode-form file while preserving the full failure count. This keeps a badly failing CPU from producing an unbounded multi-million-line in-memory report while still giving a useful reproduction/fix queue.

## SplitContainer construction fix
- Fixed startup `InvalidOperationException`: `SplitterDistance must be between Panel1MinSize and Width - Panel2MinSize`.
- Cause: WinForms validates `SplitterDistance` while the `SplitContainer` still has its tiny framework-default construction width; the final docked width is not available yet.
- The left-pane split is now applied from the form's `Shown` event after docking/layout gives the control its real client width. The hard constructor-time panel minimums were removed so DPI/layout scaling cannot make the form fail before it opens.

## 2026-09-07 CPU fix — D6 SALC
- Full-suite hardware report showed opcode D6 behaving as SALC on the tested 80286 corpus: AL becomes FFh when CF=1, otherwise 00h; flags are unchanged.
- `ProcessorCore.vb` now implements that behavior instead of treating D6 as a NOP.
- This package also preserves Eric's live AAD host-overflow edit (`baseValue As UInt64`) that allowed the first full torture run to continue to completion.
- No other CPU behavior was intentionally changed in this package.

## 2026-09-07 — real-address segment-overrun fix

After the first complete hardware-corpus run, many otherwise-correct memory-operand
instructions differed only because the Harris 80286 took real-address-mode segment
overrun interrupt 13 while VMB allowed a multi-byte operand to wrap from offset
FFFFh back to 0000h.

`ProcessorCore.SegmentAddress()` now enforces the 80286 real-address 64 KiB operand
boundary: after 16-bit effective-address calculation, any operand whose final byte
would lie beyond offset FFFFh raises interrupt 13 with no error code. Protected-mode
segment validity/type/limit handling is unchanged. This brick intentionally does
not alter ALU semantics or the separate instruction-fetch/prefetch path.

Targeted rerun suggestion: opcode-form files such as 01.MOO.gz and 03.MOO.gz,
which previously showed recurring CS changes on word memory references near the
segment boundary.

## 2026-09-07 — Harris 80286 one-bit right-shift AF behavior

Hardware-corpus follow-up after the real-address segment-overrun fix:

- `01.MOO.gz` and `03.MOO.gz` were reported by the user as 5000/5000 after the segment-overrun fix.
- The first 100 retained failures for `D0.5` (SHR r/m8,1) all differed only by AF: the hardware expected AF=1 while the emulator preserved AF=0.
- `D0.7` (SAR r/m8,1), `D1.5` (SHR r/m16,1), and `D1.7` (SAR r/m16,1) show the same one-bit right-shift AF signature in the retained hardware-corpus failures.
- `Shift8` and `Shift16` now assert AF for one-bit SHR/SAR (`kind=5` or `kind=7`, `count=1`).
- Multi-bit/CL/immediate shift AF/OF behavior is intentionally not changed by this brick; those forms remain a separate hardware-corpus task.

Validation requested: run `D0.5.MOO.gz` with Tests=0 first, then `D0.7`, `D1.5`, and `D1.7` if green.
