# Motherboard Backplane Refit — Checkpoint 06 / closure

Date: 2026-09-09
Status: configurator/profile refit complete; second-board execution boundary explicitly gated pending a real 80486 machine fabric.

Final cleanup beyond CP05:

- fixed cross-board RAM staging so a QDI RAM choice remains attached to the pending QDI board even when its capacity happens to equal the live NEAT board;
- motherboard activation validation now checks both fixed-fabric availability and the selected processor package;
- live chipset diagnostics are obtained through the motherboard runtime boundary where possible instead of teaching the configurator that `Chipset` always means NEAT;
- chipset help text is board-neutral;
- Firmware page reports the selected board's firmware-image contract and battery-backed state;
- added QDI board implementation notes;
- added `MOTHERBOARD-RUNTIME-BOUNDARY.md` documenting the exact non-fake seam required for a future 80486 core;
- added a Windows/PowerShell validation script that performs required-source checks and invokes `dotnet build` when available.

Definition of done for this refit:

- two physical motherboard profiles exist in the configurator;
- CPU/NPX/RAM/cache/clock/firmware/CMOS/slot context changes with motherboard selection;
- board-specific settings persist independently;
- the QDI OPTi 82C895/82C602 register model exists as real source;
- the proven 286 board remains the only executable board in this branch;
- the QDI board cannot be falsely powered using the NEAT memory bridge or 80286 core.

External validation still required because this assistant container has no .NET/VB build toolchain. Run `Motherboard Backplane Refit\VALIDATE-MOTHERBOARD-BACKPLANE.ps1` on the Windows source tree, then perform the NEAT boot/configurator smoke described by the script.
