#!/usr/bin/env bash

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "Running backend tests..."
dotnet test "$ROOT_DIR/glovelly.sln" -m:1

echo
echo "Running frontend lint..."
npm --prefix "$ROOT_DIR/frontend/glovelly-web" run lint

echo
echo "Running frontend build..."
npm --prefix "$ROOT_DIR/frontend/glovelly-web" run build

echo
echo "Verification complete."
