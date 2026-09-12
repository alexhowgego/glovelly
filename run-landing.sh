#!/usr/bin/env bash

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LANDING_DIR="$ROOT_DIR/frontend/glovelly-landing"
NPM_CACHE_DIR="${TMPDIR:-/tmp}/glovelly-npm-cache"

if ! command -v npm >/dev/null 2>&1; then
  echo "npm is required but was not found on PATH."
  exit 1
fi

if [[ ! -f "$LANDING_DIR/package.json" ]]; then
  echo "Glovelly landing site was not found at $LANDING_DIR."
  exit 1
fi

echo "Installing Glovelly landing dependencies..."
npm --prefix "$LANDING_DIR" --cache "$NPM_CACHE_DIR" ci

echo "Starting Glovelly landing site on http://localhost:4321 ..."
echo
echo "Press Ctrl+C to stop the landing site."

exec npm --prefix "$LANDING_DIR" run dev -- --host 0.0.0.0
