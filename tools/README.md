# Third-Party Notice Tooling

This folder contains two cooperating scripts plus shared helpers for generating and validating `THIRD-PARTY-NOTICES.md`.

## Scripts
- `update_third_party.py`  
  - Full run: `python3 tools/update_third_party.py --allow-web --trace .cache/update_trace.json`  
  - Incremental (single package): `python3 tools/update_third_party.py --package Serilog --allow-web --trace .cache/update_trace.json`  
  - Options:  
    - `--allow-web` enables fetching licenseUrl/repository/SPDX; omit when offline.  
    - `--force-refresh` ignores cached licenses in `.cache/licenses/`.  
    - `--dry-run` shows the planned diff without writing files.  
    - `--no-sync-families` skips rewriting `third-party-families.json`.  
  - Outputs & diagnostics:  
    - Writes notices to `THIRD-PARTY-NOTICES.md` (unless dry-run).  
    - Writes trace to `.cache/update_trace.json` and a per-run folder under `.cache/third_party_runs/<timestamp>/` (includes planned/current notices).  
    - Rewrites `third-party-families.json` with the discovered package grouping unless disabled.  

- `check_third_party.py`  
  - Validate the notices file: `python3 tools/check_third_party.py --trace .cache/check_trace.json`  
  - Checks ordering, indentation, placeholder text, grouping expectations, and that only direct dependencies/manual sections remain.  

## Shared helpers
- `third_party_common.py` holds common utilities: dependency resolution, family/org mapping, license cleaning, caching paths, and grouping heuristics.
- Manual/local dependencies and allowed manual sections are defined here (e.g., ILSpy, AvaloniaEdit, VS image library).

## Org and family configuration
- `third-party-orgs.json` can declare `github_prefixes` for owner mapping and `license_aliases` to unify small copyright variants within the same family.
- `third-party-families.json` captures repository-managed family names and their packages. The updater syncs this unless `--no-sync-families` is set.

## Troubleshooting
- See `.cache/third_party_runs/<timestamp>/` for per-run `trace.json`, `current_notices.md`, and `planned_notices.md`.
- Cached license files live in `.cache/licenses/`; use `--force-refresh` to re-fetch.
- If the checker fails, consult `.cache/check_trace.json` for details.***
