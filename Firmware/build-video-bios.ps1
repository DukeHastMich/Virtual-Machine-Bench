param(
    [string]$Source = (Join-Path $PSScriptRoot 'stealthpro_vgabios.asm'),
    [string]$Output = (Join-Path $PSScriptRoot 'stealthpro.rom'),
    [string]$Listing = (Join-Path $PSScriptRoot 'stealthpro.lst')
)

$ErrorActionPreference = 'Stop'
$temporaryRom = [IO.Path]::ChangeExtension($Output, '.new.rom')
$temporaryListing = [IO.Path]::ChangeExtension($Listing, '.new.lst')
$nasmCommand = Get-Command nasm.exe -ErrorAction SilentlyContinue
if (-not $nasmCommand) { throw 'NASM was not found on PATH.' }

Remove-Item $temporaryRom, $temporaryListing -Force -ErrorAction SilentlyContinue
Push-Location $PSScriptRoot
try {
    & $nasmCommand.Source -f bin -O2 -Wall -o $temporaryRom -l $temporaryListing $Source
    if ($LASTEXITCODE -ne 0) { throw "NASM failed with exit code $LASTEXITCODE." }
} finally {
    Pop-Location
}

$bytes = [IO.File]::ReadAllBytes($temporaryRom)
if ($bytes.Length -ne 32768) { throw "Video ROM is $($bytes.Length) bytes; expected 32768." }
$bytes[$bytes.Length - 1] = 0
$sum = 0
foreach ($value in $bytes) { $sum = ($sum + $value) -band 0xFF }
$bytes[$bytes.Length - 1] = [byte]((- $sum) -band 0xFF)
[IO.File]::WriteAllBytes($temporaryRom, $bytes)
if ((($bytes | Measure-Object -Sum).Sum -band 0xFF) -ne 0) { throw 'Video ROM checksum patch failed.' }

Move-Item $temporaryRom $Output -Force
Move-Item $temporaryListing $Listing -Force
Write-Host "Video BIOS built: $Output ($($bytes.Length) bytes, checksum 00h)"
