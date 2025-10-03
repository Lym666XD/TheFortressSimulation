@echo off
REM ========================================
REM HumanFortress Build Script
REM ========================================
REM This script builds and publishes the game to a fixed location
REM Output: publish\HumanFortress.App\HumanFortress.App.exe

cd /d %~dp0

echo.
echo ========================================
echo Building HumanFortress...
echo ========================================
echo.

dotnet publish src\HumanFortress.App\HumanFortress.App.csproj -c Release -r win-x64 --self-contained true -o publish\HumanFortress.App

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ========================================
    echo Build SUCCESS!
    echo ========================================
    echo.
    echo Executable location:
    echo %~dp0publish\HumanFortress.App\HumanFortress.App.exe
    echo.
) else (
    echo.
    echo ========================================
    echo Build FAILED!
    echo ========================================
    echo.
)

pause
