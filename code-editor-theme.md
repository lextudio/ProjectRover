# WPF ILSpy: How decompiled code + hover tooltip theme is determined

## 1. Where the selected theme comes from

1. `SessionSettings.Theme` is loaded from session XML, defaulting to `ThemeManager.Current.DefaultTheme` (`"Light"`).
   - `ProjectRover/src/ILSpy/ILSpy/SessionSettings.cs:58`
   - `ProjectRover/src/ILSpy/ILSpy/Themes/ThemeManager.cs:50`
2. On app startup, ILSpy applies it with `ThemeManager.Current.Theme = sessionSettings.Theme`.
   - `ProjectRover/src/ILSpy/ILSpy/App.xaml.cs:112`
3. `ThemeManager.UpdateTheme()` loads `/themes/Theme.{ThemeName}.xaml`, merges it into app resources, and caches all `SyntaxColor.*` entries for syntax highlighting.
   - `ProjectRover/src/ILSpy/ILSpy/Themes/ThemeManager.cs:113`
   - `ProjectRover/src/ILSpy/ILSpy/Themes/ThemeManager.cs:128`
   - `ProjectRover/src/ILSpy/ILSpy/Themes/ThemeManager.cs:136`

Theme can be changed at runtime from:
- View menu theme picker (`SetThemeCommand` writes `SessionSettings.Theme`)
  - `ProjectRover/src/ILSpy/ILSpy/Controls/MainMenu.xaml:25`
  - `ProjectRover/src/ILSpy/ILSpy/Commands/SetThemeCommand.cs:14`
- Options panel combo box (bound to `SessionSettings.Theme`)
  - `ProjectRover/src/ILSpy/ILSpy/Options/DisplaySettingsPanel.xaml:14`

`ThemeManager` listens for `SessionSettings.Theme` property changes and reapplies the theme dictionary:
- `ProjectRover/src/ILSpy/ILSpy/Themes/ThemeManager.cs:211`

## 2. Decompiled source editor theme resolution

1. Editor base brushes are dynamic resources in `DecompilerTextView.xaml`:
   - Background: `themes:ResourceKeys.TextBackgroundBrush`
   - Foreground: `themes:ResourceKeys.TextForegroundBrush`
   - Line numbers: `themes:ResourceKeys.LineNumbersForegroundBrush`
   - `ProjectRover/src/ILSpy/ILSpy/TextView/DecompilerTextView.xaml:35`
2. These keys are defined in `Theme.*.xaml` (and merged with `Base.*.xaml`):
   - Example light: `ProjectRover/src/ILSpy/ILSpy/Themes/Theme.Light.xaml:8`
   - Example dark: `ProjectRover/src/ILSpy/ILSpy/Themes/Theme.Dark.xaml:8`
3. Syntax highlighting definitions are registered in `DecompilerTextView.RegisterHighlighting()`.
   - `ProjectRover/src/ILSpy/ILSpy/TextView/DecompilerTextView.cs:1318`
4. While registering each `.xshd`, ILSpy wraps definition creation with `ThemeManager.Current.ApplyHighlightingColors(highlightingDefinition)`.
   - `ProjectRover/src/ILSpy/ILSpy/TextView/DecompilerTextView.cs:1457`
5. `ApplyHighlightingColors()` resets named colors and applies matching `SyntaxColor.<Language>.*` entries from the current theme dictionary.
   - `ProjectRover/src/ILSpy/ILSpy/Themes/ThemeManager.cs:90`
   - `ProjectRover/src/ILSpy/ILSpy/Themes/ThemeManager.cs:98`
6. Active language picks a highlighting definition by file extension (`Language.SyntaxHighlighting`), and decompile output sets it on the editor (`textEditor.SyntaxHighlighting = highlighting`).
   - `ProjectRover/src/ILSpy/ILSpy/Languages/Language.cs:76`
   - `ProjectRover/src/ILSpy/ILSpy/TextView/DecompilerTextView.cs:746`
7. AvalonEdit colorization uses `ThemeAwareHighlightingColorizer`; for non-theme-aware definitions on dark themes, it auto-adjusts colors.
   - `ProjectRover/src/ILSpy/ILSpy/TextView/DecompilerTextEditor.cs:9`
   - `ProjectRover/src/ILSpy/ILSpy/TextView/ThemeAwareHighlightingColorizer.cs:24`

When theme changes, ILSpy forces re-register + redraw of decompiled output:
- `DecompilerTextView.RegisterHighlighting(); RefreshDecompiledView();`
- `ProjectRover/src/ILSpy/ILSpy/AssemblyTree/AssemblyTreeModel.cs:89`

## 3. Hover tooltip popup theme resolution

