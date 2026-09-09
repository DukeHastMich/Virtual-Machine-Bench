param([Parameter(Mandatory=$true)][string]$AssemblyPath)
$ErrorActionPreference = 'Stop'
$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path $AssemblyPath).Path)
$fpu = [Activator]::CreateInstance($assembly.GetType('WindowsApplication1.Intel80287', $true))
$checks = 0
function Assert-Equal($actual, $expected, [string]$label) {
    if ($actual -ne $expected) { throw "$label expected $expected, got $actual" }
    $script:checks++
}
# Intel 210498-005 (1987), Table 1-12, printed page 1-28.
# Check all sign combinations, operand orders, masks, and both infinity modes.
foreach ($affine in @($false,$true)) {
    foreach ($masked in @($false,$true)) {
        foreach ($left in @([double]::NegativeInfinity,-2.0,0.0,2.0,[double]::PositiveInfinity)) {
            foreach ($right in @([double]::NegativeInfinity,-2.0,0.0,2.0,[double]::PositiveInfinity)) {
                $fpu.Reset()
                $cw = 0x037F
                if ($affine) { $cw = $cw -bor 0x1000 }
                if (-not $masked) { $cw = $cw -band 0xFFFE }
                $fpu.ControlWord = $cw
                $null = $fpu.Push($left)
                $result = $fpu.CompareSt0With($right)
                $li = [double]::IsInfinity($left)
                $ri = [double]::IsInfinity($right)
                $invalid = (-not $affine) -and ($li -xor $ri)
                $label = "IC=$affine masked=$masked compare $left,$right"
                Assert-Equal $result ((-not $invalid) -or $masked) "$label completion"
                Assert-Equal (($fpu.StatusWord -band 1) -ne 0) $invalid "$label invalid"
                Assert-Equal $fpu.ErrorAsserted ($invalid -and -not $masked) "$label ERROR"
                if ($invalid) {
                    if ($masked) { Assert-Equal ($fpu.StatusWord -band 0x4500) 0x4500 "$label unordered" }
                } else {
                    $condition = if (($li -and $ri -and -not $affine) -or $left -eq $right) { 0x4000 } elseif ($left -lt $right) { 0x0100 } else { 0 }
                    Assert-Equal ($fpu.StatusWord -band 0x4500) $condition "$label condition"
                }
                Assert-Equal $fpu.Top 7 "$label no pop"
            }
        }
    }
}
foreach ($popCount in @(1,2)) {
    foreach ($masked in @($false,$true)) {
        $fpu.Reset()
        if (-not $masked) { $fpu.ControlWord = 0x037E }
        $null = $fpu.Push(1.0)
        $null = $fpu.Push([double]::PositiveInfinity)
        Assert-Equal ($fpu.CompareSt0WithSt(1,$popCount)) $masked 'invalid compare completion'
        Assert-Equal $fpu.Top $(if ($masked) { (6+$popCount) -band 7 } else { 6 }) 'exception pop behavior'
    }
}
# Execute the actual detection sequence found in the supplied SCORCH.EXE
# (MZ image offsets 032A-0343, 037C-0381; +infinity at 024B).
# This tests memory ESC dispatch, FCHS, FCOMP, FSTSW, SAHF, and the branch
# on the existing isolated SingleStep memory bed, without modifying the CPU.
$bed = [Activator]::CreateInstance($assembly.GetType('WindowsApplication1.Isolated286BedInBed', $true),[object[]]@($true))
$cpu = $bed.GetType().GetField('_cpuInBed',[Reflection.BindingFlags]'Instance,NonPublic').GetValue($bed)
$cpu.Reset()
$cpu.CS=0x2000; $cpu.DS=0x4000; $cpu.SS=0x5000; $cpu.SP=0xFF00; $cpu.IP=0x032A
function Write-Hex([uint32]$address,[string]$hex) {
    foreach ($part in $hex.Split(' ')) {
        $cpu.WriteByte($address,[Convert]::ToByte($part,16))
        $address++
    }
}
Write-Hex 0x2024B '00 00 80 7f'
Write-Hex 0x2032A 'db e3 2e d9 06 4b 02 d9 e0 2e d8 1e 4b 02 9b df e0 9e b0 02 74 3c fe c0 eb 38'
Write-Hex 0x2037C '98 93 89 1e b0 83'
for ($step=0; $step -lt 25 -and $cpu.IP -ne 0x382; $step++) { $cpu.pStep() }
Assert-Equal $cpu.IP 0x382 'game detection routine completion'
Assert-Equal ($cpu.ReadWord(0x483B0)) 2 'game detects 80287, not 80387'
# A successful detector must not be obtained by enabling newer opcodes.
$decode = $cpu.GetType().GetMethod('ExecuteFpuRegister',[Reflection.BindingFlags]'Instance,NonPublic')
foreach ($reg in @(3,6,7)) {
    $cpu.NumericCoprocessor.Reset()
    $null = $cpu.NumericCoprocessor.Push(0.5)
    $null = $decode.Invoke($cpu,@([byte]0xD9,[int]7,[int]$reg))
    Assert-Equal ($cpu.NumericCoprocessor.StatusWord -band 1) 1 '387 trig remains rejected'
    Assert-Equal $cpu.NumericCoprocessor.Top 7 'reserved trig does not push'
}
Write-Output "PASS: $checks checks; game detector reports 80287; FSINCOS/FSIN/FCOS remain reserved."
