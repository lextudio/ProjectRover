## Theme Support Plan

Goal: give Project Rover the same light/dark switching the old Rover exposed, but routed through the existing ILSpy `ThemeManager` API so linked ILSpy code (settings, text view coloring, menu commands) stays consistent.

### Current State
- Avalonia app ships with Light/Dark resources in `App.axaml` via `ResourceDictionary.ThemeDictionaries`, but the runtime never switches `RequestedThemeVariant`.
- ILSpy’s `SessionSettings.Theme` is persisted, and `ThemeManager` (WPF) drives highlight colors and `IsDarkTheme`. Our Avalonia shim leaves `Theme`/`IsDarkTheme` empty and doesn’t touch the app resources.
- Menu commands for setting the theme existed in old Rover UI; the new dynamic menu doesn’t surface any theme commands.

### Proposed Approach
- Normalize on ILSpy theme names (`"Light"`, `"Dark"`) and map them to Avalonia `ThemeVariant` in the `ThemeManager` shim. Persisted value stays in `SessionSettings.Theme`.
- Let the shim own a merged `ResourceDictionary` scoped to the application. Load a small Avalonia palette (Light/Dark) keyed the same way ILSpy expects (`BracketHighlightBackgroundBrush`, `ThemeForegroundBrush`, icon keys, etc.), and set `Application.Current.RequestedThemeVariant` to drive Avalonia’s own theme dictionaries.
- Keep `ThemeManager.Current.IsDarkTheme` and `DefaultTheme` in sync so linked text-view code can react (e.g., dark-mode syntax helpers).
- Surface Light/Dark menu items via `ExportMainMenuCommand` so the dynamic main menu can call into the shim and update `SessionSettings.Theme`.

### Prototype Steps (this change)
1) Add `ThemeManager` shim behavior: `Theme` property that sets `RequestedThemeVariant`, merges a Light/Dark palette, and updates `IsDarkTheme`.
2) Apply persisted theme early in `App.OnFrameworkInitializationCompleted` using `settingsService.SessionSettings.Theme`.
3) Add Light/Dark menu commands (`_View` → Theme) that call into `ThemeManager` and update `SessionSettings`.
4) Keep the existing `App.axaml` theme dictionaries; the shim simply toggles the variant and overlays the ILSpy-keyed palette for editor/highlight resources.

### Follow-ups (post-prototype)
- Expand palette to include the remaining ILSpy theme resources (syntax colors, text marker brushes) and hook `ApplyHighlightingColors` to AvaloniaEdit definitions.
- Consider persisting the selected theme in a Rover-level settings file for scenarios where ILSpy settings are unavailable.
- Add a UI indicator/checkmark for the active theme and expose the toggle in the toolbar if requested.
