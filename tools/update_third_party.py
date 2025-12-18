#!/usr/bin/env python3
"""Generate THIRD-PARTY-NOTICES.md from direct dependencies."""
from __future__ import annotations

import argparse
import difflib
import json
import sys
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path
from typing import Dict, List, Optional, Tuple
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen

from third_party_common import (
    ASSETS,
    CS_PROJ,
    FAMILIES_CFG,
    LICENSE_CACHE,
    MANUAL_DEPENDENCIES,
    MANUAL_SECTIONS,
    NOTICES,
    PROPS,
    RUNS_DIR,
    choose_family,
    clean_license_text,
    extract_github_owner,
    has_placeholders,
    indent_block,
    is_spdx_template,
    license_signature,
    load_assets,
    load_central_versions,
    load_direct_packages,
    load_families_config,
    load_org_config,
    pick_canonical_license,
    read_sections,
    resolve_packages,
)

SPDX_RAW = "https://raw.githubusercontent.com/spdx/license-list-data/main/text/"

SPDX_FALLBACKS = {
    "MIT": """The MIT License (MIT)

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.""",
    "Apache-2.0": """Apache License
Version 2.0, January 2004
http://www.apache.org/licenses/

TERMS AND CONDITIONS FOR USE, REPRODUCTION, AND DISTRIBUTION

1. Definitions.

   "License" shall mean the terms and conditions for use, reproduction,
   and distribution as defined by Sections 1 through 9 of this document.

   "Licensor" shall mean the copyright owner or entity authorized by
   the copyright owner that is granting the License.

   "Legal Entity" shall mean the union of the acting entity and all
   other entities that control, are controlled by, or are under common
   control with that entity. For the purposes of this definition,
   "control" means (i) the power, direct or indirect, to cause the
   direction or management of such entity, whether by contract or
   otherwise, or (ii) ownership of fifty percent (50%) or more of the
   outstanding shares, or (iii) beneficial ownership of such entity.

   "You" (or "Your") shall mean an individual or Legal Entity
   exercising permissions granted by this License.

   "Source" form shall mean the preferred form for making modifications,
   including but not limited to software source code, documentation
   source, and configuration files.

   "Object" form shall mean any form resulting from mechanical
   transformation or translation of a Source form, including but
   not limited to compiled object code, generated documentation,
   and conversions to other media types.

   "Work" shall mean the work of authorship, whether in Source or
   Object form, made available under the License, as indicated by a
   copyright notice that is included in or attached to the work
   (an example is provided in the Appendix below).

   "Derivative Works" shall mean any work, whether in Source or Object
   form, that is based on (or derived from) the Work and for which the
   editorial revisions, annotations, elaborations, or other modifications
   represent, as a whole, an original work of authorship. For the purposes
   of this License, Derivative Works shall not include works that remain
   separable from, or merely link (or bind by name) to the interfaces of,
   the Work and Derivative Works thereof.

   "Contribution" shall mean any work of authorship, including
   the original version of the Work and any modifications or additions
   to that Work or Derivative Works thereof, that is intentionally
   submitted to Licensor for inclusion in the Work by the copyright owner
   or by an individual or Legal Entity authorized to submit on behalf of
   the copyright owner. For the purposes of this definition, "submitted"
   means any form of electronic, verbal, or written communication sent
   to the Licensor or its representatives, including but not limited to
   communication on electronic mailing lists, source code control systems,
   and issue tracking systems that are managed by, or on behalf of, the
   Licensor for the purpose of discussing and improving the Work, but
   excluding communication that is conspicuously marked or otherwise
   designated in writing by the copyright owner as "Not a Contribution."

   "Contributor" shall mean Licensor and any individual or Legal Entity
   on behalf of whom a Contribution has been received by Licensor and
   subsequently incorporated within the Work.

2. Grant of Copyright License. Subject to the terms and conditions of
   this License, each Contributor hereby grants to You a perpetual,
   worldwide, non-exclusive, no-charge, royalty-free, irrevocable
   copyright license to reproduce, prepare Derivative Works of,
   publicly display, publicly perform, sublicense, and distribute the
   Work and such Derivative Works in Source or Object form.

3. Grant of Patent License. Subject to the terms and conditions of
   this License, each Contributor hereby grants to You a perpetual,
   worldwide, non-exclusive, no-charge, royalty-free, irrevocable
   (except as stated in this section) patent license to make, have made,
   use, offer to sell, sell, import, and otherwise transfer the Work,
   where such license applies only to those patent claims licensable
   by such Contributor that are necessarily infringed by their
   Contribution(s) alone or by combination of their Contribution(s)
   with the Work to which such Contribution(s) was submitted. If You
   institute patent litigation against any entity (including a
   cross-claim or counterclaim in a lawsuit) alleging that the Work
   or a Contribution incorporated within the Work constitutes direct
   or contributory patent infringement, then any patent licenses
   granted to You under this License for that Work shall terminate
   as of the date such litigation is filed.

4. Redistribution. You may reproduce and distribute copies of the
   Work or Derivative Works thereof in any medium, with or without
   modifications, and in Source or Object form, provided that You
   meet the following conditions:

   (a) You must give any other recipients of the Work or
       Derivative Works a copy of this License; and

   (b) You must cause any modified files to carry prominent notices
       stating that You changed the files; and

   (c) You must retain, in the Source form of any Derivative Works
       that You distribute, all copyright, patent, trademark, and
       attribution notices from the Source form of the Work,
       excluding those notices that do not pertain to any part of
       the Derivative Works; and

   (d) If the Work includes a "NOTICE" text file as part of its
       distribution, then any Derivative Works that You distribute must
       include a readable copy of the attribution notices contained
       within such NOTICE file, excluding those notices that do not
       pertain to any part of the Derivative Works, in at least one
       of the following places: within a NOTICE text file distributed
       as part of the Derivative Works; within the Source form or
       documentation, if provided along with the Derivative Works; or,
       within a display generated by the Derivative Works, if and
       wherever such third-party notices normally appear. The contents
       of the NOTICE file are for informational purposes only and
       do not modify the License. You may add Your own attribution
       notices within Derivative Works that You distribute, alongside
       or as an addendum to the NOTICE text from the Work, provided
       that such additional attribution notices cannot be construed
       as modifying the License.

5. Submission of Contributions. Unless You explicitly state otherwise,
   any Contribution intentionally submitted for inclusion in the Work
   by You to the Licensor shall be under the terms and conditions of
   this License, without any additional terms or conditions.
   Notwithstanding the above, nothing herein shall supersede or modify
   the terms of any separate license agreement you may have executed
   with Licensor regarding such Contributions.

6. Trademarks. This License does not grant permission to use the trade
   names, trademarks, service marks, or product names of the Licensor,
   except as required for reasonable and customary use in describing the
   origin of the Work and reproducing the content of the NOTICE file.

7. Disclaimer of Warranty. Unless required by applicable law or
   agreed to in writing, Licensor provides the Work (and each
   Contributor provides its Contributions) on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or
   implied, including, without limitation, any warranties or conditions
   of TITLE, NON-INFRINGEMENT, MERCHANTABILITY, or FITNESS FOR A
   PARTICULAR PURPOSE. You are solely responsible for determining the
   appropriateness of using or redistributing the Work and assume any
   risks associated with Your exercise of permissions under this License.

8. Limitation of Liability. In no event and under no legal theory,
   whether in tort (including negligence), contract, or otherwise,
   unless required by applicable law (such as deliberate and grossly
   negligent acts) or agreed to in writing, shall any Contributor be
   liable to You for damages, including any direct, indirect, special,
   incidental, or consequential damages of any character arising as a
   result of this License or out of the use or inability to use the
   Work (including but not limited to damages for loss of goodwill,
   work stoppage, computer failure or malfunction, or any and all
   other commercial damages or losses), even if such Contributor
   has been advised of the possibility of such damages.

9. Accepting Warranty or Additional Liability. While redistributing
   the Work or Derivative Works thereof, You may choose to offer,
   and charge a fee for, acceptance of support, warranty, indemnity,
   or other liability obligations and/or rights consistent with this
   License. However, in accepting such obligations, You may act only
   on Your own behalf and on Your sole responsibility, not on behalf
   of any other Contributor, and only if You agree to indemnify,
   defend, and hold each Contributor harmless for any liability
   incurred by, or claims asserted against, such Contributor by reason
   of your accepting any such warranty or additional liability.""",
}

