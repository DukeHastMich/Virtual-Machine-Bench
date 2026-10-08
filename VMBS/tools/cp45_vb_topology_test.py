#!/usr/bin/env python3
from pathlib import Path
R=Path(__file__).resolve().parents[1]; s=(R/'generated_vmb/cp45_topology/GeneratedBoard.Topology.vb').read_text()
assert 'NetCount As Integer = 56' in s
assert 'runtime.Link' not in s and 'AllocateModule' not in s
assert '"/BD7"' in s and '"MICROWIRE_3WIRE"' in s and '"BOOT_ROM"' in s
assert s.count('New Net(')>=56
print('CP45 VB backend topology emission PASS')
