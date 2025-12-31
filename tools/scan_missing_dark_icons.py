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
Additional colors are normalized and low-opacity layers are boosted to improve legibility on dark backgrounds.
"""

import argparse
import os
import re
import shutil

HERE = os.path.abspath(os.path.dirname(__file__))
DEFAULT_ASSETS_ROOT = os.path.normpath(os.path.join(HERE, "..", "src", "ProjectRover", "Assets"))

LIGHT_TO_DARK = {
    "#212121": "#dcdcdc",
    "#424242": "#bdbdbd",
    "#272727": "#bdbdbd",
    "#005dba": "#8ab4f8",
    "#00539c": "#8ab4f8",  # blue variant seen in some svgs
    "#0077a0": "#8ab4f8",
    "#0000ff": "#8ab4f8",
    "#1ba1e2": "#8ab4f8",
    "#03a5ef": "#8ab4f8",
    "#0095d7": "#8ab4f8",
    "#6936aa": "#c3a5ff",
    "#652d90": "#c3a5ff",
    "#996f00": "#e0b44c",
    "#c27d1a": "#e0b44c",
    "#ffcc00": "#e0b44c",
    "#ffb803": "#e0b44c",
    "#dcb67a": "#e0b44c",
    "#388a34": "#6bd46b",
    "#218022": "#6bd46b",
    "#339933": "#6bd46b",
    "#7fbb03": "#6bd46b",
    "#f6f6f6": "#1f1f1f",  # toolbar background light -> dark counterpart
    "#f0eff1": "#bdbdbd",
    "#d1d1d1": "#dcdcdc",
}

HEX_RE = re.compile(r"#(?:[0-9a-fA-F]{3}){1,2}")
OPACITY_RE = re.compile(r'(opacity\s*[:=]\s*["\']?)(0?\.\d+|\d+)(["\']?)', re.IGNORECASE)

STRICT_FOREGROUND_CLASSES = [
    "icon-vs-fg",
    "icon-vs-out",
    "icon-vs-bg",
]

PREFERRED_FOREGROUND_HEX = "#dcdcdc"
LOW_OPACITY_THRESHOLD = 0.2
STANDARD_MUTED_ALPHA = 0.35


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


def normalize_hex(hexstr: str) -> str:
    s = hexstr.lower()
    if len(s) == 4:
        s = "#" + "".join(ch * 2 for ch in s[1:])
    return s


def find_light_icons(assets_root: str):
    return sorted(f for f in os.listdir(assets_root) if f.lower().endswith(".svg"))


def needs_dark_variant(svg_path: str) -> bool:
    text = read_text(svg_path)
    # If it contains any explicitly configured color that we know should have a dark counterpart
    for m in HEX_RE.findall(text):
        norm = normalize_hex(m)
        if norm in LIGHT_TO_DARK:
            return True
        try:
            r, g, b = hex_to_rgb(norm)
        except Exception:
            continue
        lum = perceived_luminance(r, g, b)
        # consider colors with very high or very low luminance as needing dark variants
        if lum >= 0.88 or lum <= 0.35:
            return True

    return False


def read_text(path: str) -> str:
    with open(path, encoding="utf-8", errors="ignore") as f:
        return f.read()


def write_text(path: str, content: str):
    with open(path, "w", encoding="utf-8") as f:
        f.write(content)


def auto_dark_color(norm_hex: str) -> str:
    r, g, b = hex_to_rgb(norm_hex)
    h, s, l = rgb_to_hsl(r, g, b)
    # Neutral greys get standardized to readable foreground greys.
    if s < 0.15:
        return "#bdbdbd" if l < 0.6 else "#dcdcdc"
    # Saturated accents get a pastel lift for dark backgrounds.
    if l < 0.45:
        target_l = 0.72
    elif l > 0.8:
        target_l = 0.68
    else:
        target_l = max(0.62, min(0.78, l + 0.2))
    target_s = min(s, 0.7)
    nr, ng, nb = hsl_to_rgb(h, target_s, target_l)
    return rgb_to_hex(nr, ng, nb)


def generate_dark(light_path: str, dark_path: str):
    content = read_text(light_path)
    # Build a per-color mapping from the light SVG.
    color_map = {}
    for raw_hex in set(HEX_RE.findall(content)):
        norm_hex = normalize_hex(raw_hex)
        if norm_hex in LIGHT_TO_DARK:
            target = LIGHT_TO_DARK[norm_hex]
        else:
            target = auto_dark_color(norm_hex)
        color_map[raw_hex] = target

    for raw_hex, target in color_map.items():
        content = re.sub(re.escape(raw_hex), target, content, flags=re.IGNORECASE)

    # Enforce strict foreground classes to readable greys.
    for cls in STRICT_FOREGROUND_CLASSES:
        content = re.sub(
            r"(\." + re.escape(cls) + r"\s*\{[^}]*fill\s*:\s*)(?:#[0-9a-fA-F]{3,6})",
            r"\1" + PREFERRED_FOREGROUND_HEX,
            content,
            flags=re.IGNORECASE,
        )

    # Boost very low opacities for dark legibility.
    def bump_opacity(match: re.Match) -> str:
        value = float(match.group(2))
        if value < LOW_OPACITY_THRESHOLD:
            return f"{match.group(1)}{STANDARD_MUTED_ALPHA}{match.group(3)}"
        return match.group(0)

    content = OPACITY_RE.sub(bump_opacity, content)
    os.makedirs(os.path.dirname(dark_path), exist_ok=True)
    write_text(dark_path, content)


def main():
    parser = argparse.ArgumentParser(description="Scan for missing dark icon variants based on palette usage.")
    parser.add_argument("--assets-root", default=DEFAULT_ASSETS_ROOT, help=f"Path to Assets/ (default: {DEFAULT_ASSETS_ROOT})")
    parser.add_argument("--generate", action="store_true", help="Generate missing dark icons using the canonical mapping.")
    parser.add_argument("--regenerate", action="store_true", help="Regenerate existing dark icons from light sources.")
    args = parser.parse_args()

    assets_root = args.assets_root
    dark_root = os.path.join(assets_root, "Dark")

    light_icons = find_light_icons(assets_root)
    dark_icons = set(f for f in os.listdir(dark_root) if f.lower().endswith(".svg")) if os.path.isdir(dark_root) else set()

    missing = []
    updated = []
    for name in light_icons:
        light_path = os.path.join(assets_root, name)
        if not needs_dark_variant(light_path):
            continue
        label = None
        if name not in dark_icons:
            missing.append(name)
            label = "missing"
        elif args.regenerate:
            updated.append(name)
            label = "update"
        else:
            continue
        print(f"[{label}] {name}")
        if args.generate:
            dark_path = os.path.join(dark_root, name)
            generate_dark(light_path, dark_path)
            print(f"  -> generated {dark_path}")

    if not missing and not updated:
        print("No missing dark icons detected.")
    else:
        if missing:
            print(f"Total missing dark variants: {len(missing)}")
        if updated:
            print(f"Total regenerated dark variants: {len(updated)}")


if __name__ == "__main__":
    main()
