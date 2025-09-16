@echo off
echo Running HumanFortress Tests...
echo.

cd game
HumanFortress.App.exe --test
cd ..

echo.
pause