LICENSE_FILE_NAMES = [
    "license",
    "license.txt",
    "license.md",
    "copying",
    "copying.txt",
    "licence",
    "licence.txt",
]


def http_get_text(url: str, timeout: int = 10) -> Optional[str]:
    try:
        req = Request(url, headers={"User-Agent": "third-party-notices"})
        with urlopen(req, timeout=timeout) as resp:
            return resp.read().decode("utf-8", errors="replace")
    except (URLError, HTTPError):
        return None


def _parse_nuspec_metadata(root: ET.Element) -> Dict:
    info: Dict[str, Optional[str]] = {}
    meta = None
    for child in root:
        if child.tag.endswith("metadata"):
            meta = child
            break
    if meta is None:
        return info
    for child in meta:
        tag = child.tag.split("}")[-1]
        if tag == "license":
            info["license"] = (child.text or "").strip()
            info["license_type"] = child.get("type") or ""
        elif tag == "licenseUrl":
            info["license_url"] = (child.text or "").strip()
        elif tag == "repository":
            info["repository"] = child.get("url") or child.get("repositoryUrl")
        elif tag == "authors":
            info["authors"] = (child.text or "").strip()
        elif tag == "projectUrl":
            info["project_url"] = (child.text or "").strip()
    if info.get("license_url", "").startswith("http"):
        info["license_url"] = info["license_url"]
    return info


