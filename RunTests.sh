#!/usr/bin/env sh
set -eu

cd "$(dirname "$0")"
/opt/homebrew/opt/dotnet@8/bin/dotnet build tests/HumanFortress.App.Tests/HumanFortress.App.Tests.csproj --no-restore -m:1 -v:quiet -p:RunAnalyzers=false
/opt/homebrew/opt/dotnet@8/bin/dotnet tests/HumanFortress.App.Tests/bin/Debug/net8.0/HumanFortress.App.Tests.dll
