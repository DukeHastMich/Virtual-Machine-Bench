# Front-panel drive bay notes

The drive faces are host presentation and media-workflow controls. They never
inject sectors, modify guest memory, or bypass the emulated FDC/IDE paths.

## Floppy bays

- Each fitted A:/B: bay shows the TEAC 3.5-inch face with no medium mounted.
- Media with 40 or fewer cylinders, or 15 sectors per track, selects the supplied
  5.25-inch face. This covers the normal 360 KB and 1.2 MB formats.
- Other supported floppy geometries retain the 3.5-inch face.
- Clicking a fitted floppy face opens the same per-drive media list as its Media
  menu entry: Disk Box images, physical floppy attachments, browse, eject, and
  boot actions. It does not open the broader Sneaker Net creation workbench.
- The supplied blank plate is loaded with the face set and reserved for a future
  genuinely absent-drive chassis configuration. An empty medium is not the same
  hardware state as an absent drive.

Artwork is deployed from `Resources/System Images` by the project file.

## Optical bay

The optical bay awaits dedicated period-correct artwork. When added, clicking it
should open the existing Optical/Disc Box workflow; mount and eject operations
must continue through the emulated IDE/ATAPI device.