def read_nuspec(nuspec_path: Path) -> Dict:
    info: Dict[str, Optional[str]] = {}
    if not nuspec_path or not nuspec_path.exists():
        return info
    try:
        tree = ET.parse(nuspec_path)
        root = tree.getroot()
        return _parse_nuspec_metadata(root)
    except Exception:
        return info


def read_nuspec_from_zip(nupkg_path: Path) -> Dict:
    if not nupkg_path or not nupkg_path.exists():
        return {}
    try:
        with zipfile.ZipFile(nupkg_path, "r") as zf:
            for name in zf.namelist():
                if name.lower().endswith(".nuspec"):
                    with zf.open(name) as fh:
                        content = fh.read()
                    try:
                        root = ET.fromstring(content)
                        return _parse_nuspec_metadata(root)
                    except Exception:
                        return {}
    except Exception:
        return {}
    return {}


def find_license_in_folder(package_path: Path, hint: Optional[str] = None) -> Tuple[Optional[str], Optional[str]]:
    if not package_path or not package_path.exists():
        return None, None
    if hint:
        candidate = package_path / hint
        if candidate.exists():
            return candidate.read_text(encoding="utf-8", errors="replace"), f"file:{candidate}"
    lowered_targets = {n.lower() for n in LICENSE_FILE_NAMES}
    for entry in package_path.iterdir():
        if entry.is_file() and entry.name.lower() in lowered_targets:
            return entry.read_text(encoding="utf-8", errors="replace"), f"file:{entry}"
    for entry in package_path.iterdir():
        if entry.is_dir():
            for sub in entry.iterdir():
                if sub.is_file() and sub.name.lower() in lowered_targets:
                    return sub.read_text(encoding="utf-8", errors="replace"), f"file:{sub}"
    return None, None


def extract_license_from_nupkg(nupkg_path: Path, hint: Optional[str] = None) -> Tuple[Optional[str], Optional[str]]:
    if not nupkg_path or not nupkg_path.exists():
        return None, None
    try:
        with zipfile.ZipFile(nupkg_path, "r") as zf:
            names = zf.namelist()
            candidates: List[str] = []
            if hint:
                for n in names:
                    if n.lower().endswith(hint.lower()):
                        candidates.append(n)
                        break
            if not candidates:
                for n in names:
                    base = n.split("/")[-1].lower()
                    if base in LICENSE_FILE_NAMES:
                        candidates.append(n)
            for c in candidates:
                with zf.open(c) as fh:
                    return fh.read().decode("utf-8", errors="replace"), f"zip:{nupkg_path}:{c}"
    except Exception:
        return None, None
    return None, None


