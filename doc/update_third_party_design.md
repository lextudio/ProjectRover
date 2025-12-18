Design: THIRD-PARTY Notices Updater
===================================

Overview
--------
This document records the design rationale, implementation details, and operational behaviors for `tools/update_third_party.py` — the script used to generate and maintain `THIRD-PARTY-NOTICES.md` from direct NuGet `PackageReference` entries in `src/ProjectRover/ProjectRover.csproj`.

Goals
-----
- Produce a clean `THIRD-PARTY-NOTICES.md` that lists third-party dependencies grouped into families.
- Use a single, canonical license text per family when possible.
- Avoid grouping packages into a family purely by name; require license equality when merging suffixes.
- Detect placeholder and SPDX-template license artifacts and avoid including them in notices.
- Persist and manage family mappings in `third-party-families.json` and owner mappings in `third-party-orgs.json`.
- Provide a trace file (`.cache/update_trace.json`) with per-package grouping reasons and license diagnostics.
- Support single-package updates safely and use a 24-hour cache of license files to minimize network calls.

High-level Flow
---------------
1. Parse `ProjectRover.csproj` for `PackageReference` items. Resolve versions using `Directory.Packages.props` when necessary.
2. Build a `pkg_infos` map for every package to be processed.
3. For each package, try to obtain license text using these sources (in order of preference):
   - Fresh cached license in `.cache/licenses/{pkg}-{ver}.txt` (< 24 hours)
   - Local `.nupkg` in `~/.nuget/packages` if available (extract LICENSE/COPYING from the package)
   - NuGet registration `packageContent` (download `.nupkg`) or flat container URL
   - nuspec `licenseUrl` or `repository` metadata (prefer GitHub repo via GitHub API `/repos/:owner/:repo/license`)
   - SPDX license text by expression (fallback when license expression is present and not a template)

   If any retrieved text looks like an SPDX *template* or contains placeholders (e.g., `<copyright holders>`, `replaceable-license-text`), it is rejected and not cached.

4. Group packages into families using `group_family(pkg_id, info)`, a heuristic function that consults `third-party-orgs.json` and a set of prefix rules.
5. Post-process grouping with hierarchical suffix-merge: for packages that remain their own family but have a dot-separated name (A.B.C), attempt to merge them into a shorter base (A.B or A) only if both package and base have identical normalized license texts.
6. Build `section_texts` per family:
   - Normalize license texts via `clean_license_text()` which strips HTML, removes SPDX preamble/footers, and collapses whitespace.
   - Group by normalized license text signatures (SHA256 prefix).
   - If a family has multiple distinct concrete license texts, record a `license_warnings` entry. Section rendering chooses a single canonical license text per family (details below).
7. Update `third-party-families.json` — new families are appended unless running in dry-run.
8. Write `THIRD-PARTY-NOTICES.md` replacing/adding family sections. The script is conservative about removing unrelated sections in single-package update mode.
9. Write trace diagnostics to `.cache/update_trace.json`, including `pkg_infos`, `package_to_section`, `sections`, `families_cfg_preview`, `final_section_order`, and `license_warnings`.

Key Implementation Details
--------------------------
Files and Locations
- `tools/update_third_party.py` — main script.
- `.cache/licenses/` — per-package license cache (files named `{PackageId}-{Version}.txt`).
- `.cache/sections.json` — package->section and sections mapping cache.
- `.cache/update_trace.json` — comprehensive trace diagnostics written when `--trace` is used.
- `third-party-families.json` — repo-level, persistent family definitions.
- `third-party-orgs.json` — owner/org mapping for preferred family naming.

Important Functions
- `read_project_refs(csproj_path)` — parse `PackageReference` tokens from csproj fallback.
- `get_package_base_address()` — discovers NuGet flat container base (v3 Service Index fallback).
- `fetch_github_license(repo_url)` — uses GitHub API `/repos/:owner/:repo/license` (if token present, uses it) with retry/backoff; falls back to raw `raw.githubusercontent.com` attempts.
- `extract_license_from_nupkg(nupkg_path)` — opens nupkg and returns contents of license-like entries.
- `read_nuspec_from_nupkg(nupkg_path)` — reads nuspec metadata to extract `license`, `licenseUrl`, `repository`.
- `contains_placeholders(text)` — returns True if text contains `<year>`, `<copyright holders>`, or common placeholder tokens.
- `is_spdx_template(text)` — heuristic to detect SPDX template/boilerplate fragments; used to avoid caching them and to remove them from cache when discovered.
- `clean_license_text(text)` — strip HTML and SPDX metadata, try to find license body markers (`mit license`, `permission is hereby granted`, `copyright (c)`), normalize whitespace.
- `group_family(pkg_id, info)` — heuristic grouping function that uses hard-coded prefixes and `third-party-orgs.json` to suggest family names. It returns `(family, reason)` where `reason` is a string for traceability.

Cache and Freshness
-------------------
- License files under `.cache/licenses` are considered "fresh" for 24 hours. When a fresh cache file exists, the script uses it and skips network queries for that package.
- If a fresh cache file is an SPDX template (detected via `is_spdx_template`), it is deleted and the script proceeds to fetch a concrete license.
- When writing new cache files, the script avoids caching SPDX templates and will attempt other sources.

