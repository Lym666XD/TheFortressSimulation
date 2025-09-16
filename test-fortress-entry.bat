@echo off
echo Testing fortress entry...
echo.
echo This test simulates: N (new world) - Select location - Enter to embark - Set fortress size - Enter to start
echo.
.\src\HumanFortress.App\bin\Release\net8.0\win-x64\HumanFortress.App.exe --test-crash
echo.
echo Check fortress_crash_test.log for details
pause