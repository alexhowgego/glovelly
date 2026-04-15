#!/usr/bin/env bash

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BACKEND_DIR="$ROOT_DIR/backend/Glovelly.Api"
FRONTEND_DIR="$ROOT_DIR/frontend/glovelly-web"
LOCAL_DEV_CONFIG_FILE="$ROOT_DIR/.glovelly.dev.local"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "dotnet is required but was not found on PATH."
  exit 1
fi

if ! command -v npm >/dev/null 2>&1; then
  echo "npm is required but was not found on PATH."
  exit 1
fi

if [[ -f "$LOCAL_DEV_CONFIG_FILE" ]]; then
  # shellcheck disable=SC1090
  source "$LOCAL_DEV_CONFIG_FILE"
fi

backend_pid=""
frontend_pid=""

terminate_process() {
  local pid="$1"

  if [[ -z "$pid" ]] || ! kill -0 "$pid" >/dev/null 2>&1; then
    return
  fi

  kill "$pid" >/dev/null 2>&1 || true

  for _ in {1..20}; do
    if ! kill -0 "$pid" >/dev/null 2>&1; then
      wait "$pid" 2>/dev/null || true
      return
    fi

    sleep 0.1
  done

  kill -9 "$pid" >/dev/null 2>&1 || true
  wait "$pid" 2>/dev/null || true
}

cleanup() {
  terminate_process "$backend_pid"
  terminate_process "$frontend_pid"
}

trap cleanup EXIT INT TERM

echo "Starting Glovelly API on http://localhost:5153 ..."
(
  cd "$BACKEND_DIR"
  exec dotnet run --urls http://localhost:5153
) &
backend_pid=$!

echo "Starting Glovelly web app on http://localhost:5173 ..."
(
  cd "$FRONTEND_DIR"
  exec npm run dev -- --host 0.0.0.0
) &
frontend_pid=$!

echo
echo "Glovelly dev environment is starting."
echo "Frontend: http://localhost:5173"
echo "Backend:  http://localhost:5153"
echo "Swagger:  http://localhost:5153/swagger"
if [[ -n "${DevelopmentSeeding__AdminGoogleSubject:-}" ]]; then
  echo "Local admin seeding: enabled for in-memory development database"
fi
echo
echo "Press Ctrl+C to stop both services."

wait "$backend_pid" "$frontend_pid"
