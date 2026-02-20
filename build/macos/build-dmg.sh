#!/usr/bin/env bash

set -euo pipefail

if [[ $# -lt 2 || $# -gt 3 ]]; then
  echo "Usage: $0 <app-path> <output-dmg> [background-image]"
  exit 1
fi

app_path="$1"
output_dmg="$2"
background_image="${3:-images/social.png}"

if [[ ! -d "$app_path" ]]; then
  echo "App bundle not found: $app_path"
  exit 1
fi

if [[ -n "$background_image" && ! -f "$background_image" ]]; then
  echo "Background image not found: $background_image"
  exit 1
fi

app_name="$(basename "$app_path")"
app_noext="${app_name%.app}"
volume_name="$app_noext"

stage_dir="$(mktemp -d -t projectrover-dmg-stage.XXXXXX)"
rw_dmg="$(mktemp -u -t projectrover-dmg-rw.XXXXXX).dmg"
detach_device=""

cleanup() {
  if [[ -n "$detach_device" ]]; then
    for _ in {1..6}; do
      if hdiutil detach "$detach_device" -quiet >/dev/null 2>&1; then
        break
      fi
      sleep 1
    done
  fi

  rm -rf "$stage_dir" >/dev/null 2>&1 || true
  rm -f "$rw_dmg" >/dev/null 2>&1 || true
}
trap cleanup EXIT

cp -R "$app_path" "$stage_dir/"
ln -s /Applications "$stage_dir/Applications"

has_background=0
layout_width=980
layout_height=640
app_icon_x=120
apps_icon_x=360
icons_y=500
stage_background=""

if [[ -f "$background_image" ]]; then
  mkdir -p "$stage_dir/.background"
  stage_background="$stage_dir/.background/background.png"

  width_raw="$(sips -g pixelWidth "$background_image" 2>/dev/null | awk '/pixelWidth/ {print $2}')"
  height_raw="$(sips -g pixelHeight "$background_image" 2>/dev/null | awk '/pixelHeight/ {print $2}')"

  if [[ "$width_raw" =~ ^[0-9]+$ && "$height_raw" =~ ^[0-9]+$ ]]; then
    max_w=920
    max_h=580

    w_scaled=$width_raw
    h_scaled=$height_raw

    if (( w_scaled > max_w )); then
      h_scaled=$(( h_scaled * max_w / w_scaled ))
      w_scaled=$max_w
    fi

    if (( h_scaled > max_h )); then
      w_scaled=$(( w_scaled * max_h / h_scaled ))
      h_scaled=$max_h
    fi

    (( w_scaled < 820 )) && w_scaled=820
    (( h_scaled < 410 )) && h_scaled=410

    layout_width=$w_scaled
    layout_height=$h_scaled
  fi

  if sips -z "$layout_height" "$layout_width" "$background_image" --out "$stage_background" >/dev/null 2>&1; then
    has_background=1
  else
    cp "$background_image" "$stage_background"
    has_background=1
  fi

  # Place icons in the lower-left area (inside the light panel of social.png).
  app_icon_x=$(( layout_width * 15 / 100 ))
  apps_icon_x=$(( layout_width * 25 / 100 ))
  icons_y=$(( layout_height * 74 / 100 ))

  left_inset=140
  bottom_inset=95
  # The social background switches from light panel to dark panel around ~40% width.
  left_panel_limit=$(( layout_width * 40 / 100 ))
  apps_right_margin=80
  (( icons_y > layout_height - bottom_inset )) && icons_y=$(( layout_height - bottom_inset ))
fi

echo "Creating writable DMG: $rw_dmg"
hdiutil create -volname "$volume_name" -srcfolder "$stage_dir" -ov -format UDRW "$rw_dmg" >/dev/null

echo "Applying Finder window layout"
attach_output="$(hdiutil attach -readwrite -noverify -noautoopen "$rw_dmg")"
detach_device="$(awk '/^\/dev\// {print $1; exit}' <<< "$attach_output")"

if [[ -z "$detach_device" ]]; then
  echo "Unable to determine mounted DMG device."
  exit 1
fi

if command -v osascript >/dev/null 2>&1; then
  # Finder bounds include window chrome; add padding to avoid scroll bars.
  window_padding_w=24
  window_padding_h=62
  window_width=$(( layout_width + window_padding_w ))
  window_height=$(( layout_height + window_padding_h ))

  applescript=$(cat <<EOF
tell application "Finder"
  tell disk "$volume_name"
    open
    tell container window
      set current view to icon view
      set toolbar visible to false
      set pathbar visible to false
      set statusbar visible to false
      set bounds to {120, 120, $((120 + window_width)), $((120 + window_height))}
    end tell
    set viewOptions to the icon view options of container window
    set arrangement of viewOptions to not arranged
    set icon size of viewOptions to 84
    set text size of viewOptions to 14
EOF
)

  if [[ "$has_background" -eq 1 ]]; then
    applescript+=$'\n'"    set background picture of viewOptions to file \".background:background.png\""
  fi

  applescript+=$'\n'"    try"
  applescript+=$'\n'"      set position of item \"$app_name\" of container window to {$app_icon_x, $icons_y}"
  applescript+=$'\n'"    end try"
  applescript+=$'\n'"    try"
  applescript+=$'\n'"      set position of item \"Applications\" of container window to {$apps_icon_x, $icons_y}"
  applescript+=$'\n'"    end try"
  applescript+=$'\n'"    update without registering applications"
  applescript+=$'\n'"    delay 1"
  applescript+=$'\n'"    close"
  applescript+=$'\n'"    open"
  applescript+=$'\n'"    delay 1"
  applescript+=$'\n'"  end tell"
  applescript+=$'\n'"end tell"

  if ! osascript -e "$applescript" >/dev/null 2>&1; then
    echo "Warning: could not fully apply Finder layout; continuing."
  fi
fi

for _ in {1..6}; do
  if hdiutil detach "$detach_device" -quiet >/dev/null 2>&1; then
    detach_device=""
    break
  fi
  sleep 1
done

if [[ -n "$detach_device" ]]; then
  echo "Failed to detach DMG device: $detach_device"
  exit 1
fi

mkdir -p "$(dirname "$output_dmg")"
echo "Creating compressed DMG: $output_dmg"
hdiutil convert "$rw_dmg" -format UDZO -ov -o "$output_dmg" >/dev/null

if [[ ! -f "$output_dmg" && -f "${output_dmg}.dmg" ]]; then
  mv "${output_dmg}.dmg" "$output_dmg"
fi

if [[ ! -f "$output_dmg" ]]; then
  echo "Failed to produce DMG at: $output_dmg"
  exit 1
fi

echo "DMG created: $output_dmg"
