# Motherboard Backplane Refit — Checkpoint 04

Date: 2026-09-09
Status: OPTi register-level silicon milestone; QDI whole-machine activation remains deliberately gated.

Completed:

- added `Opti895Chipset486`, with the documented shared index port 22h, IPC data port 23h, and main data port 24h;
- modeled decoded 82C895 control indexes 20h-2Fh (2Ch explicitly not decoded) and power-management indexes E0h-EFh;
- installed documented reset values, including IPC index 01h = C0h;
- modeled the 82C602 F7h-FAh write-only companion register bank and its upper-CMOS-bank select bit;
- kept write-only 82C602 state from becoming guest-readable host magic;
- normalized documented reserved bits on key 82C895 writes rather than accepting impossible register states;
- added host diagnostics for L2-cache encoding, fast Gate A20, global cache, AT-clock selection and 82C602 bank state;
- added a reset-default validator invoked whenever the QDI runtime object is created;
- replaced the CP03 `Pending` runtime object with a real QDI runtime object carrying the actual OPTi silicon model.

Intentional gate:

The QDI runtime still reports `FixedFabricAvailable = False`. The existing machine's shared AT peripherals and CMOS are currently constructed as globals inside the NEAT-oriented `Declares` graph. Registering the OPTi chipset on top of that would create a mislabeled hybrid motherboard. CP05 separates motherboard-owned persistent context and fixed-fabric composition before any QDI board is allowed to become the live machine.

Processor execution remains unavailable because no 80486 core exists in this branch. No 80286 substitution is permitted.
