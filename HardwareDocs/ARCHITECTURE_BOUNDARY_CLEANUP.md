# Architecture boundary cleanup

Branch: `working-beta`  
Started: 2026-09-02

## Protected baseline

`working-beta` was created from `Working` commit `b6145c3`, after Windows 3.1
Standard Mode boot, IDE/ATAPI access, VGA/S3 scanout, serial mouse input, and
PC-speaker operation had all been demonstrated. Refactoring must preserve that
guest-visible baseline.

## Non-negotiable hardware boundary

- CPU memory and I/O transactions continue through the CPU/local/AT bus model.
- DMA remains a motherboard bus master and IRQ delivery remains through the PICs.
- Clocked hardware advances only from the coordinated machine timeline.
- Host video, audio, input and file handling may run asynchronously only after a
  defined device boundary; host workers must not mutate guest-visible hardware
  state or invent completion, IRQ, DMA, or bus activity.
- Performance changes must not replace hardware behavior with guest-memory or
  application-specific shortcuts.

## First cleanup pass

The active display path is `DiamondStealthPro928` -> machine-boundary snapshot ->
`DiamondStealthPro928PresentationWorker` -> `CrtPresenter` -> WinForms image swap.

The following older WinForms rendering island was proven unreferenced and removed:

- `Mode2`, `Mode3`, and `Mode4` form timers and handlers;
- `CGAcard.vb`, `ScreenModes.vb`, and `SystemRoms.vb`;
- the `CgaController` compatibility alias;
- obsolete coordinate VRAM, screen/text buffers, Graphics objects, CRT-mask
  images, text-background tiles, pixel bitmaps, character-image cache, and font
  decoder globals from `Declares.vb`;
- the unused `Initialize_Constants` routine.

`VrMem` was deliberately retained. It remains the configured legacy RAM mirror
for `NeatMemoryController286` and is not part of the removed video renderer.

Diagnostic viewer/report methods were moved without behavioral changes from
`Form1.vb` to `Form1.Diagnostics.vb`. This is an organizational seam only; a
later pass should replace direct global-device reads with immutable diagnostic
snapshots captured under the machine ownership gate.

## Next low-risk extraction targets

1. Move media-menu, drive-bay, and host file-picker orchestration out of the main
   form while retaining all guest media access through FDC or IDE/ATAPI hardware.
2. Encapsulate serial-mouse host capture in a host adapter whose only device-side
   output is movement/button input to the emulated serial mouse at a machine
   boundary.
3. Replace global hardware construction in `Declares.vb` with an explicit machine
   composition root after behavior tests cover reset, IRQ, DMA, and media paths.
4. Separate diagnostic text formatting from hardware state by copying bounded
   snapshots at the coordinated boundary and formatting them off-thread.

Do not combine these ownership changes with CPU timing or instruction-decoder
changes. Keeping those revisions separate makes regressions attributable and
preserves the known-good Windows 3.1 milestone.
