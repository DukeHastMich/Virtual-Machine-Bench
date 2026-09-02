# Virtual Machine Bench

Virtual Machine Bench is a hardware-oriented IBM AT-class emulator written in VB.NET. The project models a late 80286-era system as a collection of devices connected through the machine's buses, with an emphasis on guest-visible hardware behavior rather than application-level shortcuts.

The current milestone boots MS-DOS and runs Microsoft Windows 3.1 in Standard Mode on the emulated Diamond Stealth Pro / S3 86C928 video hardware.

![Microsoft Windows 3.1 booting on Virtual Machine Bench](docs/screenshots/windows-31-boot.png)

## Current hardware

- Harris Semiconductor CS80C286-class processor core
- Intel 80287 numeric coprocessor
- C&T CS8221 NEAT-class AT chipset and motherboard devices
- Diamond Stealth Pro ISA video card with S3 86C928 and 2 MiB VRAM
- VGA-compatible firmware, register interface, memory aperture, DAC, and scanout
- IDE hard disks and ATAPI CD-ROM support
- Floppy controller and removable media
- 101-key AT keyboard and serial mouse path
- Sound Blaster 16-compatible ISA audio
- Novell NE2000-compatible ISA network adapter
- Epson FX-compatible virtual printer and PC speaker

Hardware configuration, I/O decoding, DMA, interrupt routing, and guest communication are intended to pass through the emulated buses and devices.

## Screenshots

### Windows 3.1 Setup

![Windows 3.1 Setup running inside the emulator](docs/screenshots/windows-31-setup.png)

### Windows 3.1 Program Manager

![Windows 3.1 Program Manager and Virtual Machine Bench hardware controls](docs/screenshots/windows-31-desktop.png)

## Project status

This is active emulator development, not a finished general-purpose virtual machine. Windows 3.1 now installs, boots, and reaches Program Manager, making this revision the first repository baseline worth preserving.

Current work continues on performance, serial-mouse responsiveness, device completeness, and hardware-accurate edge cases. The final CRT presentation step deliberately uses an efficient host bitmap; guest software still communicates with the emulated video card through its hardware interfaces.

Implementation and debugging notes are kept in [`HardwareDocs`](HardwareDocs), including the Intel 80287, S3 86C928, and Windows 3.1 Standard Mode investigations.

## Building

Requirements:

- Windows
- Visual Studio 2022 or the .NET 8 SDK
- .NET 8 Windows Desktop runtime

Open `VirtualComputer.Modern.sln` in Visual Studio, or build from a Developer PowerShell prompt:

```powershell
dotnet build .\VirtualComputer.Modern.sln
```

The emulator is a Windows Forms application targeting `net8.0-windows` and x64. Required system and video ROM images are included in the project and copied to the output directory during the build.

## Branches

- `main` is the known-good Windows 3.1 milestone.
- `Working` is the active development branch.

## Historical note

This repository began as a long-running experimental emulator and contains implementation notes and hardware reference material accumulated during development. Some subsystems remain approximate and are being replaced incrementally with behavior grounded in original hardware documentation and observed guest software behavior.
