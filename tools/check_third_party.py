#!/usr/bin/env python3
"""Validate THIRD-PARTY-NOTICES.md."""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import Dict, List, Tuple

from third_party_common import (
    ASSETS,
    CS_PROJ,
    FAMILIES_CFG,
    MANUAL_DEPENDENCIES,
    MANUAL_SECTIONS,
    NOTICES,
    clean_license_text,
    family_for_package,
    has_placeholders,
    load_assets,
    load_central_versions,
    load_direct_packages,
    load_families_config,
    read_sections,
    resolve_packages,
)

KEYWORDS = ["license", "permission", "copyright", "apache", "mit", "bsd", "gpl"]


def check_alphabetical(titles: List[str]) -> Tuple[bool, List[str]]:
    expected = sorted(titles, key=str.lower)
    return titles == expected, expected


def contains_keyword(lines: List[str]) -> bool:
    joined = "\n".join(lines[:8]).lower()
    return any(k in joined for k in KEYWORDS)


def check_indentation(lines: List[str], prefix: str = "    ") -> Tuple[bool, str]:
    nonempty = [ln for ln in lines if ln.strip()]
    if not nonempty:
        return False, "empty body"
    for ln in nonempty:
        if not ln.startswith(prefix):
            return False, f"expected indentation with {repr(prefix)}"
    return True, ""


def expected_family_map() -> Dict[str, List[str]]:
    central_versions = load_central_versions()
    assets = load_assets()
    direct = load_direct_packages()
    resolved = resolve_packages(direct, central_versions, assets)
    _, package_to_family = load_families_config(FAMILIES_CFG)

    families: Dict[str, List[str]] = {}
    for pkg in direct:
        fam = family_for_package(pkg, package_to_family)
        families.setdefault(fam, []).append(pkg)
    for entry in MANUAL_DEPENDENCIES:
        fam = entry["family"]
        for pkg in entry.get("packages", []):
            families.setdefault(fam, []).append(pkg)
    return families


def main() -> int:
    parser = argparse.ArgumentParser(description="Check THIRD-PARTY-NOTICES.md rules.")
    parser.add_argument("--notices", type=Path, default=NOTICES)
    parser.add_argument("--trace", type=Path, help="Write diagnostics json to this path.")
    args = parser.parse_args()

    preamble, sections = read_sections(args.notices)
    titles = list(sections.keys())
    if not titles:
        print(f"No sections found in {args.notices}", file=sys.stderr)
        return 2

    errors: List[str] = []
    warnings: List[str] = []

    ok, expected_order = check_alphabetical(titles)
    if not ok:
        errors.append("Sections are not in alphabetical order.")

    for title, body in sections.items():
        lines = body.splitlines()
        if not contains_keyword(lines):
            errors.append(f"{title}: missing recognizable license header.")
        ind_ok, ind_reason = check_indentation(lines)
        if not ind_ok:
            errors.append(f"{title}: indentation issue ({ind_reason}).")
        cleaned = clean_license_text(body)
        if not cleaned:
            errors.append(f"{title}: empty license text.")
        if has_placeholders(cleaned):
            errors.append(f"{title}: contains placeholder copyright/year fields.")

    families = expected_family_map()
    expected_titles = set(families.keys()) | MANUAL_SECTIONS

    # Detect extra sections not mapped to direct dependencies or manual allowance.
    for title in titles:
        if title in expected_titles:
            continue
        # allow when a single-package family name equals package id
        if any(title in pkgs for pkgs in families.values()):
            continue
        errors.append(f"{title}: not mapped to direct dependencies.")

    # Detect missing families and grouping issues.
    title_set = set(titles)
    for fam, pkgs in families.items():
        present_members = [t for t in titles if t == fam or t in pkgs]
        if not present_members:
            warnings.append(f"{fam}: missing from notices.")
            continue
        members_without_family_name = [t for t in titles if t in pkgs and t != fam]
        if len(pkgs) > 1:
            if fam not in title_set and len(present_members) > 1:
                warnings.append(f"{fam}: multiple packages present but not grouped.")
            if fam in title_set and members_without_family_name:
                errors.append(f"{fam}: family header present alongside individual members.")

    diagnostics = {
        "preamble_lines": len(preamble.splitlines()),
        "sections_count": len(sections),
        "alphabetical_ok": ok,
        "expected_order": expected_order,
        "titles": titles,
        "errors": errors,
        "warnings": warnings,
    }

    if args.trace:
        args.trace.parent.mkdir(parents=True, exist_ok=True)
        args.trace.write_text(json.dumps(diagnostics, indent=2), encoding="utf-8")
        print(f"Wrote diagnostics to {args.trace}")

    if errors:
        print("ERRORS:", file=sys.stderr)
        for e in errors:
            print(f"- {e}", file=sys.stderr)
        if warnings:
            print("\nWARNINGS:", file=sys.stderr)
            for w in warnings:
                print(f"- {w}", file=sys.stderr)
        return 2

    if warnings:
        print("WARNINGS:")
        for w in warnings:
            print(f"- {w}")
        return 1

    print("OK: THIRD-PARTY-NOTICES.md passes checks.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
