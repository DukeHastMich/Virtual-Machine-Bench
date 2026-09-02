param(
    [string]$Source = (Join-Path $PSScriptRoot 'atbios_phase1.asm'),
    [string]$Output = (Join-Path $PSScriptRoot 'atbios.rom'),
    [string]$Listing = (Join-Path $PSScriptRoot 'atbios.lst')
)

$ErrorActionPreference = 'Stop'
$temporaryRom = [IO.Path]::ChangeExtension($Output, '.new.rom')
$temporaryListing = [IO.Path]::ChangeExtension($Listing, '.new.lst')

$localNasm = Join-Path $PSScriptRoot 'nasm.exe'
if (Test-Path $localNasm) {
    $nasm = $localNasm
} else {
    $nasmCommand = Get-Command nasm.exe -ErrorAction SilentlyContinue
    if (-not $nasmCommand) {
        throw 'NASM was not found. Put nasm.exe beside this script or add it to PATH.'
    }
    $nasm = $nasmCommand.Source
}

if (-not (Test-Path $Source)) {
    throw "BIOS source not found: $Source"
}

Remove-Item $temporaryRom, $temporaryListing -Force -ErrorAction SilentlyContinue
& $nasm -f bin -O2 -Wall -o $temporaryRom -l $temporaryListing $Source
if ($LASTEXITCODE -ne 0) {
    throw "NASM failed with exit code $LASTEXITCODE. The existing ROM was not replaced."
}

$bytes = [IO.File]::ReadAllBytes($temporaryRom)
if ($bytes.Length -ne 65536) {
    throw "ROM is $($bytes.Length) bytes; expected exactly 65536 bytes."
}
if ($bytes[0xFFF0] -ne 0xEA) {
    throw 'The reset vector at FFF0h is not a far JMP (EAh).'
}

Move-Item $temporaryRom $Output -Force
Move-Item $temporaryListing $Listing -Force

Write-Host "BIOS built successfully"
Write-Host "ROM:     $Output"
Write-Host "Listing: $Listing"
Write-Host "Size:    $($bytes.Length) bytes"
