#!/usr/bin/env python3
"""Shared helpers for third-party notice tooling."""
from __future__ import annotations

import html
import json
import re
import xml.etree.ElementTree as ET
from pathlib import Path
from typing import Dict, Iterable, List, Optional, Tuple

ROOT = Path(__file__).resolve().parents[1]
CS_PROJ = ROOT / "src" / "ProjectRover" / "ProjectRover.csproj"
PROPS = ROOT / "Directory.Packages.props"
ASSETS = ROOT / "src" / "ProjectRover" / "obj" / "project.assets.json"
NOTICES = ROOT / "THIRD-PARTY-NOTICES.md"
FAMILIES_CFG = ROOT / "third-party-families.json"
ORG_CFG = ROOT / "third-party-orgs.json"
LICENSE_CACHE = ROOT / ".cache" / "licenses"
RUNS_DIR = ROOT / ".cache" / "third_party_runs"

# Default grouping heuristics when no explicit mapping exists.
DEFAULT_FAMILY_PREFIXES: List[Tuple[str, str]] = [
    ("Avalonia.AvaloniaEdit", "AvaloniaEdit"),
    ("AvaloniaEdit", "AvaloniaEdit"),
    ("Avalonia", "Avalonia"),
    ("Dock.", "Dock.Avalonia"),
    ("ICSharpCode.", "ILSpy"),
    ("ILSpy", "ILSpy"),
    ("Microsoft.Extensions", "Microsoft.Extensions"),
    ("Microsoft.DiaSymReader", "Microsoft.DiaSymReader"),
    ("Serilog", "Serilog"),
    ("TomsToolbox", "TomsToolbox"),
    ("TextMateSharp", "TextMateSharp"),
]

MANUAL_DEPENDENCIES = [
    {
        "family": "ILSpy",
        "packages": ["ICSharpCode.Decompiler", "ICSharpCode.ILSpyX"],
        "license_path": ROOT / "src" / "ILSpy" / "LICENSE",
    },
    {
        "family": "AvaloniaEdit",
        "packages": ["Avalonia.AvaloniaEdit", "AvaloniaEdit.TextMate"],
        "license_path": ROOT / "src" / "AvaloniaEdit" / "LICENSE",
    },
]

# Sections that are allowed to stay even if not mapped to a package.
MANUAL_SECTIONS = {"MICROSOFT VISUAL STUDIO 2022 IMAGE LIBRARY"}


def load_central_versions(props_path: Path = PROPS) -> Dict[str, str]:
    versions: Dict[str, str] = {}
    if not props_path.exists():
        return versions
    try:
        tree = ET.parse(props_path)
        root = tree.getroot()
        for pv in root.findall(".//{*}PackageVersion"):
            inc = pv.get("Include")
            ver = pv.get("Version")
            if inc and ver:
                versions[inc] = ver
    except Exception:
        pass
    return versions


def load_direct_packages(csproj_path: Path = CS_PROJ) -> List[str]:
    packages: List[str] = []
    if not csproj_path.exists():
        return packages
    try:
        tree = ET.parse(csproj_path)
        root = tree.getroot()
        for pr in root.findall(".//{*}PackageReference"):
            inc = pr.get("Include")
            if inc and inc not in packages:
                packages.append(inc)
    except Exception:
        pass
    return packages


def load_assets(assets_path: Path = ASSETS) -> Dict:
    if not assets_path.exists():
        return {}
    try:
        return json.loads(assets_path.read_text(encoding="utf-8"))
    except Exception:
        return {}


def resolve_packages(packages: Iterable[str], central_versions: Dict[str, str], assets: Dict) -> Dict[str, Dict]:
    targets = next(iter((assets.get("targets") or {}).values()), {})
    pkg_folders = list((assets.get("packageFolders") or {}).keys())
    resolved: Dict[str, Dict] = {}
    for pkg in packages:
        version = None
        for key in targets.keys():
            if key.lower().startswith(pkg.lower() + "/"):
                version = key.split("/", 1)[1]
                break
        if version is None:
            version = central_versions.get(pkg)
        package_path = None
        if version and pkg_folders:
            package_path = Path(pkg_folders[0]) / pkg.lower() / version.lower()
        resolved[pkg] = {"version": version, "package_path": package_path}
    return resolved


def load_families_config(path: Path = FAMILIES_CFG) -> Tuple[Dict, Dict[str, str]]:
    data = {"version": "1.0", "families": []}
    package_to_family: Dict[str, str] = {}
    if path.exists():
        try:
            data = json.loads(path.read_text(encoding="utf-8"))
        except Exception:
            pass
    for entry in data.get("families", []):
        name = entry.get("name")
        for pkg in entry.get("packages") or []:
            if name:
                package_to_family[pkg] = name
    return data, package_to_family


def load_org_config(path: Path = ORG_CFG) -> List[Dict]:
    if not path.exists():
        return []
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
        return data.get("orgs", []) or []
    except Exception:
        return []


def extract_github_owner(repo_url: Optional[str]) -> Optional[str]:
    if not repo_url or "github.com" not in repo_url:
        return None
    try:
        url = repo_url
        if url.endswith(".git"):
            url = url[:-4]
        if "github.com/" not in url:
            return None
        tail = url.split("github.com/", 1)[1]
        owner = tail.split("/", 1)[0]
        return owner.lower()
    except Exception:
        return None


