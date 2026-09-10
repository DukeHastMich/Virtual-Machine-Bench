param(
    [string]$Source = (Join-Path $PSScriptRoot 'atbios.asm'),
    [string]$BasicRom = (Join-Path $PSScriptRoot 'basic110.rom'),
    [string]$Output = (Join-Path $PSScriptRoot 'atbios.rom'),
    [string]$Listing = (Join-Path $PSScriptRoot 'atbios.lst')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
function Get-RomSha256([string]$Path) {
    $stream = [IO.File]::OpenRead($Path)
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '').ToLowerInvariant() }
    finally { $sha.Dispose(); $stream.Dispose() }
}

$temporaryRom = [IO.Path]::ChangeExtension($Output, '.new.rom')
$temporaryListing = [IO.Path]::ChangeExtension($Listing, '.new.lst')

$localNasm = Join-Path $PSScriptRoot 'nasm.exe'
if (Test-Path -LiteralPath $localNasm) {
    $nasm = $localNasm
} else {
    $nasmCommand = Get-Command nasm.exe -ErrorAction SilentlyContinue
    if (-not $nasmCommand) {
        $nasmCommand = Get-Command nasm -ErrorAction SilentlyContinue
    }
    if (-not $nasmCommand) {
        throw 'NASM was not found. Put nasm.exe beside this script or add NASM to PATH.'
    }
    $nasm = $nasmCommand.Source
}

if (-not (Test-Path -LiteralPath $Source)) {
    throw "BIOS source not found: $Source"
}
$sourceFull = (Resolve-Path -LiteralPath $Source).Path
if (-not (Test-Path -LiteralPath $BasicRom)) {
    throw "IBM BASIC ROM not found: $BasicRom"
}

$basicBytes = [IO.File]::ReadAllBytes($BasicRom)
if ($basicBytes.Length -ne 32768) {
    throw "IBM BASIC ROM is $($basicBytes.Length) bytes; expected exactly 32768 bytes."
}
$basicSha = (Get-RomSha256 $BasicRom)
$expectedBasicSha = '3033d1a54c99d7e2aa1fc7c8c2e51a56ae1b61bf4e70e8aa580f43c43e37a63e'
if ($basicSha -ne $expectedBasicSha) {
    throw "IBM BASIC ROM SHA-256 mismatch. Expected $expectedBasicSha, got $basicSha. Refusing to build an unknown ROM image."
}

Remove-Item -LiteralPath $temporaryRom, $temporaryListing -Force -ErrorAction SilentlyContinue

# INCbin paths are resolved relative to the assembler working directory. Build
# from Firmware so `incbin "basic110.rom"` is deterministic whether invoked from
# Visual Studio, Codex, Explorer, or another shell.
Push-Location $PSScriptRoot
try {
    & $nasm -f bin -O2 -Wall -o $temporaryRom -l $temporaryListing $sourceFull
    if ($LASTEXITCODE -ne 0) {
        throw "NASM failed with exit code $LASTEXITCODE. Existing atbios.rom/atbios.lst were not replaced."
    }
} finally {
    Pop-Location
}

$bytes = [IO.File]::ReadAllBytes($temporaryRom)
if ($bytes.Length -ne 65536) {
    throw "ROM is $($bytes.Length) bytes; expected exactly 65536 bytes."
}
if ($bytes[0xFFF0] -ne 0xEA) {
    throw 'The reset vector at FFF0h is not a far JMP (EAh).'
}

# IBM Cassette BASIC must occupy its original physical F6000h-FDFFFh window,
# which is offsets 6000h-DFFFh inside the system BIOS image mapped at F0000h.
for ($i = 0; $i -lt $basicBytes.Length; $i++) {
    if ($bytes[0x6000 + $i] -ne $basicBytes[$i]) {
        throw ('BASIC ROM embedding mismatch at system-ROM offset {0:X4}h.' -f (0x6000 + $i))
    }
}

Move-Item -LiteralPath $temporaryRom -Destination $Output -Force
Move-Item -LiteralPath $temporaryListing -Destination $Listing -Force

$romSha = (Get-RomSha256 $Output)
Write-Host 'BIOS built and validated successfully'
Write-Host "ROM:       $Output"
Write-Host "Listing:   $Listing"
Write-Host "Size:      $($bytes.Length) bytes"
Write-Host "ROM SHA256: $romSha"
Write-Host "BASIC:     F6000h-FDFFFh / SHA256 $basicSha"
