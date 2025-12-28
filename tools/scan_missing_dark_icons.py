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
    "#00539c": "#8ab4f8",  # blue variant seen in some svgs
    "#6936aa": "#c3a5ff",
    "#996f00": "#e0b44c",
    "#f6f6f6": "#1f1f1f",  # toolbar background light -> dark counterpart
    "#f0eff1": "#424242",
}

HEX_RE = re.compile(r"#(?:[0-9a-fA-F]{3}){1,2}")


def hex_to_rgb(hexstr: str):
    s = hexstr.lstrip('#')
    if len(s) == 3:
        s = ''.join(ch*2 for ch in s)
    r = int(s[0:2], 16)
    g = int(s[2:4], 16)
    b = int(s[4:6], 16)
    return r, g, b


def rgb_to_hex(r: int, g: int, b: int) -> str:
    return '#{:02x}{:02x}{:02x}'.format(max(0, min(255, int(r))), max(0, min(255, int(g))), max(0, min(255, int(b))))


def rgb_to_hsl(r: int, g: int, b: int):
    r_, g_, b_ = r/255.0, g/255.0, b/255.0
    mx = max(r_, g_, b_)
    mn = min(r_, g_, b_)
    l = (mx + mn) / 2
    if mx == mn:
        h = s = 0.0
    else:
        d = mx - mn
        s = d / (2 - mx - mn) if l > 0.5 else d / (mx + mn)
        if mx == r_:
            h = (g_ - b_) / d + (6 if g_ < b_ else 0)
        elif mx == g_:
            h = (b_ - r_) / d + 2
        else:
            h = (r_ - g_) / d + 4
        h /= 6
    return h, s, l


def hsl_to_rgb(h: float, s: float, l: float):
    def hue2rgb(p, q, t):
        if t < 0:
            t += 1
        if t > 1:
            t -= 1
        if t < 1/6:
            return p + (q - p) * 6 * t
        if t < 1/2:
            return q
        if t < 2/3:
            return p + (q - p) * (2/3 - t) * 6
        return p

    if s == 0:
        r = g = b = int(l * 255)
    else:
        q = l + s - l*s if l < 0.5 else l + s - l*s
        p = 2 * l - q
        r = int(hue2rgb(p, q, h + 1/3) * 255)
        g = int(hue2rgb(p, q, h) * 255)
        b = int(hue2rgb(p, q, h - 1/3) * 255)
    return r, g, b


def perceived_luminance(r: int, g: int, b: int) -> float:
    # relative luminance (0-1)
    return (0.2126 * r + 0.7152 * g + 0.0722 * b) / 255.0


def find_light_icons(assets_root: str):
    return sorted(f for f in os.listdir(assets_root) if f.lower().endswith(".svg"))


def needs_dark_variant(svg_path: str) -> bool:
    text = read_text(svg_path)
    lowered = text.lower()
    # If it contains any explicitly configured color that we know should have a dark counterpart
    if any(k in lowered for k in LIGHT_TO_DARK.keys()):
        return True

    # Otherwise, scan for any very-light hex colors (likely to blend with light toolbar)
    for m in HEX_RE.findall(text):
        try:
            r, g, b = hex_to_rgb(m)
        except Exception:
            continue
        lum = perceived_luminance(r, g, b)
        # consider colors with very high luminance as needing dark variants
        if lum >= 0.88:
            return True

    return False


def read_text(path: str) -> str:
    with open(path, encoding="utf-8", errors="ignore") as f:
        return f.read()


def write_text(path: str, content: str):
    with open(path, "w", encoding="utf-8") as f:
        f.write(content)


def generate_dark(light_path: str, dark_path: str):
    content = read_text(light_path)
    lowered = content.lower()
    replaced = set()
    # First apply canonical replacements
    for light, dark in LIGHT_TO_DARK.items():
        if light in lowered:
            content = re.sub(light, dark, content, flags=re.IGNORECASE)
            replaced.add(light)

    # Then find remaining hex colors and darken any very-light colors using HSL fallback
    found_hexes = set(m.lower() for m in HEX_RE.findall(content))
    for hx in found_hexes:
        if hx in replaced:
            continue
        try:
            r, g, b = hex_to_rgb(hx)
        except Exception:
            continue
        lum = perceived_luminance(r, g, b)
        if lum >= 0.88:
            # darken by flipping lightness: new_l = max(0.06, 1 - l)
            h, s, l = rgb_to_hsl(r, g, b)
            new_l = max(0.06, 1.0 - l)
            nr, ng, nb = hsl_to_rgb(h, s, new_l)
            dark_hex = rgb_to_hex(nr, ng, nb)
            content = re.sub(re.escape(hx), dark_hex, content, flags=re.IGNORECASE)
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
