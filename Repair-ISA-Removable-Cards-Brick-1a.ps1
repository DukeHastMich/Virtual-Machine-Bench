param(
    [string]$ProjectRoot = (Get-Location).Path,
    [switch]$Build
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Say([string]$Text, [ConsoleColor]$Color = [ConsoleColor]::Gray) {
    Write-Host $Text -ForegroundColor $Color
}

$root = [IO.Path]::GetFullPath($ProjectRoot)
$project = Join-Path $root 'VirtualComputer.Modern.vbproj'
$backplane = Join-Path $root 'IsaBackplane.vb'
$wavetableRoot = Join-Path $root 'Wavetable'

Say ''
Say 'ISA Removable Cards - Brick 1a project-file repair' Cyan
Say ('Project root : ' + $root) DarkCyan
Say ''

if (-not (Test-Path -LiteralPath $project -PathType Leaf)) {
    throw "VirtualComputer.Modern.vbproj was not found in '$root'."
}
if (-not (Test-Path -LiteralPath $backplane -PathType Leaf)) {
    throw "IsaBackplane.vb is not present. Brick 1 does not appear to be installed in this tree. No source was changed."
}
if (-not (Test-Path -LiteralPath $wavetableRoot -PathType Container)) {
    throw "The existing Wavetable source directory was not found at '$wavetableRoot'. No source was changed."
}

$projectBytes = [IO.File]::ReadAllBytes($project)
$hadUtf8Bom = $projectBytes.Length -ge 3 -and
              $projectBytes[0] -eq 0xEF -and
              $projectBytes[1] -eq 0xBB -and
              $projectBytes[2] -eq 0xBF
$text = [IO.File]::ReadAllText($project)

if ($text -notmatch '(?is)<EnableDefaultCompileItems>\s*false\s*</EnableDefaultCompileItems>') {
    throw 'This repair is only for the Brick-1 project shape with EnableDefaultCompileItems=false. No source was changed.'
}

# Make sure the file is valid XML before touching it.
try { [void]([xml]$text) }
catch { throw ('The project file is not valid XML before repair: ' + $_.Exception.Message) }

$existing = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($match in [regex]::Matches($text, '(?is)<Compile\s+Include\s*=\s*"([^"]+)"')) {
    [void]$existing.Add($match.Groups[1].Value.Replace('/', '\'))
}

$rootPrefix = $root.TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar
$wanted = [System.Collections.Generic.List[string]]::new()

# Brick 1 itself must stay compiled.
$wanted.Add('IsaBackplane.vb')

# Restore the pre-existing modular wavetable build inputs that Brick 1's stale
# project-file baseline accidentally omitted. No wavetable implementation file
# is edited or replaced; this only restores Compile items for files already on disk.
$wavetableFiles = @(Get-ChildItem -LiteralPath $wavetableRoot -Recurse -File -Filter '*.vb' | Sort-Object FullName)
if ($wavetableFiles.Count -eq 0) {
    throw "The Wavetable directory contains no .vb source files. No source was changed."
}
foreach ($file in $wavetableFiles) {
    if (-not $file.FullName.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw ('Unexpected Wavetable source path outside project root: ' + $file.FullName)
    }
    $relative = $file.FullName.Substring($rootPrefix.Length).Replace('/', '\')
    $wanted.Add($relative)
}

$missing = [System.Collections.Generic.List[string]]::new()
foreach ($relative in $wanted) {
    if (-not $existing.Contains($relative)) {
        $missing.Add($relative)
    }
}

if ($missing.Count -eq 0) {
    Say 'Project compile inputs are already repaired; no file was changed.' Green
    if ($Build) {
        $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
        if ($null -eq $dotnet) { throw 'Build requested, but dotnet was not found on PATH.' }
        & $dotnet.Source build $project --nologo
        if ($LASTEXITCODE -ne 0) { throw "dotnet build failed with exit code $LASTEXITCODE." }
    }
    exit 0
}

$nl = if ($text.Contains("`r`n")) { "`r`n" } else { "`n" }
$block = '  <!-- ISA Removable Cards Brick 1a: restore existing Wavetable compile inputs -->' + $nl
$block += '  <ItemGroup>' + $nl
foreach ($relative in $missing) {
    $escaped = [Security.SecurityElement]::Escape($relative)
    $block += '    <Compile Include="' + $escaped + '" />' + $nl
}
$block += '  </ItemGroup>' + $nl

$closingMatches = [regex]::Matches($text, '(?i)</Project>')
if ($closingMatches.Count -ne 1) {
    throw "Expected exactly one </Project> closing tag; found $($closingMatches.Count). No source was changed."
}
$closing = $closingMatches[0]
$patched = $text.Substring(0, $closing.Index) + $block + '</Project>' + $text.Substring($closing.Index + $closing.Length)

# Validate the complete patched XML before writing it.
try { [void]([xml]$patched) }
catch { throw ('Generated project XML failed validation before write: ' + $_.Exception.Message) }

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$backup = $project + '.brick1a-' + $stamp + '.bak'
Copy-Item -LiteralPath $project -Destination $backup -Force

$encoding = [Text.UTF8Encoding]::new($hadUtf8Bom)
try {
    [IO.File]::WriteAllText($project, $patched, $encoding)

    # Post-write verification: parse again and confirm every intended Compile item exists.
    $written = [IO.File]::ReadAllText($project)
    [void]([xml]$written)
    $writtenIncludes = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($match in [regex]::Matches($written, '(?is)<Compile\s+Include\s*=\s*"([^"]+)"')) {
        [void]$writtenIncludes.Add($match.Groups[1].Value.Replace('/', '\'))
    }
    foreach ($relative in $wanted) {
        if (-not $writtenIncludes.Contains($relative)) {
            throw "Post-write verification failed: Compile Include='$relative' is missing."
        }
    }
}
catch {
    Copy-Item -LiteralPath $backup -Destination $project -Force
    throw ('Repair failed; original project file was restored from backup. ' + $_.Exception.Message)
}

Say ('Backup      : ' + $backup) DarkGray
Say ('Restored ' + $missing.Count + ' project Compile item(s):') Green
foreach ($relative in $missing) { Say ('  + ' + $relative) Gray }
Say ''
Say 'No .vb implementation file, device wiring, UI, card population, IRQ/DMA behavior, or chassis configuration was changed.' Green

if ($Build) {
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($null -eq $dotnet) {
        throw 'Project repair succeeded, but Build was requested and dotnet was not found on PATH.'
    }
    Say ''
    Say '[BUILD] dotnet build' Cyan
    & $dotnet.Source build $project --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "Project repair succeeded, but dotnet build failed with exit code $LASTEXITCODE. The project-file backup was left in place."
    }
    Say 'Build passed.' Green
}
else {
    Say ''
    Say 'Now reload the project in Visual Studio if prompted, then Rebuild Solution.' Cyan
}
