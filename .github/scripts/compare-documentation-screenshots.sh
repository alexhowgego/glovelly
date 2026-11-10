#!/usr/bin/env bash

set -euo pipefail

candidate_dir="${1:?candidate directory is required}"
baseline_dir="${2:?baseline directory is required}"
report_path="${3:?report path is required}"
# Allow up to 0.05% changed pixels for harmless browser rasterisation drift.
max_differing_pixels_per_10000="${DOCUMENTATION_SCREENSHOT_MAX_DIFF_PER_10000:-5}"
# Ignore very small per-pixel colour drift before applying the area threshold.
colour_fuzz="3%"

if ! [[ "$max_differing_pixels_per_10000" =~ ^[0-9]+$ ]]; then
  printf 'DOCUMENTATION_SCREENSHOT_MAX_DIFF_PER_10000 must be a whole number\n' >&2
  exit 1
fi

mkdir -p "$(dirname "$report_path")"
changed=()
encoding_only=()
within_tolerance=()
masked_maps=()
diff_dir="$(dirname "$report_path")/diffs"
comparison_dir="$(mktemp -d "${TMPDIR:-/tmp}/glovelly-screenshot-compare.XXXXXXXX")"
trap 'rm -rf "$comparison_dir"' EXIT

normalise_pixel_count() {
  # ImageMagick may emit AE counts using scientific notation. Request full
  # double precision below, then validate and format an integral count in C locale.
  LC_ALL=C awk -v metric="$1" 'BEGIN {
    if (metric !~ /^[0-9]+([.][0-9]+)?([eE][+-]?[0-9]+)?$/) exit 1
    count = metric + 0
    if (count < 0 || count > 9007199254740991 || count != int(count)) exit 1
    printf "%.0f", count
  }'
}

create_review_artifacts() {
  local baseline="$1"
  local candidate="$2"
  local name="$3"
  local raw_diff_path="$diff_dir/${name%.png}-diff.png"
  local mask_path="$diff_dir/.${name%.png}-overlay-mask.png"
  local overlay_path="$diff_dir/${name%.png}-overlay.png"
  local crop_path="$diff_dir/${name%.png}-overlay-crop.png"
  local bounds width height crop_width crop_height left top right bottom compare_status=0

  mkdir -p "$diff_dir"
  compare -fuzz "$colour_fuzz" "$baseline" "$candidate" "$raw_diff_path" 2>/dev/null || compare_status=$?
  if (( compare_status > 1 )) || [ ! -f "$raw_diff_path" ]; then
    printf 'Could not create visual diff for %s\n' "$name" >&2
    exit 1
  fi

  bounds="$(convert "$baseline" "$candidate" -compose difference -composite -threshold "$colour_fuzz" -trim -format '%@' info:)"
  convert "$baseline" "$candidate" -compose difference -composite -threshold "$colour_fuzz" \
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
      compare_baseline="$baseline"
      compare_candidate="$candidate"
      mask_file="$candidate.mask"
      if [ -f "$mask_file" ]; then
        case "$name" in
          client-workspace-*.png|gig-workspace-*.png) ;;
          *) printf 'Unexpected map mask for %s\n' "$name" >&2; exit 1 ;;
        esac
        mask="$(cat "$mask_file")"
        if ! [[ "$mask" =~ ^([0-9]+)\ ([0-9]+)\ ([0-9]+)\ ([0-9]+)$ ]]; then
          printf 'Invalid map mask for %s\n' "$name" >&2; exit 1
        fi
        left="${BASH_REMATCH[1]}"; top="${BASH_REMATCH[2]}"; right="${BASH_REMATCH[3]}"; bottom="${BASH_REMATCH[4]}"
        read -r width height <<< "$(identify -format '%w %h' "$candidate")"
        read -r baseline_width baseline_height <<< "$(identify -format '%w %h' "$baseline")"
        if (( left >= right || top >= bottom || right > width || bottom > height || width != baseline_width || height != baseline_height )); then
          printf 'Map mask or image dimensions invalid for %s\n' "$name" >&2; exit 1
        fi
        compare_baseline="$comparison_dir/baseline.png"
        compare_candidate="$comparison_dir/candidate.png"
        # Rectangle endpoints are inclusive in ImageMagick; the mask uses exclusive bounds.
        rectangle="rectangle $left,$top $((right - 1)),$((bottom - 1))"
        convert "$baseline" -fill black -draw "$rectangle" "$compare_baseline"
        convert "$candidate" -fill black -draw "$rectangle" "$compare_candidate"
        masked_maps+=("$name ($left,$top to $right,$bottom)")
      fi
      compare_status=0
      metric="$(LC_ALL=C compare -precision 17 -metric AE -fuzz "$colour_fuzz" "$compare_baseline" "$compare_candidate" null: 2>&1)" || compare_status=$?
      # ImageMagick returns 0 for identical images, 1 for differences, and >1
      # for errors. Never turn a genuine tool failure into a freshness result.
      if (( compare_status > 1 )); then
        printf 'Could not compare decoded pixels for %s (exit %s): %s\n' "$name" "$compare_status" "$metric" >&2
        exit 1
      fi
      if ! differing_pixels="$(normalise_pixel_count "$metric")"; then
        printf 'Invalid decoded-pixel count for %s: %s\n' "$name" "$metric" >&2
        exit 1
      fi

      if [ "$differing_pixels" -eq 0 ]; then
        encoding_only+=("$name")
      else
        read -r width height <<< "$(identify -format '%w %h' "$candidate")"
        max_differing_pixels="$(((width * height * max_differing_pixels_per_10000 + 9999) / 10000))"
        create_review_artifacts "$compare_baseline" "$compare_candidate" "$name"

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
    echo "The following candidates have no differences beyond the colour tolerance and excluded map pixels, and need no review:"
    echo
    for name in "${encoding_only[@]}"; do echo "- $name"; done
  fi

  if [ "${#within_tolerance[@]}" -gt 0 ]; then
    echo
    echo "The following candidates differ within the rasterisation tolerance; raw, overlay, and magnified-crop diffs are in the artifact:"
    echo
    for item in "${within_tolerance[@]}"; do echo "- $item"; done
  fi

  if [ "${#masked_maps[@]}" -gt 0 ]; then
    echo
    echo "Live map image regions excluded from comparison (coordinates use exclusive right/bottom bounds):"
    echo
    for item in "${masked_maps[@]}"; do echo "- $item"; done
    echo
    echo "Review real candidates separately for map presence, placement and attribution."
  fi
} > "$report_path"

if [ "${#changed[@]}" -gt 0 ]; then
  printf '%s\n' "${changed[@]}" > "${report_path%.md}.txt"
else
  : > "${report_path%.md}.txt"
fi
