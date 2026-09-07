# Front-panel drive bay notes

The drive faces are host presentation and media-workflow controls. They never
inject sectors, modify guest memory, or bypass the emulated FDC/IDE paths.

## Chassis layout

The left front-panel rail is a compact six-position physical stack:

1. Floppy A
2. Floppy B
3. Primary-slave IDE bay
4. Secondary-slave IDE bay
5. Primary-master IDE bay
6. Secondary-master IDE bay

The four IDE positions are physical socket/bay representations, not hard-coded device types. System Configuration can fit each one with an ATA hard disk, an ATAPI CD-ROM, or nothing. After the required power cycle the facade changes to the MFM/RLL-style fixed-disk face, ATAPI tray face, or supplied blank 5.25-inch plate to match the configured hardware.

## Floppy bays

- Each fitted A:/B: bay shows the TEAC 3.5-inch face with no medium mounted.
- Media with 40 or fewer cylinders, or 15 sectors per track, selects the supplied
  5.25-inch face. This covers the normal 360 KB and 1.2 MB formats.
- Other supported floppy geometries retain the 3.5-inch face.
- Clicking a fitted floppy face opens the same per-drive media list as its Media
  menu entry: Disk Box images, physical floppy attachments, browse, eject, and
  boot actions.

Artwork is deployed from `Resources/System Images` by the project file.

## Optical bays

Any IDE bay configured as ATAPI renders the optical facade. Clicking it opens a local menu with that exact master/slave socket's media state, ISO mount/eject actions, and a shortcut into Sneaker Net's Optical/Disc Box page. All mount/eject operations pass through the corresponding emulated IDE/ATAPI channel and selected device position.

## Hard-disk bays

Any IDE bay configured as ATA renders the fixed-disk facade and opens the Drive Shelf for that exact master/slave socket plus mount/create/disconnect operations, the IDE-Drives folder, and Sneaker Net Hard Drive utilities. Only primary master exposes the current BIOS boot action.

The persistent Drive Shelf stores independent attachment IDs for PrimaryMaster, PrimarySlave, SecondaryMaster, and SecondarySlave. A writable shelf image cannot be attached to two sockets at the same time. Each raw image's adjacent `.meta` file is its physical HDD identity record and already feeds ATA IDENTIFY.

The facades remain presentation only. ATA attachment and guest access still pass through the emulated task-file controllers.

The four legacy HDD activity indicators now map one-for-one to IDE electrical positions when that position contains an ATA disk: HDD0=primary master, HDD1=primary slave, HDD2=secondary master, HDD3=secondary slave. ATAPI activity does not falsely light an HDD indicator.
