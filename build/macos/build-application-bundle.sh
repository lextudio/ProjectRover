#    Copyright 2024 CodeMerx
#    Copyright 2025 LeXtudio Inc.
#    This file is part of ProjectRover.

#    ProjectRover is free software: you can redistribute it and/or modify
#    it under the terms of the GNU Affero General Public License as published by
#    the Free Software Foundation, either version 3 of the License, or
#    (at your option) any later version.

#    ProjectRover is distributed in the hope that it will be useful,
#    but WITHOUT ANY WARRANTY; without even the implied warranty of
#    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
#    GNU Affero General Public License for more details.

#    You should have received a copy of the GNU Affero General Public License
#    along with ProjectRover.  If not, see<https://www.gnu.org/licenses/>.

#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 1 ]]; then
  echo "Usage: $0 <rid|osx-universal>"
  exit 1
fi

rid="$1"
base_dir="src/ProjectRover/bin/Release/net10.0"
bundle_root="ProjectRover.app"
bundle_macos="$bundle_root/Contents/MacOS"
script_dir="$(cd "$(dirname "$0")" && pwd)"

rm -rf "$bundle_root"
mkdir -p "$bundle_root/Contents/Resources" "$bundle_macos"
cp "$script_dir/Info.plist" "$bundle_root/Contents"
cp "$script_dir/projectrover.icns" "$bundle_root/Contents/Resources"

is_macho() {
  local path="$1"
  file -b "$path" 2>/dev/null | grep -q "Mach-O"
}

if [[ "$rid" != "osx-universal" ]]; then
  src="$base_dir/$rid/publish"
  if [[ ! -d "$src" ]]; then
    echo "Publish directory not found: $src"
    exit 1
  fi
  cp -Rp "$src"/. "$bundle_macos/"
  exit 0
fi

arm_src="$base_dir/osx-arm64/publish"
x64_src="$base_dir/osx-x64/publish"

if [[ ! -d "$arm_src" ]]; then
  echo "Publish directory not found: $arm_src"
  exit 1
fi
if [[ ! -d "$x64_src" ]]; then
  echo "Publish directory not found: $x64_src"
  exit 1
fi

# Use arm64 publish as base payload for the .app, then merge native binaries with x64.
cp -Rp "$arm_src"/. "$bundle_macos/"

while IFS= read -r -d '' arm_file; do
  rel="${arm_file#$arm_src/}"
  x64_file="$x64_src/$rel"
  dest_file="$bundle_macos/$rel"

  [[ -f "$x64_file" ]] || continue
  if ! is_macho "$arm_file"; then
    continue
  fi
  if ! is_macho "$x64_file"; then
    continue
  fi

  arm_archs="$(lipo -archs "$arm_file" 2>/dev/null || true)"
  x64_archs="$(lipo -archs "$x64_file" 2>/dev/null || true)"
  if [[ -n "$arm_archs" && "$arm_archs" == "$x64_archs" ]]; then
    # Already universal (or same arch set), keep arm64 copy.
    continue
  fi

  lipo -create "$x64_file" "$arm_file" -output "$dest_file"
  chmod +x "$dest_file" || true
done < <(find "$arm_src" -type f -print0)
