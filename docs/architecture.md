## **Code Sharing: ILSpy (WPF) ↔ ILSpyRover (Avalonia)**

**Purpose:** Document architecture, shared code, feature parity, current gaps, and a prioritized set of epics/tasks to bring ILSpyRover to feature parity with ILSpy WPF while maximizing shared code via `ICSharpCode.ILSpyX`.

**Scope:** This file compares `ilspy/ILSpy` (WPF) to `ILSpyRover/src/ProjectRover` (Avalonia), plus related shared libs included in the workspace (`ICSharpCode.Decompiler`, `AvaloniaEdit`).

---

**Quick summary**
- WPF ILSpy: Mature WPF app with many platform-specific UI components and rich features.
- ILSpyRover: Avalonia reimplementation focused on cross-platform UI; already shares core decompiler engine and increasingly reuses `ICSharpCode.ILSpyX`.

---

**1) High-level Architecture Comparison**

- ILSpy (WPF)
  - UI: WPF XAML, Docking, SharpTreeView, many WPF controls and templates.
  - App logic: `ICSharpCode.ILSpyX` provides many core behaviors, tree models and adapters.
  - Decompiler: `ICSharpCode.Decompiler` used as core engine.

- ILSpyRover (Avalonia)
  - UI: Avalonia XAML, Dock.Avalonia, AvaloniaEdit (editor), Dock.Model.
  - App logic: Own `ProjectRover` viewmodels and `Node` hierarchy but gradually adopting `ICSharpCode.ILSpyX` adapters.
  - Decompiler: `ICSharpCode.Decompiler` shared.

Shared pieces
- `ICSharpCode.Decompiler` (core engine)
- `ICSharpCode.ILSpyX` when available (Rover uses it conditionally via `UsingLocalIlSpyX`)
- `AvaloniaEdit` (ported editor) is shared across Rover-specific UI.

---

**2) Feature Inventory (feature-by-feature)**

Legend: W = WPF ILSpy, R = Rover (Avalonia), S = Shared (library)

- Assembly loading & list: W (ILSpyX AssemblyListManager) — R (Rover uses ILSpyX when present) — S: ILSpyX
- Tree view (types, members, resources): W (SharpTreeNode hierarchy) — R (Rover Node classes + adapters) — S: ILSpyX adapters
- Decompilation/Language selection: W (ILSpy UI integrations) — R (Rover delegates to ICSharpCode.Decompiler) — S: Decompiler
- Search across assemblies/types/members: W (ILSpy search) — R (SearchService + ILSpyX adapters) — S: ILSpyX search providers
- Resources extraction (.resources, .resx): W (ResourcesFile parsing, resource viewers) — R (implemented, uses Decompiler.Util.ResourcesFile) — S: Decompiler.Util.ResourcesFile
- Go To Definition / Reference navigation: W (integrated with ILSpyX mapping) — R (partially: adapters exist; some mappings ported) — S: ILSpyX mappings
- Syntax highlighting & editor features: W (AvalonEdit) — R (AvaloniaEdit port) — S: TextMate grammars (via TextMateSharp.Grammars)
- Plugin / extension model: W (ILSpy add-ins) — R (no established plugin model yet) — S: ICSharpCode.ILSpyX extension points (where available)
- Settings & Recent files: W (ILSpySettings) — R (Rover has its own JSON options but now integrates ILSpySettings when using ILSpyX) — S: ILSpyX settings
- Analyzers / code metrics: W (many analyzers integrated) — R (not fully integrated) — S: ILSpy analyzers (some are in ILSpyX)
- UX polish (icons, theming, dock layout): W (uses VS-style icons and many small tweaks) — R (Avalonia theming + custom icons) — S: Shared image assets are limited; mapping required

---

**3) Shared Code Areas (already shared or practical to share)**

- Decompiler engine: `ICSharpCode.Decompiler` — already used unchanged.
- Resource parsing: `ICSharpCode.Decompiler.Util.ResourcesFile` — Rover uses it.
- ILSpyX adapters & models: `ICSharpCode.ILSpyX` — Rover conditionally references it and has `IlSpyXTreeAdapter` and `IlSpyXSearchAdapter` to adapt WPF concepts to Avalonia UI.
- Text editor logic: ported `AvaloniaEdit` preserves many editor behaviors from AvalonEdit.
- Search result factories/adapters — Rover implements adapters to consume ILSpyX results.

---

**4) Current Gaps (Rover → ILSpy parity)**

Major gaps to address (mapped to proposed epics below):

1. Decompilation integration parity — delegate more navigation and token mapping to ILSpyX.
2. Search completeness — integrate all ILSpyX search providers and result types.
3. Analyzers & usage tools — port or reuse ILSpy analyzers for Rover.
4. Extension/plugin model — determine a minimal adapter to support ILSpy add-ins or provide an Avalonia plugin wrapper.
5. Settings & profile parity — ensure Rover reads/writes the same settings nodes for cross-app coherence.
6. Icon/theme parity & UX polish — align icons and small UI behaviors with ILSpy WPF.
