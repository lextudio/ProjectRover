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

# Additional mappings for accents we observed
ACCENT_DARK_MAP = {
    "#388a34": "#6bd46b",  # make green accents lighter for dark backgrounds
}

NORMALIZE_DARK = {
    "#c0c0c0": "#dcdcdc",
    "#005dba": "#8ab4f8",  # encourage lightened blue
    "#0000ff": "#8ab4f8",
    "#0077a0": "#8ab4f8",
}

# Grey normalization: make mid greys lighter for foreground roles on dark theme
GREY_TO_LIGHT = {
    "#424242": "#bdbdbd",
    "#666666": "#cfcfcf",
    "#272727": "#bdbdbd",
}

# Classes that should use lighter greys on dark theme
STRICT_FOREGROUND_CLASSES = [
    'icon-vs-fg',
    'icon-vs-out',
    'icon-vs-bg',
]

# Preferred readable foreground for dark theme
PREFERRED_FOREGROUND_HEX = '#dcdcdc'

LOW_OPACITY_THRESHOLD = 0.2
STANDARD_MUTED_ALPHA = 0.35
OPACITY_RE = re.compile(r'(opacity\s*[:=]\s*["\']?)(0?\.\d+|\d+)(["\']?)', re.IGNORECASE)
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
    return (0.2126 * r + 0.7152 * g + 0.0722 * b) / 255.0


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


def learn_mappings(assets_root: str):
    """Look for light/dark filename pairs and infer color mappings by matching hex usage.
    Returns a dict mapping light_hex -> dark_hex for discovered pairs."""
    light_root = assets_root
    dark_root = os.path.join(assets_root, "Dark")
    learned = {}
    if not os.path.isdir(dark_root):
        return learned
    light_files = [f for f in os.listdir(light_root) if f.lower().endswith('.svg')]
    dark_files = {f for f in os.listdir(dark_root) if f.lower().endswith('.svg')}
    for name in light_files:
        if name in dark_files:
            light_path = os.path.join(light_root, name)
            dark_path = os.path.join(dark_root, name)
            ltext = read_text(light_path).lower()
            dtext = read_text(dark_path).lower()
            lcolors = [m for m in HEX_RE.findall(ltext)]
            dcolors = [m for m in HEX_RE.findall(dtext)]
            # simple heuristic: pair first few frequent colors
            for lc in set(lcolors):
                if lc in learned:
                    continue
                # find candidate in dcolors with similar role (heuristic: same index occurrence)
                try:
                    li = lcolors.index(lc)
                except ValueError:
                    continue
                if li < len(dcolors):
                    learned[lc] = dcolors[li]
    return learned


def learn_role_mappings(assets_root: str):
    """Learn class -> dark color mappings by inspecting <style> blocks in paired light/dark SVGs."""
    dark_root = os.path.join(assets_root, "Dark")
    role_map = {}
    if not os.path.isdir(dark_root):
        return role_map

    def extract_styles(text: str):
        styles = {}
        for style_block in re.findall(r"<style[^>]*>(.*?)</style>", text, flags=re.DOTALL | re.IGNORECASE):
            for m in re.finditer(r"\.(?P<class>[\w-]+)\s*\{[^}]*?fill\s*:\s*(?P<hex>#[0-9a-fA-F]{3,6})", style_block, flags=re.IGNORECASE):
                cls = m.group('class').strip()
                hexc = m.group('hex').lower()
                styles[cls] = hexc
        return styles

    light_root = assets_root
    light_files = [f for f in os.listdir(light_root) if f.lower().endswith('.svg')]
    dark_files = {f for f in os.listdir(dark_root) if f.lower().endswith('.svg')}
    counts = {}
    for name in light_files:
        if name in dark_files:
            ltext = read_text(os.path.join(light_root, name)).lower()
            dtext = read_text(os.path.join(dark_root, name)).lower()
            lstyles = extract_styles(ltext)
            dstyles = extract_styles(dtext)
            for cls, lhex in lstyles.items():
                if cls in dstyles:
                    dhex = dstyles[cls]
                    # accumulate consistent mappings
                    if cls not in role_map:
                        role_map[cls] = dhex
                        counts[cls] = 1
                    else:
                        if role_map[cls] == dhex:
                            counts[cls] += 1
                        else:
                            # if conflicting, prefer the canonical if present
                            counts[cls] += 1
    return role_map


