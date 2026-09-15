#!/usr/bin/env bash

set -euo pipefail

candidate_dir="${1:?candidate directory is required}"
baseline_dir="${2:?baseline directory is required}"
report_path="${3:?report path is required}"

mkdir -p "$(dirname "$report_path")"
changed=()
encoding_only=()
diff_dir="$(dirname "$report_path")/diffs"

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
      differing_pixels="$(compare -metric AE "$baseline" "$candidate" null: 2>&1 || true)"
      if [[ ! "$differing_pixels" =~ ^[0-9]+$ ]]; then
        printf 'Could not compare decoded pixels for %s: %s\n' "$name" "$differing_pixels" >&2
        exit 1
      fi

      if [ "$differing_pixels" -eq 0 ]; then
        encoding_only+=("$name")
      else
        mkdir -p "$diff_dir"
        diff_path="$diff_dir/${name%.png}-diff.png"
        compare "$baseline" "$candidate" "$diff_path" 2>/dev/null || true
        if [ ! -f "$diff_path" ]; then
          printf 'Could not create visual diff for %s\n' "$name" >&2
          exit 1
        fi

        changed+=("$name (changed: $differing_pixels pixels)")
      fi
    fi
  done

  if [ "${#changed[@]}" -eq 0 ]; then
    echo "All checked-in screenshots are current."
  else
    echo "The following candidate images need review:"
    echo
    for item in "${changed[@]}"; do echo "- $item"; done
  fi

  if [ "${#encoding_only[@]}" -gt 0 ]; then
    echo
    echo "The following candidates differ only in PNG encoding and need no review:"
    echo
    for name in "${encoding_only[@]}"; do echo "- $name"; done
  fi
} > "$report_path"

printf '%s\n' "${changed[@]}" > "${report_path%.md}.txt"
