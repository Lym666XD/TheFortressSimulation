@echo off
echo Testing fortress entry...
echo.
echo This test simulates: N (new world) - Select location - Enter to embark - Set fortress size - Enter to start
echo.
cd /d "%~dp0"
dotnet run --project src\HumanFortress.App\HumanFortress.App.csproj -c Release -- --test-crash
echo.
echo Check fortress_crash_test.log for details
pause
