#!/usr/bin/env sh
set -eu

cd "$(dirname "$0")"
DOTNET="${DOTNET:-dotnet}"
RESULTS_DIR="${RESULTS_DIR:-artifacts/test-results/local}"

"$DOTNET" test tests/HumanFortress.App.Tests/HumanFortress.App.Tests.csproj \
  -m:1 \
  -v:minimal \
  -p:RunAnalyzers=false \
  -p:UseAppHost=false \
  --filter "TestCategory=discoverable" \
  --logger "trx;LogFileName=discoverable-suites.trx" \
  --results-directory "$RESULTS_DIR"
