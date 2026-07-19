@echo off
setlocal

cd /d "%~dp0"
if not defined DOTNET set "DOTNET=dotnet"
if not defined RESULTS_DIR set "RESULTS_DIR=artifacts\test-results\local"

"%DOTNET%" test tests\HumanFortress.App.Tests\HumanFortress.App.Tests.csproj ^
  -m:1 ^
  -v:minimal ^
  -p:RunAnalyzers=false ^
  -p:UseAppHost=false ^
  --filter "TestCategory=discoverable" ^
  --logger "trx;LogFileName=discoverable-suites.trx" ^
  --results-directory "%RESULTS_DIR%"
exit /b %errorlevel%