1. On mouse hover, `DecompilerTextView` finds the hovered reference segment and calls `GenerateTooltip(...)`.
   - `ProjectRover/src/ILSpy/ILSpy/TextView/DecompilerTextView.cs:237`
   - `ProjectRover/src/ILSpy/ILSpy/TextView/DecompilerTextView.cs:412`
2. For opcode/entity tooltips, ILSpy builds a `FlowDocument` via `DocumentationUIBuilder`, passing `languageService.Language.SyntaxHighlighting` / `currentLanguage.SyntaxHighlighting`.
   - `ProjectRover/src/ILSpy/ILSpy/TextView/DecompilerTextView.cs:419`
   - `ProjectRover/src/ILSpy/ILSpy/TextView/DecompilerTextView.cs:471`
3. `DocumentationUIBuilder` uses that highlighting definition for signature/code blocks (`DocumentHighlighter` + rich text runs).
   - `ProjectRover/src/ILSpy/ILSpy/TextView/DocumentationUIBuilder.cs:96`
   - `ProjectRover/src/ILSpy/ILSpy/TextView/DocumentationUIBuilder.cs:110`
4. Popup chrome/text colors are `SystemColors`-based:
   - Background: `SystemColors.ControlBrushKey`
   - Border: `SystemColors.ControlDarkBrushKey`
   - Foreground: `SystemColors.InfoTextBrushKey`
   - `ProjectRover/src/ILSpy/ILSpy/TextView/DecompilerTextView.cs:523`
5. Those `SystemColors.*` resources are overridden by the active base theme dictionaries (`Base.Light.xaml` / `Base.Dark.xaml`), so popup container text/chrome follows current theme.
   - `ProjectRover/src/ILSpy/ILSpy/Themes/Base.Light.xaml:18`
   - `ProjectRover/src/ILSpy/ILSpy/Themes/Base.Dark.xaml:18`

## 4. Short summary

- Decompiled editor colors come from:
  - `ResourceKeys` brushes (background/foreground/etc.) from `Theme.*.xaml`
  - Syntax token colors from `SyntaxColor.*` entries applied through `ThemeManager.ApplyHighlightingColors()`
- Hover popup colors come from:
  - Same language syntax highlighting for code/signatures inside the tooltip
  - `SystemColors.*` brushes for popup frame/text, which are overridden by the active base theme resources

## 5. Avalonia (Rover) equivalent: current implementation

### 5.1 Theme selection + persistence

1. Rover still uses `SessionSettings.Theme` and applies it at startup:
   - `ProjectRover/src/ProjectRover/App.axaml.cs:77`
   - `ProjectRover/src/ProjectRover/App.axaml.cs:295`
2. Avalonia theme switching is centralized in `ThemeManagerShim` (`ThemeManager` in Rover), and it drives `RequestedThemeVariant` on app/windows:
   - `ProjectRover/src/ProjectRover/Themes/ThemeManagerShim.cs:80`
   - `ProjectRover/src/ProjectRover/Themes/ThemeManagerShim.cs:93`
   - `ProjectRover/src/ProjectRover/Themes/ThemeManagerShim.cs:101`
3. Options panel and menu are wired to the same setting/theme manager:
   - `ProjectRover/src/ProjectRover/Options/DisplaySettingsPanel.axaml:18`
   - `ProjectRover/src/ProjectRover/Commands/ThemeCommands.cs:38`
   - `ProjectRover/src/ProjectRover/Controls/MainMenu.axaml.cs:1403`

### 5.2 Decompiled editor colors

1. Highlighting registration flow is equivalent to WPF (`RegisterHighlighting` + `ApplyHighlightingColors` hook):
   - `ProjectRover/src/ProjectRover/TextView/DecompilerTextView.axaml.cs:1759`
   - `ProjectRover/src/ProjectRover/TextView/DecompilerTextView.axaml.cs:1923`
2. Decompile output still assigns language highlighting definition to editor:
   - `ProjectRover/src/ProjectRover/TextView/DecompilerTextView.axaml.cs:1111`
3. Decompiled syntax coloring is now driven by ILSpy highlighting + `ThemeManager.ApplyHighlightingColors` (WPF-equivalent), not TextMate theme names.
4. Current/line/link editor resources are still bound from theme resource keys:
   - `ProjectRover/src/ProjectRover/TextView/DecompilerTextView.axaml.cs:327`

### 5.3 Hover tooltip colors

1. Hover flow mirrors WPF: resolve reference segment, build tooltip content via `DocumentationUIBuilder`, show popup:
   - `ProjectRover/src/ProjectRover/TextView/DecompilerTextView.axaml.cs:743`
   - `ProjectRover/src/ProjectRover/TextView/DecompilerTextView.axaml.cs:752`
   - `ProjectRover/src/ProjectRover/TextView/DecompilerTextView.axaml.cs:820`