def fetch_spdx(license_id: str, allow_web: bool) -> Optional[str]:
    if not license_id:
        return None
    lic = (
        license_id.split(" OR ")[0]
        .split(" AND ")[0]
        .strip()
        .strip("() ")
    )
    if lic in SPDX_FALLBACKS:
        return SPDX_FALLBACKS[lic]
    if not allow_web:
        return None
    url = SPDX_RAW + lic + ".txt"
    return http_get_text(url)


def acquire_license(pkg_id: str, version: str, package_path: Path, allow_web: bool, force_refresh: bool) -> Tuple[Optional[str], Optional[str], Optional[str], Optional[Path]]:
    cache_path = LICENSE_CACHE / f"{pkg_id}-{version}.txt"
    if cache_path.exists() and not force_refresh:
        cached = cache_path.read_text(encoding="utf-8", errors="replace")
        if cached and not has_placeholders(cached) and not is_spdx_template(cached):
            return clean_license_text(cached), "cache", None, cache_path
    nuspec_info: Dict = {}
    text: Optional[str] = None
    source: Optional[str] = None
    repo_url: Optional[str] = None

    nuspec_file = package_path / f"{pkg_id}.nuspec" if package_path else None
    if nuspec_file and not nuspec_file.exists():
        nuspec_candidates = list(package_path.glob("*.nuspec")) if package_path else []
        if nuspec_candidates:
            nuspec_file = nuspec_candidates[0]
    if nuspec_file and nuspec_file.exists():
        nuspec_info = read_nuspec(nuspec_file)

    if package_path:
        t, src = find_license_in_folder(package_path, nuspec_info.get("license"))
        if t:
            text, source = t, src

    nupkg_path = None
    if not text and package_path and version:
        nupkg_path = package_path / f"{pkg_id.lower()}.{version.lower()}.nupkg"
        t, src = extract_license_from_nupkg(nupkg_path, nuspec_info.get("license"))
        if t:
            text, source = t, src
        if not nuspec_info:
            nuspec_info = read_nuspec_from_zip(nupkg_path)

    if not text and nuspec_info.get("license_type") == "expression":
        text = fetch_spdx(nuspec_info.get("license"), allow_web)
        source = source or "spdx-expression"

    if not text and allow_web and nuspec_info.get("license_url"):
        text = http_get_text(nuspec_info["license_url"])
        source = source or nuspec_info.get("license_url")

    if not text and allow_web and nuspec_info.get("repository"):
        repo_url = nuspec_info.get("repository")
        text = http_get_text(repo_url.rstrip("/") + "/blob/master/LICENSE?plain=1")
        if not text:
            text = http_get_text(repo_url.rstrip("/") + "/blob/main/LICENSE?plain=1")
        if text:
            source = source or repo_url
    repo_url = repo_url or nuspec_info.get("repository")

    if text:
        cleaned = clean_license_text(text)
        if cleaned and not has_placeholders(cleaned) and not is_spdx_template(cleaned):
            cache_path.parent.mkdir(parents=True, exist_ok=True)
            cache_path.write_text(cleaned, encoding="utf-8")
            return cleaned, source, repo_url, cache_path
    return None, source, repo_url, None


def load_manual_packages() -> List[Dict]:
    manual: List[Dict] = []
    for entry in MANUAL_DEPENDENCIES:
        lic_path = entry.get("license_path")
        text = None
        if lic_path and Path(lic_path).exists():
            text = clean_license_text(Path(lic_path).read_text(encoding="utf-8", errors="replace"))
        for pkg in entry.get("packages", []):
            manual.append(
                {
                    "id": pkg,
                    "version": "local",
                    "family": entry.get("family"),
                    "license_text": text,
                    "source": str(lic_path) if lic_path else None,
                    "package_path": str(lic_path.parent) if lic_path else None,
                }
            )
    return manual


