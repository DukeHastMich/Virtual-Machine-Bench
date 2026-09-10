# Motherboard Backplane Refit — Checkpoint 05

Date: 2026-09-09
Status: board-owned configuration/persistence boundary complete; 286 runtime remains unchanged.

Completed:

- added per-motherboard persistent context sections (`Motherboard.<profile-id>`) for RAM population, L2-cache population and clock/jumper selection;
- preserved the old `[Machine] MemoryMB` key as a NEAT-only migration/compatibility path, never as a QDI default;
- QDI default context is now 16 MB RAM, 256 KB L2 and the manual's 33 MHz JP38/JP39 default; NEAT retains its own settings;
- motherboard profiles now own firmware image path, CMOS/NVRAM path and CMOS capacity;
- QDI expects its own `Firmware\qdi-v4p895p3-smt-v5.rom` image and its own NVRAM file instead of sharing the 286 BIOS/CMOS;
- extended `CmosRtc` from a fixed 128-byte store to an optional 128/256-byte device with a bank-select callback; the unchanged NEAT constructor still creates exactly 128 bytes;
- the 82C602's F9h upper-CMOS bit can therefore drive a real second 128-byte bank when the QDI fabric is attached later;
- added contextual motherboard clock and cache selectors to System Configuration;
- motherboard changes restore that board's saved/default RAM/cache/clock context rather than inheriting the previous board's physical settings;
- added motherboard-profile ISA connector compatibility counting and an apply-time gate; the existing Diamond/SB16/NE2000 population is treated as three 16-bit ISA cards and fits the QDI board's six 16-bit slots;
- System Configuration now reports per-board firmware path/presence, NVRAM capacity/path and installed-card slot fit.

Activation boundary:

The QDI board remains deliberately power-on gated because no 80486 execution core/common non-NEAT memory bridge exists yet. Its silicon, physical profile and persistent motherboard context are real and separate; no NEAT device is used as a substitute.
