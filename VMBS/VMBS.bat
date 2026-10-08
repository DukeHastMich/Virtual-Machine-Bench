@echo off
setlocal
cd /d "%~dp0"
where py >nul 2>nul
if %errorlevel%==0 (
  py -3 vmbs_launcher.py
  goto :done
)
where python >nul 2>nul
if %errorlevel%==0 (
  python vmbs_launcher.py
  goto :done
)
echo.
echo ERROR: Python 3 was not found.
echo Install Python 3 and enable the Python launcher or add python.exe to PATH.
echo.
pause
exit /b 1
:done
if not %errorlevel%==0 pause
endlocal