def fix_file(path: str):
    content = read_text(path)
    # quick normalization for known problematic blues and accents
    for src, dst in {"#0077a0": "#8ab4f8", "#0000ff": "#8ab4f8"}.items():
        content = re.sub(re.escape(src), dst, content, flags=re.IGNORECASE)
    # Try to refine using the paired light file if present
    assets_root = os.path.normpath(os.path.join(HERE, '..', 'src', 'ProjectRover', 'Assets'))
    light_path = os.path.join(assets_root, os.path.basename(path))
    if os.path.exists(light_path):
        ltext = read_text(light_path).lower()
        dtext = content.lower()
        lcolors = [m for m in HEX_RE.findall(ltext)]
        dcolors = [m for m in HEX_RE.findall(dtext)]
        for i, lc in enumerate(lcolors):
            # prefer canonical mapping if it exists, else try to use learned mapping
            lc_l = lc.lower()
            if lc_l in LIGHT_TO_DARK:
                preferred = LIGHT_TO_DARK[lc_l]
            elif lc_l in learned:
                preferred = learned[lc_l]
            else:
                # default to a readable light grey for fg roles
                preferred = PREFERRED_FOREGROUND_HEX
            # replace occurrences of the lc in the dark content
            content = re.sub(re.escape(lc_l), preferred, content, flags=re.IGNORECASE)
    # Replace light colors with canonical dark
    for light, dark in LIGHT_TO_DARK.items():
        content = re.sub(light, dark, content, flags=re.IGNORECASE)
    # Normalize legacy dark colors
    for legacy, canonical in NORMALIZE_DARK.items():
        content = re.sub(legacy, canonical, content, flags=re.IGNORECASE)
    # Normalize accent colors (green/others)
    for accent_light, accent_dark in ACCENT_DARK_MAP.items():
        content = re.sub(accent_light, accent_dark, content, flags=re.IGNORECASE)

    # Apply role-based mappings if available (class -> hex), try to replace class definitions in style blocks
    if 'ROLE_MAPPINGS' in globals() and ROLE_MAPPINGS:
        for cls, hexval in ROLE_MAPPINGS.items():
            # replace .cls { fill: #xxxxxx } occurrences
            content = re.sub(r'(\.' + re.escape(cls) + r'\s*\{[^}]*fill\s*:\s*)(?:#[0-9a-fA-F]{3,6})', r"\1" + hexval, content, flags=re.IGNORECASE)
    # Apply class-based overrides for strict foreground classes first
    for cls in STRICT_FOREGROUND_CLASSES:
        ROLE_MAPPINGS.setdefault(cls, PREFERRED_FOREGROUND_HEX)

    # Apply grey->light normalization for better visibility of grey elements
    for g, l in GREY_TO_LIGHT.items():
        # replace classed fills in style blocks
        content = re.sub(r'(\.icon-vs-fg\s*\{[^}]*fill\s*:\s*)(?:#[0-9a-fA-F]{3,6})', r"\1" + l, content, flags=re.IGNORECASE)
        # replace inline fills
        content = re.sub(r'(fill\s*=\s*["\'])(?:'+re.escape(g[1:])+r')(["\'])', r"\1" + l[1:] + r"\2", content, flags=re.IGNORECASE)
        # generic replacement as fallback
        content = re.sub(g, l, content, flags=re.IGNORECASE)

    # Force class based overrides for known foreground classes
    for cls in STRICT_FOREGROUND_CLASSES:
        if cls in ROLE_MAPPINGS:
            val = ROLE_MAPPINGS[cls]
        else:
            # prefer a readable light grey
            val = '#bdbdbd'
        # replace style class fills
        content = re.sub(r'(\.' + re.escape(cls) + r'\s*\{[^}]*fill\s*:\s*)(?:#[0-9a-fA-F]{3,6})', r"\1" + val, content, flags=re.IGNORECASE)
        # replace inline fill and stroke attributes
        def replace_fill(match: re.Match) -> str:
            return f"{match.group(1)}{val}{match.group(2)}"

        def replace_stroke(match: re.Match) -> str:
            return f"{match.group(1)}{val}{match.group(2)}"

        content = re.sub(r'(fill\s*=\s*["\'])(?:#[0-9a-fA-F]{3,6})(["\'])', replace_fill, content, flags=re.IGNORECASE)
        content = re.sub(r'(stroke\s*=\s*["\'])(?:#[0-9a-fA-F]{3,6})(["\'])', replace_stroke, content, flags=re.IGNORECASE)

    # Also replace class-based 'light-lightblue' usage specifically
    content = re.sub(r'(\.light-lightblue(?:-10)?\s*\{[^}]*fill\s*:\s*)(?:#[0-9a-fA-F]{3,6})', r"\1#8ab4f8", content, flags=re.IGNORECASE)
    # Bump very low opacities to the standard muted alpha
    def bump_opacity(match: re.Match) -> str:
        value = float(match.group(2))
        if value < LOW_OPACITY_THRESHOLD:
            return f"{match.group(1)}{STANDARD_MUTED_ALPHA}{match.group(3)}"
        return match.group(0)

    content = OPACITY_RE.sub(bump_opacity, content)
    # Ensure canvas/background opacity is reduced for dark icons (avoid full black canvas)
    # Replace common opaque canvas fill (#1f1f1f) used by generator to a muted canvas with 0.35 alpha in style blocks
    def replace_full_opacity(match: re.Match) -> str:
        return f"{match.group(1)}{STANDARD_MUTED_ALPHA}{match.group(3)}"

    content = re.sub(r'(opacity\s*[:=]\s*["\']?)(1(?:\.0+)?)(["\']?)', replace_full_opacity, content, flags=re.IGNORECASE)

    # For any very-dark or near-black fill used as outline, nudge it lighter to preserve shape on dark background
    def lift_dark_hex(match: re.Match) -> str:
        hx = match.group(0)
        try:
            r, g, b = hex_to_rgb(hx)
        except Exception:
            return hx
        lum = perceived_luminance(r, g, b)
        if lum < 0.07:
            # lift lightness slightly in HSL
            h, s, l = rgb_to_hsl(r, g, b)
            new_l = min(0.18, max(0.06, l + 0.12))
            nr, ng, nb = hsl_to_rgb(h, s, new_l)
            return rgb_to_hex(nr, ng, nb)
        return hx

    content = HEX_RE.sub(lift_dark_hex, content)
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
    # Learn mappings from existing pairs and extend LIGHT_TO_DARK
    learned = learn_mappings(args.assets_root)
    if learned:
        print(f"Learned {len(learned)} light->dark color mappings from existing assets")
        # merge, prefer explicit LIGHT_TO_DARK keys
        for k, v in learned.items():
            if k not in LIGHT_TO_DARK:
                LIGHT_TO_DARK[k] = v
    # learn role-based mappings
    global ROLE_MAPPINGS
    ROLE_MAPPINGS = learn_role_mappings(args.assets_root)
    if ROLE_MAPPINGS:
        print(f"Learned {len(ROLE_MAPPINGS)} role->dark mappings from existing assets")
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
