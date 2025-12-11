## ILSpy add-in inventory

The ILSpy repository already defines a few extension surfaces that Rover can reuse or reimplement so we can stay close to what the WPF variant offers.

### 1. Visual Studio integration (`ILSpy.AddIn` + `ILSpy.AddIn.VS2022`)
The `ILSpy.AddIn` project contains the Visual Studio package that drives the VS-specific UI (VS menus, tool windows, options pages, etc.). Key artifacts include:

- `ILSpy.AddIn/ILSpyAddIn.vsct` and the localized variants (`ILSpyAddIn.en-US.vsct`, `ILSpyAddIn.zh-Hans.vsct`) define the commands that appear in VS menus and context menus.
- `ILSpy.AddIn/source.extension.vsixmanifest` (with the `.template`) declares the VSIX metadata.
- `ILSpy.AddIn.VS2022` is the version of the package that targets Visual Studio 2022 APIs; it shares most logic with `ILSpy.AddIn.Shared`.
- `ILSpy.AddIn.Shared` houses reusable helpers like `ILSpyInstance`, `Utils`, and VS package resources; it already abstracts the cross‑version bits that Rover can reuse (e.g., `ILSpyInstance` for bootstrapping ILSpy services).

**What this means for Rover:** The command/menu definitions and handler wiring in these projects show how ILSpy exposes navigation/decompilation features to external hosts. Rover can borrow those contracts (exportable commands, metadata for context menus) so Avalonia-based UI can light up similar extensions without re-creating the command definitions from scratch.

### 2. Sample plugin (`TestPlugin`)
`TestPlugin` demonstrates how the WPF ILSpy shell hosts add-ins:

- `MainMenuCommand.cs` / `ContextMenuCommand.cs` register new commands that target the main menu and context menus.
- `CustomLanguage.cs` and accompanying resource (`CustomOptionPage.xaml`) show how a plugin can inject a custom decompilation language and option page.
- `AboutPageAddition.cs` illustrates how to append UI to existing pages.

**What this means for Rover:** `TestPlugin` is a living example of a plugin that touches menus, options, and languages. Recreating something similar inside Rover would prove that Avalonia can honor the same add‑in contracts ILSpy exposes.

### Prioritized next steps for Rover

1. **Re-export ILSpy command metadata** – start by mirroring the VSCT command IDs (from `ILSpy.AddIn.vsct`) in Rover so Avalonia menus can host the same command set and a shared `Command`/`Menu` registry can be re-used across platforms.
2. **Frame a plugin hook** – once commands are shared, expose a lightweight plugin loader where an add-in can register menu and context menu entries in Rover (model after `TestPlugin` but targeting Avalonia menus).
3. **Document how to port extensions** – capture the steps required to port a WPF add-in (e.g., `TestPlugin`) so contributors can re-target the same code to Rover without rewriting the feature from scratch.

This document can be extended as other ILSpy add-ins surface (e.g., analyzers or VS-specific packages) so the Rover team has a single reference point for prioritizing compatibility work.
