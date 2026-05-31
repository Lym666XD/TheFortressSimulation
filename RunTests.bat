@echo off
echo Running HumanFortress Tests...
echo.

cd /d "%~dp0"
dotnet run --project src\HumanFortress.App\HumanFortress.App.csproj -- --test

echo.
pause
