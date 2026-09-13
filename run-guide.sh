#!/usr/bin/env bash

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
GUIDE_DIR="$ROOT_DIR/frontend/glovelly-guide"
NPM_CACHE_DIR="${TMPDIR:-/tmp}/glovelly-npm-cache"

if ! command -v npm >/dev/null 2>&1; then
  echo "npm is required but was not found on PATH."
  exit 1
fi

if [[ ! -f "$GUIDE_DIR/package.json" ]]; then
  echo "Glovelly user guide was not found at $GUIDE_DIR."
  exit 1
fi

echo "Installing Glovelly user guide dependencies..."
npm --prefix "$GUIDE_DIR" --cache "$NPM_CACHE_DIR" ci

echo "Starting Glovelly user guide on http://localhost:4321 ..."
echo
echo "Press Ctrl+C to stop the user guide."

exec npm --prefix "$GUIDE_DIR" run dev -- --host 0.0.0.0
