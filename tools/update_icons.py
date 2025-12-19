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
    print("[done] Icons updated. Rebuild the app/bundle to see changes.")


if __name__ == "__main__":
    main()
