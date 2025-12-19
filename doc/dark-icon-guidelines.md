**Dark Icon Guidelines**

Summary
- The repository contains paired icon sets under `Assets/` and `Assets/Dark/`.
- Dark variants are not simply inverted; they use lighter foreground colors (typically grey or pastel hues) and adjusted opacities to remain readable on dark backgrounds.
- Inventory: 70 light SVGs vs 49 dark SVGs (21 light icons have no dark counterpart yet). Missing dark examples: `StatusInformationOutline.svg`, `ShowAll.svg`, `SuperTypes.svg`, `ShowPublicOnly.svg`, `ClassPublic.svg`, `StatusOKOutline.svg`, `DebugMetadata.svg`, `ClearSearch.svg`, `AssemblyList.svg`, `StatusWarningOutline.svg`, … (11 more).
- Palette snapshot (current files)
  - Light top colors: `#212121` (93 uses), `#996f00` (30), `#005dba` (26), `#f6f6f6` (16), `#6936aa` (10)
  - Dark top colors: `#c0c0c0` (40), `#dcdcdc` (38), `#e0b44c` (14), `#005dba` (12), `#996f00` (10), `#c3a5ff` (9), `#50e6ff` (4)
  - Observed mappings in current pairs:
    - Low-opacity layers often go from 0.1 → ~0.35 in dark variants

| Light color (usage) | Dark color (usage) | Notes |
| --- | --- | --- |
| <span style="display:inline-block;width:12px;height:12px;background:#212121;border:1px solid #ccc;"></span> `#212121` (93) | <span style="display:inline-block;width:12px;height:12px;background:#dcdcdc;border:1px solid #ccc;"></span> `#dcdcdc` (canonical) | Primary greys: standardize on `#dcdcdc` for dark |
| <span style="display:inline-block;width:12px;height:12px;background:#6936aa;border:1px solid #ccc;"></span> `#6936aa` (10) | <span style="display:inline-block;width:12px;height:12px;background:#c3a5ff;border:1px solid #ccc;"></span> `#c3a5ff` (9) | Purple accent lightened |
| <span style="display:inline-block;width:12px;height:12px;background:#005dba;border:1px solid #ccc;"></span> `#005dba` (26) | <span style="display:inline-block;width:12px;height:12px;background:#8ab4f8;border:1px solid #ccc;"></span> `#8ab4f8` (canonical) | Blue accents: standardize on `#8ab4f8` for dark |
| <span style="display:inline-block;width:12px;height:12px;background:#996f00;border:1px solid #ccc;"></span> `#996f00` (30) | <span style="display:inline-block;width:12px;height:12px;background:#e0b44c;border:1px solid #ccc;"></span> `#e0b44c` (14) | Gold accent lightened |
| <span style="display:inline-block;width:12px;height:12px;background:#f6f6f6;border:1px solid #ccc;"></span> `#f6f6f6` (16) | — (no direct dominant) | Dark relies on lighter greys instead of near-white |
| Semi-transparent layers (0.1 alpha) | ~0.35 alpha | Dark variants boost alpha for legibility |

Observed color mapping patterns
- Neutral/greys:
  - Light icons use dark greys like `#212121` with opacities such as `1` or `0.1` for accents.
  - Dark icons replace these with lighter greys around `#c0c0c0` — `#c0c0c0` or `#dcdcdc` — and sometimes increase the semi-transparent alpha (e.g., `0.35` instead of `0.1`) so the accents are legible.

- Accent colors (blues, purples):
  - Example: `MethodPublic` uses `#6936aa` (rich purple) in the light set and `#c3a5ff` (pale purple) in dark. The dark variant increases perceived luminance to remain visible on dark backgrounds.
  - Example: blues like `#005dba` in light set become `#8ab4f8` in dark set.

- Opacity adjustments:
  - Many light icons use low-opacity `-10` classes (e.g., `.light-defaultgrey-10 { opacity: 0.1 }`). Dark counterparts frequently use larger alpha for the same layer (e.g., `0.35`) to preserve contrast.

Implementation patterns discovered
- CSS class naming inside SVGs uses theme-prefixed names like `.light-defaultgrey`, `.light-purple`. Despite prefix `light-`, dark variants often keep the same class names but change the color values inside the file (i.e., `Assets/Dark/*.svg` redefines `.light-defaultgrey` to a lighter color). This keeps application logic simple: the same class keys are referenced but individual SVG files are pre-adjusted for the theme.

Inconsistencies & issues found
- Class naming mismatch vs. file location:
  - The classes still use `light-` prefixes (e.g., `.light-defaultgrey`) even in Dark SVG copies. This is confusing for maintainers and tools. Consider renaming classes to theme-agnostic names (e.g., `.fg-1`, `.fg-2`, `.accent`) or remove `light-` prefix in Dark folder to avoid cognitive load.
- Missing dark variants:
  - 21 light icons have no dark version; see inventory note above. This blocks full dark parity because the UI falls back to light assets.

