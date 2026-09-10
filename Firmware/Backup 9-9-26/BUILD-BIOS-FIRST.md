# Build the BIOS before compiling/running VMB

`Firmware/atbios.asm` is newer than the pre-refit binary ROM retained in the backup folder.
The active `atbios.rom` and `atbios.lst` are intentionally generated artifacts.

From the repository root on Windows:

```powershell
.\Firmware\build-bios.ps1
```

The script validates the 64 KiB ROM layout, reset vector, and exact embedded IBM BASIC image before publishing `atbios.rom` and `atbios.lst`.

Only after that succeeds should the VB.NET project be built/run.
