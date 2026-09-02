@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "ROOT=%~dp0"
set "SOURCE=%ROOT%atbios_phase1.asm"
set "OUTPUT=%ROOT%atbios.rom"
set "TEMPROM=%ROOT%atbios.new.rom"
set "LISTING=%ROOT%atbios.lst"
set "TEMPLIST=%ROOT%atbios.new.lst"

if exist "%ROOT%nasm.exe" (
    set "NASM=%ROOT%nasm.exe"
) else (
    set "NASM=nasm.exe"
    where nasm.exe >nul 2>nul
    if errorlevel 1 (
        echo.
        echo ERROR: NASM was not found.
        echo Put nasm.exe beside this script or add NASM to PATH.
        echo.
        exit /b 1
    )
)

if not exist "%SOURCE%" (
    echo ERROR: Source file not found: "%SOURCE%"
    exit /b 1
)

del /q "%TEMPROM%" "%TEMPLIST%" >nul 2>nul

echo Assembling %SOURCE%
"%NASM%" -f bin -O2 -Wall -o "%TEMPROM%" -l "%TEMPLIST%" "%SOURCE%"
if errorlevel 1 (
    echo.
    echo BIOS assembly failed. The existing atbios.rom was left untouched.
    exit /b 1
)

for %%I in ("%TEMPROM%") do set "ROMSIZE=%%~zI"
if not "!ROMSIZE!"=="65536" (
    echo.
    echo ERROR: ROM is !ROMSIZE! bytes; it must be exactly 65536 bytes.
    exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$b=[IO.File]::ReadAllBytes('%TEMPROM%'); if($b[0xFFF0] -ne 0xEA){Write-Error 'Reset vector is not a far JMP at FFF0h'; exit 1}"
if errorlevel 1 exit /b 1

move /y "%TEMPROM%" "%OUTPUT%" >nul
move /y "%TEMPLIST%" "%LISTING%" >nul

echo.
echo BIOS built successfully:
echo   ROM:     %OUTPUT%
echo   Listing: %LISTING%
echo   Size:    !ROMSIZE! bytes
echo.
echo Copy the ROM into the project's Firmware folder if this script is not
 echo already being run there, then rebuild the VB.NET project.
exit /b 0
