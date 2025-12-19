#!/usr/bin/env python3
"""
Scan light icons for palette usage and report/generate missing dark variants.

Rules (canonical light -> dark mapping):
  #212121   -> #dcdcdc
  #005dba   -> #8ab4f8
  #6936aa   -> #c3a5ff
  #996f00   -> #e0b44c

If a light SVG uses any of these light colors, we expect a dark counterpart.
With --generate, a dark SVG is created by copying the light file and applying the mapping.
"""

import argparse
import os
import re
import shutil

HERE = os.path.abspath(os.path.dirname(__file__))
DEFAULT_ASSETS_ROOT = os.path.normpath(os.path.join(HERE, "..", "src", "ProjectRover", "Assets"))

LIGHT_TO_DARK = {
    "#212121": "#dcdcdc",
    "#005dba": "#8ab4f8",
    "#6936aa": "#c3a5ff",
    "#996f00": "#e0b44c",
}

HEX_RE = re.compile(r"#(?:[0-9a-fA-F]{3}){1,2}")


def find_light_icons(assets_root: str):
    return sorted(f for f in os.listdir(assets_root) if f.lower().endswith(".svg"))


def needs_dark_variant(svg_path: str) -> bool:
    text = read_text(svg_path)
    return any(k in text.lower() for k in LIGHT_TO_DARK.keys())


def read_text(path: str) -> str:
    with open(path, encoding="utf-8", errors="ignore") as f:
        return f.read()


def write_text(path: str, content: str):
    with open(path, "w", encoding="utf-8") as f:
        f.write(content)


def generate_dark(light_path: str, dark_path: str):
    content = read_text(light_path)
    lowered = content.lower()
    for light, dark in LIGHT_TO_DARK.items():
        if light in lowered:
            content = re.sub(light, dark, content, flags=re.IGNORECASE)
    os.makedirs(os.path.dirname(dark_path), exist_ok=True)
    write_text(dark_path, content)


def main():
    parser = argparse.ArgumentParser(description="Scan for missing dark icon variants based on palette usage.")
    parser.add_argument("--assets-root", default=DEFAULT_ASSETS_ROOT, help=f"Path to Assets/ (default: {DEFAULT_ASSETS_ROOT})")
    parser.add_argument("--generate", action="store_true", help="Generate missing dark icons using the canonical mapping.")
    args = parser.parse_args()

    assets_root = args.assets_root
    dark_root = os.path.join(assets_root, "Dark")

    light_icons = find_light_icons(assets_root)
    dark_icons = set(f for f in os.listdir(dark_root) if f.lower().endswith(".svg")) if os.path.isdir(dark_root) else set()

    missing = []
    for name in light_icons:
        light_path = os.path.join(assets_root, name)
        if not needs_dark_variant(light_path):
            continue
        if name not in dark_icons:
            missing.append(name)
            print(f"[missing] {name}")
            if args.generate:
                dark_path = os.path.join(dark_root, name)
                generate_dark(light_path, dark_path)
                print(f"  -> generated {dark_path}")

    if not missing:
        print("No missing dark icons detected.")
    else:
        print(f"Total missing dark variants: {len(missing)}")


if __name__ == "__main__":
    main()
