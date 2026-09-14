#!/usr/bin/env bash

set -euo pipefail

candidate_dir="${1:?candidate directory is required}"
baseline_dir="${2:?baseline directory is required}"
report_path="${3:?report path is required}"

mkdir -p "$(dirname "$report_path")"
changed=()

{
  echo "# Documentation screenshot freshness"
  echo
  for candidate in "$candidate_dir"/*.png; do
    [ -e "$candidate" ] || continue
    name="$(basename "$candidate")"
    baseline="$baseline_dir/$name"
    if [ ! -f "$baseline" ]; then
      changed+=("$name (new candidate)")
    elif ! cmp -s "$candidate" "$baseline"; then
      changed+=("$name (changed)")
    fi
  done

  if [ "${#changed[@]}" -eq 0 ]; then
    echo "All checked-in screenshots are current."
  else
    echo "The following candidate images need review:"
    echo
    for item in "${changed[@]}"; do echo "- $item"; done
  fi
} > "$report_path"

printf '%s\n' "${changed[@]}" > "${report_path%.md}.txt"