2. Popup styling uses resource keys `ToolTipBackgroundBrush`/`ToolTipBorderBrush` with hardcoded fallback:
   - `ProjectRover/src/ProjectRover/Controls/TooltipPopup.cs:93`
   - `ProjectRover/src/ProjectRover/Controls/TooltipPopup.cs:94`
3. App-level theme resources are currently provided via `ThemeDictionaries` for only `Light` and `Dark`:
   - `ProjectRover/src/ProjectRover/App.axaml:67`
   - `ProjectRover/src/ProjectRover/App.axaml:176`

## 6. Rover status update: 6 themes now wired

Target names (same as WPF) are now available in Rover:
- `Light`
- `Dark`
- `VS Code Light+`
- `VS Code Dark+`
- `R# Light`
- `R# Dark`

Implemented:

1. Theme surface/API expanded to 6 names (no 2-theme collapsing)
   - `ProjectRover/src/ProjectRover/Themes/ThemeManagerShim.cs:22`
   - `ProjectRover/src/ProjectRover/Themes/ThemeManagerShim.cs:31`
   - `ProjectRover/src/ProjectRover/Themes/ThemeManagerShim.cs:168`
2. Menu commands now include all 6 theme entries
   - `ProjectRover/src/ProjectRover/Commands/ThemeCommands.cs:78`
3. View-model theme picker now exposes all 6
   - `ProjectRover/src/ProjectRover/MainWindowViewModel.cs:67`
4. Per-theme Avalonia resource dictionaries added and loaded dynamically
   - `ProjectRover/src/ProjectRover/Themes/ThemeManagerShim.cs:190`
   - `ProjectRover/src/ProjectRover/Themes/Theme.VSCodeLightPlus.axaml:1`
   - `ProjectRover/src/ProjectRover/Themes/Theme.VSCodeDarkPlus.axaml:1`
   - `ProjectRover/src/ProjectRover/Themes/Theme.RSharpLight.axaml:1`
   - `ProjectRover/src/ProjectRover/Themes/Theme.RSharpDark.axaml:1`
5. Tooltip resource keys now exist in both app-level and per-theme resources
   - `ProjectRover/src/ProjectRover/App.axaml:87`
   - `ProjectRover/src/ProjectRover/App.axaml:198`
   - `ProjectRover/src/ProjectRover/Controls/TooltipPopup.cs:93`
6. Decompiler TextMate wiring removed to keep decompiled highlighting source aligned with WPF (`SyntaxColor.*` via theme manager).
   - `ProjectRover/src/ProjectRover/TextView/DecompilerTextView.axaml.cs`

## 7. Syntax color parity status (updated)

`SyntaxColor.*` support is now implemented in Rover:

1. Rover stores `SyntaxColor.<Language>.*` entries directly in Rover `Theme.*.axaml` files as `themes:SyntaxColorResource` (no WPF theme file dependency).
   - `ProjectRover/src/ProjectRover/Themes/Theme.Light.axaml`
   - `ProjectRover/src/ProjectRover/Themes/Theme.Dark.axaml`
   - `ProjectRover/src/ProjectRover/Themes/Theme.VSCodeLightPlus.axaml`
   - `ProjectRover/src/ProjectRover/Themes/Theme.VSCodeDarkPlus.axaml`
   - `ProjectRover/src/ProjectRover/Themes/Theme.RSharpLight.axaml`
   - `ProjectRover/src/ProjectRover/Themes/Theme.RSharpDark.axaml`
2. `ThemeManagerShim` now caches parsed syntax-color mappings per selected theme and applies them to named highlighting colors (same prefix logic as WPF).
   - `ProjectRover/src/ProjectRover/Themes/ThemeManagerShim.cs`
3. Highlighting refresh is triggered for open editors when theme changes.
   - `ProjectRover/src/ProjectRover/Themes/ThemeManagerShim.cs`
   - `ProjectRover/src/ProjectRover/TextView/DecompilerTextView.axaml.cs:1778`
4. A dark-theme fallback conversion path remains in place only when syntax-color mappings cannot be loaded/applied.

## 8. Remaining gaps to full WPF parity

1. Visual equivalence still needs manual tuning/verification for each of the 6 themes across editor + tooltip scenarios.
2. Decompiler no longer blends `SyntaxColor.*` with TextMate theme tokenization; remaining differences are mostly brush/value tuning and Avalonia rendering differences.
3. No automated UI regression check yet validates token colors for all languages/themes.
