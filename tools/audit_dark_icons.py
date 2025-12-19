#!/usr/bin/env python3
"""
Audit dark SVG icons for palette compliance and optionally fix them.

Canonical mappings (light -> dark):
  #212121   -> #dcdcdc
  #005dba   -> #8ab4f8
  #6936aa   -> #c3a5ff
  #996f00   -> #e0b44c

Legacy dark colors are normalized to the canonical dark palette when --fix is used:
  #c0c0c0   -> #dcdcdc
  #005dba   -> #8ab4f8
"""

import argparse
import os
import re

HERE = os.path.abspath(os.path.dirname(__file__))
DEFAULT_ASSETS_ROOT = os.path.normpath(os.path.join(HERE, "..", "src", "ProjectRover", "Assets"))

LIGHT_TO_DARK = {
    "#212121": "#dcdcdc",
    "#005dba": "#8ab4f8",
    "#6936aa": "#c3a5ff",
    "#996f00": "#e0b44c",
}

NORMALIZE_DARK = {
    "#c0c0c0": "#dcdcdc",
    "#005dba": "#8ab4f8",  # encourage lightened blue
}

LOW_OPACITY_THRESHOLD = 0.2
STANDARD_MUTED_ALPHA = 0.35
OPACITY_RE = re.compile(r'(opacity\s*[:=]\s*["\']?)(0?\.\d+|\d+)(["\']?)', re.IGNORECASE)
HEX_RE = re.compile(r"#(?:[0-9a-fA-F]{3}){1,2}")


def read_text(path: str) -> str:
    with open(path, encoding="utf-8", errors="ignore") as f:
        return f.read()


def write_text(path: str, content: str):
    with open(path, "w", encoding="utf-8") as f:
        f.write(content)


def audit_file(path: str):
    text = read_text(path)
    colors = set(m.lower() for m in HEX_RE.findall(text))

    issues = []
    for light, dark in LIGHT_TO_DARK.items():
        if light in colors:
            issues.append(f"contains light color {light}")
    for legacy, canonical in NORMALIZE_DARK.items():
        if legacy in colors and legacy != canonical:
            issues.append(f"uses legacy dark {legacy} (canonical {canonical})")
    low_opacities = sorted({m.group(2) for m in OPACITY_RE.finditer(text) if float(m.group(2)) < LOW_OPACITY_THRESHOLD})
    if low_opacities:
        issues.append(f"opacity too low ({', '.join(low_opacities)}) use ~{STANDARD_MUTED_ALPHA}")
    return issues, colors


def fix_file(path: str):
    content = read_text(path)
    # Replace light colors with canonical dark
    for light, dark in LIGHT_TO_DARK.items():
        content = re.sub(light, dark, content, flags=re.IGNORECASE)
    # Normalize legacy dark colors
    for legacy, canonical in NORMALIZE_DARK.items():
        content = re.sub(legacy, canonical, content, flags=re.IGNORECASE)
    # Bump very low opacities to the standard muted alpha
    def bump_opacity(match: re.Match) -> str:
        value = float(match.group(2))
        if value < LOW_OPACITY_THRESHOLD:
            return f"{match.group(1)}{STANDARD_MUTED_ALPHA}{match.group(3)}"
        return match.group(0)

    content = OPACITY_RE.sub(bump_opacity, content)
    write_text(path, content)


def main():
    parser = argparse.ArgumentParser(description="Audit dark SVG icons for palette compliance.")
    parser.add_argument("--assets-root", default=DEFAULT_ASSETS_ROOT, help=f"Path to Assets/ (default: {DEFAULT_ASSETS_ROOT})")
    parser.add_argument("--fix", action="store_true", help="Rewrite non-compliant dark SVGs using the canonical palette.")
    args = parser.parse_args()

    dark_root = os.path.join(args.assets_root, "Dark")
    if not os.path.isdir(dark_root):
        print(f"Dark folder not found: {dark_root}")
        return

    dark_icons = sorted(f for f in os.listdir(dark_root) if f.lower().endswith(".svg"))
    non_compliant = 0
    for name in dark_icons:
        path = os.path.join(dark_root, name)
        issues, colors = audit_file(path)
        if issues:
            non_compliant += 1
            print(f"[non-compliant] {name}: {', '.join(issues)}")
            if args.fix:
                fix_file(path)
                print(f"  -> fixed {name}")
    if non_compliant == 0:
        print("All dark icons comply with the canonical palette.")
    else:
        print(f"Non-compliant dark icons: {non_compliant}/{len(dark_icons)}")


if __name__ == "__main__":
    main()
