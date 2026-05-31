@echo off
echo ====================================
echo     HumanFortress - Release Build
echo     Phase A-D Implementation Complete
echo ====================================
echo.

cd /d "%~dp0src\HumanFortress.App\bin\Release\net8.0\win-x64"
if exist HumanFortress.App.exe (
    echo Starting game from: %CD%
    HumanFortress.App.exe
) else (
    cd /d "%~dp0"
    dotnet run --project src\HumanFortress.App\HumanFortress.App.csproj -c Release
)
cd /d "%~dp0"
