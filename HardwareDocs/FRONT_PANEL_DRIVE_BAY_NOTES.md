# Front-panel drive bay notes

The drive faces are host presentation and media-workflow controls. They never
inject sectors, modify guest memory, or bypass the emulated FDC/IDE paths.

## Chassis layout

The left front-panel rail is now treated as a physical stack instead of two
floating controls:

1. Floppy A
2. Floppy B
3. Fitted ATAPI CD-ROM
4. Blank 5.25-inch bay reserved for a future IDE device
5. Fitted primary-master hard disk with an MFM/RLL-era fixed-disk facade
6. Blank 5.25-inch bay reserved for a future IDE device

The fitted optical and hard-disk faces are derived at run time from the supplied
blank 5.25-inch plate so they stay visually consistent with the chassis artwork.
Blank bays are deliberately inert; they do not advertise hardware the guest
cannot actually see.

## Floppy bays

- Each fitted A:/B: bay shows the TEAC 3.5-inch face with no medium mounted.
- Media with 40 or fewer cylinders, or 15 sectors per track, selects the supplied
  5.25-inch face. This covers the normal 360 KB and 1.2 MB formats.
- Other supported floppy geometries retain the 3.5-inch face.
- Clicking a fitted floppy face opens the same per-drive media list as its Media
  menu entry: Disk Box images, physical floppy attachments, browse, eject, and
  boot actions. It does not open the broader Sneaker Net creation workbench.

Artwork is deployed from `Resources/System Images` by the project file.

## Optical bay

Clicking the fitted ATAPI face opens a chassis-local menu with current media
state, the existing ISO mount/eject actions, and a shortcut into Sneaker Net's
Optical/Disc Box page. The shortcut selects the Optical page directly instead of
opening the workbench on an unrelated tab.

All mount/eject operations still pass through the emulated IDE/ATAPI device.

## Hard-disk bay

Clicking the fitted fixed-disk facade opens:

- the live IDE Drive Shelf selector,
- the existing mount/create/boot/disconnect operations,
- the IDE-Drives folder,
- and a `Hard Drive Utilities...` shortcut into Sneaker Net's Hard Drives page.

The Hard Drives workbench already owns inspect, attach/disconnect, clone, backup,
shelf browsing and variable-size image creation. Opening it from the facade also
selects the currently attached shelf drive when possible, so those utilities are
immediately aimed at the physical bay the operator clicked.

The facade remains presentation only. ATA attachment and guest access still pass
through the primary-master controller path.
