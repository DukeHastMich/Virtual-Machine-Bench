param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProjectRoot = [System.IO.Path]::GetFullPath($ProjectRoot)
$project = Join-Path $ProjectRoot 'VirtualComputer.Modern.vbproj'
if (-not (Test-Path -LiteralPath $project -PathType Leaf)) {
    throw "VirtualComputer.Modern.vbproj not found at $ProjectRoot"
}

$required = @(
    'Motherboards\MotherboardCatalog.vb',
    'Motherboards\MotherboardRuntime.vb',
    'Motherboards\QDI V4P895P3\Opti895Chipset486.vb',
    'MachineConfiguration.vb',
    'HardwarePopulation.vb',
    'SystemConfigurationDrawer.vb'
)
foreach ($relative in $required) {
    $path = Join-Path $ProjectRoot $relative
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing required source: $relative" }
    Write-Host "[OK] $relative"
}

$projectText = Get-Content -LiteralPath $project -Raw
if ($projectText -notmatch [regex]::Escape('Motherboards\QDI V4P895P3\Opti895Chipset486.vb')) {
    throw 'QDI OPTi chipset source is not in the VB project Compile list.'
}

$qdiRom = Join-Path $ProjectRoot 'Firmware\qdi-v4p895p3-smt-v5.rom'
if (Test-Path -LiteralPath $qdiRom -PathType Leaf) {
    $length = (Get-Item -LiteralPath $qdiRom).Length
    Write-Host "[INFO] QDI firmware image present: $length bytes"
} else {
    Write-Host '[INFO] QDI firmware image not present. This is allowed while QDI runtime activation remains gated.' -ForegroundColor Yellow
}

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if ($null -eq $dotnet) {
    Write-Host '[SKIP] dotnet not found; source checks complete but build not run.' -ForegroundColor Yellow
    exit 2
}

Write-Host ''
Write-Host '[BUILD] dotnet build' -ForegroundColor Cyan
& dotnet build $project
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed with exit code $LASTEXITCODE" }

Write-Host ''
Write-Host 'Motherboard backplane validation build passed.' -ForegroundColor Green
Write-Host 'Runtime smoke: boot the NEAT board, open System Configuration, stage QDI, inspect CPU/RAM/cache/clock/slots, then Revert without applying the gated board.'