Family Merging & Canonical License Selection
--------------------------------------------
- Hierarchical suffix merge: For a package that is its own family (the package id equals family) and contains dots, the script tries progressively shorter bases (right-to-left) — e.g., for `A.B.C` it tries `A.B` then `A` — and merges into the first base that exists in `sections` and whose normalized license text (post-`clean_license_text`) is identical to the child's.
- License grouping per family: the script groups all per-package license texts by normalized text signature (SHA256 prefix). If all packages have the same normalized text, that text is used as the family's canonical license.
- If multiple distinct concrete license texts remain in a family, a `license_warnings` entry is recorded and included in the trace. The rendering logic selects a single canonical license for the family using the following priority:
  1. Prefer a license text that is NOT an SPDX template/boilerplate.
  2. Prefer a license text that contains an explicit copyright notice (e.g., "Copyright (c) 2024").
  3. Prefer the license text that appears most frequently among the packages in the family.
  - The selected canonical license is rendered alone; individual package lists are not included in the output section body.

Rendering Requirements Applied
------------------------------
Per your instructions, we enforce these formatting/behavior rules:
- Indentation: license bodies are indented with 4 spaces.
- Alphabetical order of sections: the script sorts family section names case-insensitively.
- Exclude transitive dependencies: only direct `PackageReference` entries from `ProjectRover.csproj` are processed (plus manually added local dependencies like `ILSpy` and `AvaloniaEdit` where packaged).
- No SPDX templates in notices: templates are removed and not cached.
- Validation: `tools/check_third_party.py` validates the output file against these rules and ensures that families defined in `third-party-families.json` are correctly grouped (i.e., no individual package sections exist if a family grouping is defined).

Warnings & Diagnostics
----------------------
- `license_warnings`: collected list of families where multiple distinct concrete license texts exist. Each warning includes the family, reason, and per-signature details (sig, packages list, version hint, snippet).
- Warnings are not printed mid-run; after writing files the updater prints a concise summary of warnings to stderr and points to `.cache/update_trace.json` for full details.

Single-Package Safety
---------------------
- When invoked with `--package X`, the script only processes that package, captures `THIRD-PARTY-NOTICES.md` pre-change sections, and refuses to write changes if the single-package update would remove unrelated existing sections. This avoids accidental mass edits.

Edge Cases & Known Limitations
------------------------------
- GitHub raw URL layout and branch differences cause many 404s. Using a `GITHUB_TOKEN` (exported as `GITHUB_TOKEN` or `GH_TOKEN`) significantly improves success of `fetch_github_license()` by calling the API endpoint.
- Some projects host their license in non-standard locations; the script tries common names (`LICENSE`, `COPYING`, etc.) but may miss some.
- When family grouping heuristics are imperfect, maintainers can edit `third-party-families.json` or `third-party-orgs.json` to force desired grouping.

Operational instructions
------------------------
Run a full update (writes files):

```bash
python3 tools/update_third_party.py --trace .cache/update_trace.json
```

Run a dry-run (shows planned diffs):

```bash
python3 tools/update_third_party.py --dry-run --trace .cache/update_trace.json
```

Run for a single package (safe-checks applied):

```bash
python3 tools/update_third_party.py --package Microsoft.DiaSymReader --trace .cache/update_trace.json
```

If you have a GitHub token (recommended to increase license-detection success), export it first:

```bash
export GITHUB_TOKEN="<your token>"
python3 tools/update_third_party.py --trace .cache/update_trace.json
```

Files to review after a run
- `THIRD-PARTY-NOTICES.md` — the generated notices file.
- `third-party-families.json` — the repo-managed family definitions.
- `.cache/update_trace.json` — diagnostic trace with `pkg_infos` entries and `license_warnings` for triage.

Design decisions & rationale
----------------------------
- Conservative updates: preserve existing sections unless a clear family mapping and license text indicate replacement. Single-package updates are restricted to prevent accidental removals.
- SPDX-template detection and deletion: SPDX raw pages and templates are not suitable as license texts in notices — they often contain placeholders and HTML. Deleting them from cache reduces noise and forces the script to preferentially use actual LICENSE files.
- Hierarchical suffix merging with license check: avoids grouping packages just because their names share prefixes. License equality is required before merging to avoid hiding license differences.
- Trace-first debugging: `--trace` writes comprehensive diagnostics that make it simple to audit how each package was grouped and which license was chosen.

Potential future improvements
-----------------------------
- Better SPDX-template normalization to salvage useful license body content when it exists; e.g., automatically substitute placeholders using nuspec authors/year if safe.
- Add a set of overrides in the repo config to mark certain SPDX-derived texts as "acceptable" for caching if maintainers confirm them.
- Implement a smaller per-family canonicalization strategy (e.g., prefer the license text from a canonical package listed in `third-party-families.json`) so the output always uses a single agreed source.
- Attempt to fetch license files via GitHub's GraphQL API or via a small delay-retry loop to reduce 404s caused by branch naming.

Appendix: Code pointers
-----------------------
- `clean_license_text()` — located in `tools/update_third_party.py`, search for function name.
- `is_spdx_template()` — located in `tools/update_third_party.py`.
- `group_family()` — heuristics live in the same script; update here to tweak family mapping rules.
- Trace writing — the script writes `.cache/update_trace.json` when `--trace` is provided.

If you want, next I can:
- Print the exact `license_warnings` block from `.cache/update_trace.json` for triage.
- Implement your requested final rendering rule: "one canonical license statement per family" (I can pick the most-common concrete license and drop others, with an entry in the trace showing what was dropped).
- Make `third-party-families.json` edits you prefer (manual cleanup).


— End of design.md