def build_sections(packages: List[Dict]) -> Tuple[Dict[str, str], List[Dict]]:
    family_map: Dict[str, List[Dict]] = {}
    for pkg in packages:
        fam = pkg["family"]
        family_map.setdefault(fam, []).append(pkg)
    sections: Dict[str, str] = {}
    warnings: List[Dict] = []
    org_lookup = { (entry.get("name") or entry.get("id")): entry for entry in load_org_config() }
    for fam, pkgs in sorted(family_map.items(), key=lambda kv: kv[0].lower()):
        texts = [p.get("license_text") or "" for p in pkgs]
        org_entry = org_lookup.get(fam)
        canonical_text, warn = pick_canonical_license(texts, org_entry)
        if warn:
            warnings.append({"family": fam, "variants": warn, "packages": [p["id"] for p in pkgs]})
        sections[fam] = indent_block(canonical_text.strip()) + "\n"
    return sections, warnings


def write_notices(path: Path, preamble: str, sections: Dict[str, str]) -> None:
    order = sorted(sections.keys(), key=str.lower)
    out_lines: List[str] = []
    if preamble.strip():
        out_lines.append(preamble.rstrip())
        out_lines.append("")
    for name in order:
        out_lines.append(f"## {name}")
        out_lines.append("")
        out_lines.append(sections[name].rstrip())
        out_lines.append("")
    path.write_text("\n".join(out_lines).rstrip() + "\n", encoding="utf-8")


