# S3 86C928 implementation notes

Primary reference preserved with the project:

- `HardwareDocs/S3_86C928_GUI_Accelerator.pdf`
- Source: https://www.ardent-tool.com/datasheets/S3_86C928.pdf
- S3 Incorporated, *86C928 GUI Accelerator*, September 1992 databook.

## Compatibility audit - 2026-09-01

The goal is guest-visible software compatibility and hardware-faithful register behavior. CRT phosphor and analog effects are deliberately outside the current scope.

Confirmed implemented command types from CMD bits 15-13:

- `000`: NOP / short-stroke setup
- `001`: line draw
- `010`: rectangle fill and CPU image transfer when WAIT is set
- `110`: BitBLT
- `111`: pattern fill

CMD types `011`, `100`, and `101` are reserved by the databook. They are not missing drawing operations and must not be assigned invented behavior.

Compatibility repairs completed on 2026-09-01:

1. `MULT_MISC` bits 3-0 now select independent source and destination base-address megabytes, allowing engine surfaces in every installed MiB of VRAM.
2. `MULT_MISC.RSF` bit 4 now selects the lower or upper half of 32-bit color, mask, and compare registers in 32-bpp mode and toggles after each access.
3. Memory-mapped I/O is decoded when either CR53 bit 4 or ADVFUNC bit 5 is set. A0000-A7FFF aliases the pixel-transfer stream and the documented A8xxx-ABxxx locations write their enhanced registers. MMIO takes priority over the VGA aperture while enabled, and BEE8's read-register-select has no invented memory alias.

Still requiring driver-facing regression coverage or primary board evidence:

1. Engine reset through `SUBSYS_CNTL.GE_RST` must preserve the documented enable/reset behavior and status/FIFO transitions.
2. `PIX_CNTL` packed image-read and data-extension selection, FIFO status, read-data availability, and byte-swap behavior require bus-level regression checks.
3. The ICD2061A clock-generator serial interface remains undocumented at the Diamond board-wiring level. Do not invent a programming protocol until a board schematic or verified driver trace establishes the wiring and word format.

Already corrected:

- VGA/S3 graphics scanout now advances memory using the CRTC row-scan counter rather than one VRAM row per physical scanline.
- Mode 13h produces a 320x400 physical scanout from 200 distinct source rows with CR09 repetition.
- Card-to-monitor output now includes explicit scanout timing metadata while keeping CRT presentation lightweight.

## Databook locations used

- Section 10, pp. 10-1 through 10-20: enhanced command registers.
- CMD, pp. 10-8 through 10-10: command types and transfer controls.
- GP_STAT, p. 10-8: FIFO occupancy, read-data available, busy, and all-empty bits.
- Color/mask registers, pp. 10-11 through 10-13: 32-bit RSF access behavior.
- PIX_CNTL and MULT_MISC, pp. 10-17 through 10-18: data extension, clipping, source/destination base address, RSF, and color compare.
- Section 11: programming sequences and FIFO polling expectations.
- Section 3.4.3 and Table 3-1, pp. 3-3 through 3-4: MMIO enables, pixel-transfer aperture, and enhanced-register memory aliases.
