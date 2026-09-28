#!/usr/bin/env bash

set -euo pipefail

candidate_dir="${1:?candidate directory is required}"
baseline_dir="${2:?baseline directory is required}"
report_path="${3:?report path is required}"
# Allow up to 0.05% changed pixels for harmless browser rasterisation drift.
max_differing_pixels_per_10000="${DOCUMENTATION_SCREENSHOT_MAX_DIFF_PER_10000:-5}"

if ! [[ "$max_differing_pixels_per_10000" =~ ^[0-9]+$ ]]; then
  printf 'DOCUMENTATION_SCREENSHOT_MAX_DIFF_PER_10000 must be a whole number\n' >&2
  exit 1
fi

mkdir -p "$(dirname "$report_path")"
changed=()
encoding_only=()
within_tolerance=()
diff_dir="$(dirname "$report_path")/diffs"

create_review_artifacts() {
  local baseline="$1"
  local candidate="$2"
  local name="$3"
  local raw_diff_path="$diff_dir/${name%.png}-diff.png"
  local mask_path="$diff_dir/.${name%.png}-overlay-mask.png"
  local overlay_path="$diff_dir/${name%.png}-overlay.png"
  local crop_path="$diff_dir/${name%.png}-overlay-crop.png"
  local bounds width height crop_width crop_height left top right bottom

  mkdir -p "$diff_dir"
  compare "$baseline" "$candidate" "$raw_diff_path" 2>/dev/null || true
  if [ ! -f "$raw_diff_path" ]; then
    printf 'Could not create visual diff for %s\n' "$name" >&2
    exit 1
  fi

  bounds="$(convert "$baseline" "$candidate" -compose difference -composite -threshold 0 -trim -format '%@' info:)"
  convert "$baseline" "$candidate" -compose difference -composite -threshold 0 \
    -morphology Dilate Disk:2 -alpha off -transparent black -fill red -colorize 100 "$mask_path"
  convert "$candidate" "$mask_path" -compose over -composite "$overlay_path"
  rm "$mask_path"

  if [[ "$bounds" =~ ^([0-9]+)x([0-9]+)\+([0-9]+)\+([0-9]+)$ ]]; then
    crop_width="${BASH_REMATCH[1]}"
    crop_height="${BASH_REMATCH[2]}"
    left="$((BASH_REMATCH[3] - 24))"
    top="$((BASH_REMATCH[4] - 24))"
    right="$((BASH_REMATCH[3] + crop_width + 24))"
    bottom="$((BASH_REMATCH[4] + crop_height + 24))"
    read -r width height <<< "$(identify -format '%w %h' "$candidate")"
    if (( left < 0 )); then left=0; fi
    if (( top < 0 )); then top=0; fi
    if (( right > width )); then right="$width"; fi
    if (( bottom > height )); then bottom="$height"; fi
    crop_width="$((right - left))"
    crop_height="$((bottom - top))"
    convert "$overlay_path" -crop "${crop_width}x${crop_height}+${left}+${top}" +repage -resize '200%' "$crop_path"
  fi
}

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
        read -r width height <<< "$(identify -format '%w %h' "$candidate")"
        max_differing_pixels="$(((width * height * max_differing_pixels_per_10000 + 9999) / 10000))"
        create_review_artifacts "$baseline" "$candidate" "$name"

        if [ "$differing_pixels" -gt "$max_differing_pixels" ]; then
          changed+=("$name (changed: $differing_pixels pixels; threshold: $max_differing_pixels)")
        else
          within_tolerance+=("$name (changed: $differing_pixels pixels; threshold: $max_differing_pixels)")
        fi
      fi
    fi
  done

  if [ "${#changed[@]}" -eq 0 ]; then
    echo "No material screenshot changes need review."
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

  if [ "${#within_tolerance[@]}" -gt 0 ]; then
    echo
    echo "The following candidates differ within the rasterisation tolerance; raw, overlay, and magnified-crop diffs are in the artifact:"
    echo
    for item in "${within_tolerance[@]}"; do echo "- $item"; done
  fi
} > "$report_path"

printf '%s\n' "${changed[@]}" > "${report_path%.md}.txt"