def sync_families_config(path: Path, family_packages: Dict[str, List[str]]) -> None:
    data = {"version": "1.0", "families": []}
    for fam, pkgs in sorted(family_packages.items(), key=lambda kv: kv[0].lower()):
        data["families"].append({"name": fam, "retain": True, "packages": sorted(pkgs)})
    path.write_text(json.dumps(data, indent=2), encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description="Update THIRD-PARTY-NOTICES.md.")
    parser.add_argument("--csproj", type=Path, default=CS_PROJ)
    parser.add_argument("--props", type=Path, default=PROPS)
    parser.add_argument("--assets", type=Path, default=ASSETS)
    parser.add_argument("--notices", type=Path, default=NOTICES)
    parser.add_argument("--trace", type=Path, help="Write trace to this path (default .cache/update_trace.json).")
    parser.add_argument("--allow-web", action="store_true", help="Allow fetching licenseUrl/repository/SPDX over HTTP.")
    parser.add_argument("--force-refresh", action="store_true", help="Ignore cached license files.")
    parser.add_argument("--dry-run", action="store_true", help="Show planned changes without writing files.")
    parser.add_argument("--no-sync-families", action="store_true", help="Do not rewrite third-party-families.json.")
    parser.add_argument("--package", help="Update only the specified package (incremental mode).")
    args = parser.parse_args()

    import datetime

    # Prepare run directories and default trace location
    RUNS_DIR.mkdir(parents=True, exist_ok=True)
    timestamp = datetime.datetime.utcnow().strftime("%Y%m%dT%H%M%SZ")
    run_dir = RUNS_DIR / timestamp
    run_dir.mkdir(parents=True, exist_ok=True)
    trace_path = args.trace or (Path(".cache") / "update_trace.json")

    direct_packages = load_direct_packages(args.csproj)
    if not direct_packages:
        print(f"No PackageReference entries found in {args.csproj}", file=sys.stderr)
        return 1

    central_versions = load_central_versions(args.props)
    assets = load_assets(args.assets)
    resolved = resolve_packages(direct_packages, central_versions, assets)

    families_cfg, package_to_family = load_families_config(FAMILIES_CFG)
    orgs = load_org_config()

    packages: List[Dict] = []
    missing: List[str] = []

    # Manual dependencies from project references
    for manual_pkg in load_manual_packages():
        if not manual_pkg.get("license_text"):
            missing.append(f"{manual_pkg['id']} (manual license missing)")
        packages.append(manual_pkg)

    target_packages = direct_packages
    package_mode = False
    if getattr(args, "package", None):
        package_mode = True
        if args.package not in direct_packages:
            print(f"Warning: {args.package} is not a direct PackageReference; continuing anyway.", file=sys.stderr)
        target_packages = [args.package]

    for pkg in target_packages:
        info = resolved.get(pkg, {})
        version = info.get("version")
        package_path = info.get("package_path")
        if not version:
            missing.append(f"{pkg} (version not resolved)")
            continue
        text, source, repo_url, cache_path = acquire_license(pkg, version, package_path, args.allow_web, args.force_refresh)
        if not text:
            missing.append(f"{pkg} {version}")
            continue
        owner = extract_github_owner(repo_url)
        packages.append(
            {
                "id": pkg,
                "version": version,
                "license_text": text,
                "source": source,
                "package_path": str(package_path) if package_path else None,
                "cache_path": str(cache_path) if cache_path else None,
                "repository": repo_url,
                "owner": owner,
                "family": choose_family(pkg, owner, package_to_family, orgs),
            }
        )

    if missing:
        print("Missing licenses for:", file=sys.stderr)
        for m in missing:
            print(f" - {m}", file=sys.stderr)
        return 2

    sections, warnings = build_sections(packages)

    # Retain manual non-package sections if present.
    preamble, existing_sections = read_sections(args.notices)
    # Save current notices snapshot for troubleshooting
    (run_dir / "current_notices.md").write_text(
        existing_sections and args.notices.read_text(encoding="utf-8") or "", encoding="utf-8"
    )
    for name in MANUAL_SECTIONS:
        if name in existing_sections and name not in sections:
            sections[name] = existing_sections[name]

    new_text_lines = []
    order = sorted(sections.keys(), key=str.lower)
    if preamble.strip():
        new_text_lines.append(preamble.rstrip())
        new_text_lines.append("")
    for name in order:
        new_text_lines.append(f"## {name}")
        new_text_lines.append("")
        new_text_lines.append(sections[name].rstrip())
        new_text_lines.append("")
    new_text = "\n".join(new_text_lines).rstrip() + "\n"

    # Persist planned notices for troubleshooting
    (run_dir / "planned_notices.md").write_text(new_text, encoding="utf-8")

    if args.dry_run:
        current = args.notices.read_text(encoding="utf-8") if args.notices.exists() else ""
        diff = difflib.unified_diff(
            current.splitlines(keepends=True),
            new_text.splitlines(keepends=True),
            fromfile=str(args.notices) + " (current)",
            tofile=str(args.notices) + " (planned)",
        )
        print("".join(diff))
    else:
        write_notices(args.notices, preamble, sections)
        print(f"Updated {args.notices}")

    # Sync family config unless disabled.
    family_packages: Dict[str, List[str]] = {}
    for pkg in packages:
        family_packages.setdefault(pkg["family"], []).append(pkg["id"])
    if not args.no_sync_families:
        sync_families_config(FAMILIES_CFG, family_packages)

    diag = {
        "packages": packages,
        "warnings": warnings,
        "family_packages": family_packages,
        "notices": str(args.notices),
        "assets_path": str(args.assets),
        "allow_web": args.allow_web,
        "run_dir": str(run_dir),
        "timestamp": timestamp,
        "dry_run": bool(args.dry_run),
    }
    # Write trace both to requested path (or default) and per-run folder
    trace_path.parent.mkdir(parents=True, exist_ok=True)
    trace_path.write_text(json.dumps(diag, indent=2), encoding="utf-8")
    (run_dir / "trace.json").write_text(json.dumps(diag, indent=2), encoding="utf-8")
    print(f"Wrote trace to {trace_path} and {run_dir/'trace.json'}")

    if warnings:
        print("Warnings: multiple license variants detected in families:", file=sys.stderr)
        for w in warnings:
            variants = w.get("variants") or w.get("details")
            print(f" - {w.get('family')}: {variants}", file=sys.stderr)

    return 0


if __name__ == "__main__":
    sys.exit(main())
