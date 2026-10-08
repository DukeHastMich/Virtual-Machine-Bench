# VMBS Windows Launcher — CP27

Double-click `VMBS.bat` from the `VMBS` directory. It prefers the Windows `py -3` launcher and falls back to `python` on PATH.

The launcher is an operator shell over the existing VMBS tools; it does not duplicate compiler logic. The normal development path is:

`KiCad -> BS -> Analyzer -> Construction IR -> VMB Board Code -> .vmbcard -> VMB/Card Store`

Option 3 performs the full KiCad-to-card path. Option 6 validates a `.vmbcard` install header and copies it to the adjacent modular VMB `Card Store`. Option 9 checks Python, stores, Card Store discovery, and optional KiCad CLI visibility.

Current development limitation: the bundled component map/store knowledge is still centered on the ISA8 RTL8019 specimen. Unknown components remain unknown rather than being invented.
