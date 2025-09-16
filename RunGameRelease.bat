@echo off
echo ====================================
echo     HumanFortress - Release Build
echo     Phase A-D Implementation Complete
echo ====================================
echo.

cd /d "%~dp0src\HumanFortress.App\bin\Release\net8.0\win-x64"
echo Starting game from: %CD%
HumanFortress.App.exe
cd /d "%~dp0"