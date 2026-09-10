# Virtual Machine Bench — Motherboard Backplane Refit Checkpoint 03

Date: 2026-09-09
Status: runtime motherboard factory seam complete; NEAT behavior intentionally delegated unchanged; QDI fixed fabric still gated for CP04.

## Added
- `IMotherboardRuntimeBackplane` runtime contract.
- `MotherboardRuntimeFactory` and `MotherboardRuntimeManager`.
- `NeatAt286RuntimeBackplane`, an adapter over the existing proven `Declares.InitializeHardware()` graph.
- Explicit pending QDI runtime implementation that cannot register substitute hardware.
- Form1 startup now asks the runtime manager to instantiate the persisted motherboard instead of directly assuming one global motherboard implementation.
- Commit gating now queries the runtime factory rather than hard-coding knowledge of QDI readiness into the UI.

## Preserved
- Existing NEAT device construction and registration order.
- Existing global device identities used by the 80286 core/diagnostics.
- Existing ISA removable-card behavior.
- No QDI device is registered in this checkpoint.

## Next
Checkpoint 04 replaces the QDI pending runtime with a real QDI/OPTi fixed-fabric object and implements the documented 82C895/82C602 configuration decode skeleton.
