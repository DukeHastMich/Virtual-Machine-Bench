# SingleStepTests/80286 harness audit — 2026-09-07

Base: the user-tested BROAD-SHIFT package, before the unvalidated Big-Four CPU candidate.

## What this pass fixes

- Parse MOO v1.1 top-level and per-test-final `RMSK` register masks.
- Parse `EXCP` (exception number plus stacked-FLAGS address).
- In default **OFFICIAL MOO CONFORMANCE** mode, compare registers through the effective MOO mask and apply the FLAGS mask to the two RAM bytes at `EXCP.flag_address`.
- Add optional **Strict Harris undefined bits** mode. This intentionally ignores `RMSK` so raw captured undefined bits can still be studied as Harris-silicon behavior without confusing them with upstream conformance failures.
- Track every local-DRAM byte actually written while a test executes and scrub those addresses before the next case, preventing cross-test contamination from unexpected writes.
- Assert that the isolated bed is exactly 16 MiB, A20-enabled, and writable at representative addresses inside the VGA/option-ROM/system-ROM apertures and above 1 MiB.
- Preserve existing execution termination behavior. `Processor286.RunCycle(1)` is an instruction-count call, not a T-state call. Existing full-suite results also already had direct flow-control/HALT forms green.

## Audit conclusions

- EXCP/RMSK omission: valid harness defect; fixed here.
- Cross-test rogue-write persistence: valid harness defect; fixed here.
- Mini-AT memory concern: current isolated setup loads no ROM/MMIO and is effectively flat DRAM; this pass adds runtime sentinels so that assumption cannot silently regress.
- TEST index concern: no change. MOO v1.1 documents a raw uint32 index at the start of each TEST payload, and Daniel Balsom's official `moo2json.py` reads it that way.
- Blanket FLAGS masking: intentionally not done. Default mode honors only masks supplied by MOO. Fixed/reserved behavior remains testable; strict mode additionally exposes raw undefined silicon bits.

## Upstream references checked

- SingleStepTests/80286 README: 16 MiB writable RAM, no wait states, HLT termination/injection methodology, EXCP flag-address purpose.
- dbalsom/moo MOO v1.1 format: top-level/final RMSK, raw TEST index, EXCP structure.
- SingleStepTests/80286 v1.1 changelog: corrected EXCP odd-SP flag address and initial real-mode FLAGS top nibble.

## Validation sequence

1. Build in Visual Studio (the generation environment has no compile authority).
2. Default mode, run `F6.6.MOO.gz`, `Tests=0`.
3. Run the same file with **Strict Harris undefined bits** checked. The A/B should demonstrate whether RMSK was responsible for the old flags-heavy red count.
4. Default mode, run `F6.4`, `69`, `D4`, and `27`.
5. Then run the entire suite in default mode and export a new report.
6. Do not apply the parked Big-Four CPU semantic candidate until this corrected harness establishes a new baseline.
