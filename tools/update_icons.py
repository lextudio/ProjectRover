#!/usr/bin/env python3
"""
Update all Rover app icons (.ico and .icns) from a single PNG source.

Requirements:
- ImageMagick `magick` CLI available on PATH (used for both .ico and .icns)

Usage (from repo root):
  python ProjectRover/tools/update_icons.py --png ProjectRover/image.png

Defaults:
- Source PNG: ProjectRover/image.png
- Output ICO: ProjectRover/src/ProjectRover/Assets/projectrover-logo.ico
- Output ICNS: ProjectRover/build/macos/projectrover.icns
"""

import argparse
import shutil
import subprocess
import sys
from pathlib import Path
from typing import Tuple


def run_magick(args: list[str]):
    cmd = ["magick"] + args
    try:
        subprocess.run(cmd, check=True)
    except FileNotFoundError:
        sys.exit("magick not found. Install ImageMagick and ensure `magick` is on PATH.")
    except subprocess.CalledProcessError as e:
        sys.exit(f"magick command failed: {' '.join(cmd)} -> {e}")


def make_ico(src: Path, dest: Path):
    dest.parent.mkdir(parents=True, exist_ok=True)
    run_magick([str(src), "-define", "icon:auto-resize=256,128,64,48,32,24,16", str(dest)])
    print(f"[ok] wrote ICO: {dest}")


def make_icns(src: Path, dest: Path):
    dest.parent.mkdir(parents=True, exist_ok=True)
    # Resize to 1024 and let ImageMagick generate all standard sizes
    run_magick([str(src), "-resize", "1024x1024", "-define", "icns:auto-resize=16,32,64,128,256,512,1024", str(dest)])
    print(f"[ok] wrote ICNS: {dest}")


def make_png_asset(src: Path, dest: Path, size: int = 256):
    dest.parent.mkdir(parents=True, exist_ok=True)
    # Resize source to a square PNG for app assets (default 256x256)
    run_magick([str(src), "-resize", f"{size}x{size}", str(dest)])
    print(f"[ok] wrote PNG asset: {dest} ({size}x{size})")


def make_iconset(src: Path, dest_dir: Path):
    dest_dir.mkdir(parents=True, exist_ok=True)
    # Standard macOS iconset sizes (with @2x variants). Filenames follow the iconset convention.
    sizes = [
        ("icon_16x16.png", 16),
        ("icon_16x16@2x.png", 32),
        ("icon_32x32.png", 32),
        ("icon_32x32@2x.png", 64),
        ("icon_128x128.png", 128),
        ("icon_128x128@2x.png", 256),
        ("icon_256x256.png", 256),
        ("icon_256x256@2x.png", 512),
        ("icon_512x512.png", 512),
        ("icon_512x512@2x.png", 1024),
    ]

    for name, px in sizes:
        out = dest_dir / name
        run_magick([str(src), "-resize", f"{px}x{px}", str(out)])
        print(f"[ok] wrote iconset PNG: {out}")


def identify_size(path: Path) -> Tuple[int, int]:
    try:
        out = subprocess.check_output(["magick", "identify", "-format", "%w %h", str(path)], stderr=subprocess.DEVNULL)
        w, h = out.decode().strip().split()
        return int(w), int(h)
    except FileNotFoundError:
        sys.exit("magick not found. Install ImageMagick and ensure `magick` is on PATH.")
    except Exception:
        raise


def replace_pngs(src: Path, root: Path, dry_run: bool = False):
    src = src.resolve()
    for p in sorted(root.rglob("*.png")):
        if p.resolve() == src:
            continue
        try:
            w, h = identify_size(p)
        except Exception:
            print(f"[skip] cannot identify size for {p}")
            continue
        print(f"[found] {p} -> target size {w}x{h}")
        if dry_run:
            continue
        # Resize source to match target and overwrite
        run_magick([str(src), "-resize", f"{w}x{h}", str(p)])
        print(f"[ok] replaced PNG: {p} ({w}x{h})")


def main():
    repo_root = Path(__file__).resolve().parents[2]
    default_png = repo_root / "ProjectRover" / "image.png"
    default_ico = repo_root / "ProjectRover" / "src" / "ProjectRover" / "Assets" / "projectrover-logo.ico"
    default_icns = repo_root / "ProjectRover" / "build" / "macos" / "projectrover.icns"

    parser = argparse.ArgumentParser(description="Update Rover icons from a PNG source.")
    parser.add_argument("--png", type=Path, default=default_png, help=f"Source PNG (default: {default_png})")
    parser.add_argument("--ico", type=Path, default=default_ico, help=f"Output ICO (default: {default_ico})")
    parser.add_argument("--icns", type=Path, default=default_icns, help=f"Output ICNS (default: {default_icns})")
    args = parser.parse_args()

    if not args.png.exists():
        sys.exit(f"Source PNG not found: {args.png}")

    make_ico(args.png, args.ico)
    make_icns(args.png, args.icns)
    # Also write a standard PNG asset for the app (used in UI/resources)
    try:
        asset_png = repo_root / "ProjectRover" / "src" / "ProjectRover" / "Assets" / "projectrover-logo.png"
        make_png_asset(args.png, asset_png, size=256)
    except Exception:
        print(f"[warn] failed to write PNG asset: {asset_png}")

    make_iconset(args.png, repo_root / "ProjectRover" / "build" / "macos" / "RoverIcon.iconset")

    project_root = repo_root / "ProjectRover"
    replace_pngs(args.png, project_root)
    print("[done] Icons updated. Rebuild the app/bundle to see changes.")


if __name__ == "__main__":
    main()
