@echo off
echo Running HumanFortress Tests...
echo.

cd /d "%~dp0"
dotnet build tests\HumanFortress.App.Tests\HumanFortress.App.Tests.csproj --no-restore -m:1 -v:quiet -p:RunAnalyzers=false
if errorlevel 1 exit /b %errorlevel%
dotnet tests\HumanFortress.App.Tests\bin\Debug\net8.0\HumanFortress.App.Tests.dll

echo.
pause