- Opacity variance not uniform:
  - Some icons increase the opacity of `-10` accent layers from `0.1` → `0.35` while others change it to `0.1`→`0.1` or `0.35` inconsistently. Decide on a consistent mapping table (see recommendations below).

- Accent color lightening inconsistent across hues:
  - Purple and blue accents are lightened, but some hues are changed more aggressively than others. Define a measured luminance target (e.g., remap to same perceived luminance value) to make the transformation predictable.

- Title and metadata labels sometimes still say "IconLight..." inside `Assets/Dark` files (minor but confusing). Update titles to reflect dark variants.
- Some icon families (class/enum/interface) still carry light-palette strokes (e.g., `#212121` or the original `#005dba`) and low-opacity overlays (~0.1) in Dark files, so they look heavier than fields/properties. Normalize these to the canonical dark greys (`#dcdcdc`) and lightened accents (`#8ab4f8`/`#c3a5ff`), and raise muted strokes to ~0.35 opacity for parity.

Recommended conversion rules (light → dark)
1. Neutral greys
   - Light: `#212121` (opaque) → Dark: `#dcdcdc` / `#c0c0c0` (opaque)
   - Light: `#212121` @ opacity `0.1` → Dark: `#c0c0c0` @ opacity `0.35`

2. Primary accent colors (blue/purple)
   - Map to higher lightness variant while preserving hue.
   - Example: `#005dba` → `#8ab4f8`; `#6936aa` → `#c3a5ff`.
   - Prefer HSL-preserving transformations: increase L (lightness) to ~70% while preserving H and S as possible.

3. Opacity policy
   - For semi-transparent accents used as shadows or low-contrast strokes: increase alpha in dark variants by 2–4× compared to light variant (e.g., 0.1 → 0.35).

4. Semantic class names
   - Replace theme-prefixed class names (`light-*`) with semantic tokens: `.bg`, `.bg-weak`, `.fg`, `.fg-weak`, `.accent`, `.accent-muted`.
   - This allows both light and dark files to share the same conceptual tokens and makes automation easier.

Practical migration suggestions
- Short term (minimal changes):
  - Keep the current file-per-theme approach but fix these inconsistencies:
    - Update `<title>` in Dark files to include "Dark".
    - Standardize grey replacements to `#c0c0c0` or `#dcdcdc` consistently.

- Mid term (recommended):
  - Normalize classes to semantic tokens and update all icons (light + dark) to use them. Example tokens:
    - `.canvas` — canvas/background mask
    - `.fg` — main foreground color
    - `.fg-muted` — secondary/low-opacity foreground
    - `.accent` — accent color for that icon
    - `.accent-muted` — accent low-opacity

  - Add a small script (Node.js or dotnet tool) to validate and normalize SVGs:
    - Ensure dark variants exist for all icons used by the app.
    - Ensure color mapping follows the rules above.

- Long term:
  - Consider single-SVG approach with CSS variables: use the same SVG but switch variables at runtime (via an injected stylesheet), so you ship a single asset and change appearance by swapping a small theme stylesheet. This requires changing how assets are loaded (inlining or referencing external CSS variables) but reduces duplication.

Examples (concrete pairs reviewed)
- `Search.svg` vs `Dark/Search.svg`
  - Light: `.light-defaultgrey-10 { fill: #212121; opacity: 0.1 }` and `.light-defaultgrey { fill: #212121 }`
  - Dark: `.light-defaultgrey-10 { fill: #c0c0c0; opacity: 0.35 }` and `.light-defaultgrey { fill: #c0c0c0 }`
  - Recommendation: rename class `.fg-muted` and map to `#c0c0c0`@0.35 in dark, `#212121`@0.1 in light.

- `MethodPublic.svg` vs `Dark/MethodPublic.svg`
  - Light accent: `#6936aa` → Dark accent: `#c3a5ff` (good pattern: hue preserved, lightness increased).

Checklist for follow-up PRs
- [ ] Add dark variants for the 21 missing light icons (start with the list above).
- [ ] Rename SVG classes to semantic tokens and update all SVGs.
- [ ] Standardize grey replacement values and opacity mapping.
- [ ] Update `<title>` in Dark variants to say "IconDark...".
- [ ] Add a small validation script and run it as part of CI to detect missing dark assets or class mismatches.
- [ ] Optionally implement CSS variable approach for single-file icons.

Appendix: Quick script idea (pseudo)
```bash
# iterate over icons
for f in Assets/*.svg; do
  name=$(basename "$f")
  dark=Assets/Dark/$name
  if [ ! -f "$dark" ]; then
    echo "Missing dark variant: $name"
  fi
  # validate classes and title
done
```

Notes
- These findings are based on inspecting representative icon pairs in `Assets/` and `Assets/Dark/`. The same patterns apply across most icons but there are a few outliers where opacity or hues changed non-uniformly.

If you want, I can:
- Produce a script that standardizes all dark SVGs to the mapping rules above and optionally rename classes to semantic tokens.
- Implement a CI lint step that flags inconsistent icon pairs.