def map_owner_to_org(owner: Optional[str], orgs: List[Dict]) -> Optional[str]:
    if not owner:
        return None
    for entry in orgs:
        name = entry.get("name") or entry.get("id")
        for prefix in entry.get("github_prefixes", []) or []:
            pref_owner = prefix.rstrip("/").split("/")[-1].lower()
            if pref_owner == owner.lower():
                return name
    return None


def choose_family(pkg_id: str, repo_owner: Optional[str], package_to_family: Dict[str, str], orgs: List[Dict]) -> str:
    if pkg_id in package_to_family:
        return package_to_family[pkg_id]
    mapped = map_owner_to_org(repo_owner, orgs)
    if mapped:
        return mapped
    for manual in MANUAL_DEPENDENCIES:
        if pkg_id in manual["packages"]:
            return manual["family"]
    for prefix, fam in DEFAULT_FAMILY_PREFIXES:
        if pkg_id.startswith(prefix):
            return fam
    # heuristic: collapse dotted names to prefix if multiple tokens share same root
    if "." in pkg_id:
        root = pkg_id.split(".")[0]
        if root and any(pkg_id.startswith(root + ".") for root in [root]):
            return root
    return pkg_id


def pick_canonical_license(texts: List[str], org_entry: Optional[Dict] = None) -> Tuple[str, Dict]:
    """Pick a canonical license text from a list, considering org-specific aliases."""
    aliases = set()
    if org_entry:
        for alias in org_entry.get("license_aliases", []) or []:
            aliases.add(license_signature(alias))

    sig_map: Dict[str, Dict] = {}
    for txt in texts:
        sig = license_signature(txt or "")
        sig_map.setdefault(sig, {"text": txt or "", "count": 0, "alias": sig in aliases})
        sig_map[sig]["count"] += 1
    # warning if more than one signature
    warning = {}
    if len(sig_map) > 1:
        warning = {k: v["count"] for k, v in sig_map.items()}

    def score(item: Tuple[str, Dict]) -> Tuple[int, int, int, int]:
        sig, meta = item
        text = meta["text"]
        alias_bonus = 1 if meta.get("alias") else 0
        has_copyright = 1 if ("copyright" in text.lower()) else 0
        length = len(text)
        return (alias_bonus, meta["count"], has_copyright, length)

    canonical_sig, canonical_meta = max(sig_map.items(), key=score)
    return canonical_meta["text"], warning


def family_for_package(pkg_id: str, package_to_family: Dict[str, str] | None = None) -> str:
    if package_to_family and pkg_id in package_to_family:
        return package_to_family[pkg_id]
    for manual in MANUAL_DEPENDENCIES:
        if pkg_id in manual["packages"]:
            return manual["family"]
    for prefix, fam in DEFAULT_FAMILY_PREFIXES:
        if pkg_id.startswith(prefix):
            return fam
    return pkg_id


def looks_like_html(text: str) -> bool:
    sample = text.strip().lower()
    return "<html" in sample or sample.startswith("<!doctype") or "<body" in sample


def clean_license_text(text: str) -> str:
    if not text:
        return ""
    normalized = text.replace("\r\n", "\n")
    if looks_like_html(normalized):
        normalized = re.sub(r"<[^>]+>", " ", normalized)
    normalized = html.unescape(normalized)
    normalized = re.sub(r"[ \t]+", " ", normalized)
    normalized = re.sub(r"\n{3,}", "\n\n", normalized)
    return normalized.strip()


def license_signature(text: str) -> str:
    return re.sub(r"\s+", " ", clean_license_text(text).strip().lower())


def has_placeholders(text: str) -> bool:
    if not text:
        return False
    low = text.lower()
    placeholders = [
        "<year>",
        "<copyright",
        "<copyright holders>",
        "{year}",
        "yyyy",
        "replaceable-license-text",
    ]
    return any(p in low for p in placeholders)


def is_spdx_template(text: str) -> bool:
    if not text:
        return False
    low = text.lower()
    markers = [
        "spdx identifier",
        "data pulled from spdx",
        "replaceable-license-text",
        "licenses.nuget.org",
    ]
    return any(m in low for m in markers)


def indent_block(text: str, prefix: str = "    ") -> str:
    lines = [ln.lstrip() for ln in text.splitlines()]
    return "\n".join(prefix + ln for ln in lines)


def read_sections(path: Path = NOTICES) -> Tuple[str, Dict[str, str]]:
    if not path.exists():
        return "", {}
    text = path.read_text(encoding="utf-8")
    lines = text.splitlines()
    header_re = re.compile(r"^##\s+(.*)")
    preamble_lines: List[str] = []
    sections: Dict[str, str] = {}
    current = None
    buf: List[str] = []
    for line in lines:
        m = header_re.match(line)
        if m:
            if current is None:
                preamble_lines = buf[:]
            else:
                sections[current] = "\n".join(buf).strip("\n") + "\n"
            current = m.group(1).strip()
            buf = []
        else:
            buf.append(line)
    if current is not None:
        sections[current] = "\n".join(buf).strip("\n") + "\n"
    preamble = "\n".join(preamble_lines).strip()
    preamble = preamble + "\n" if preamble else ""
    return preamble, sections
