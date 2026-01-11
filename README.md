![Project Rover banner](images/social.png)

# Project Rover

A small, cross-platform UI for exploring .NET assemblies.

- **Stable Releases:** [Downloads](https://github.com/lextudio/ProjectRover/releases)
- **Unstable Builds:** [CI artifacts](https://github.com/lextudio/ProjectRover/actions/workflows/package.yml)

## Table of Contents

- [Quick Start](#quick-start)
- [Screenshots](#screenshots)
- [Build from source](#build-from-source)
- [Before reporting any issues](#before-reporting-any-issues)
- [Layout](#layout)
- [Status](#status)
- [License](#license)

## Quick Start

Download the latest release archive for your platform from the Releases page, extract it, and run the app.

- **macOS** — extract the `.tar.gz` for your architecture and open `ProjectRover.app`. Downloads are on the [Releases page](https://github.com/lextudio/ProjectRover/releases).

  Example:

  ```bash
  tar -xzpf ProjectRover-macos-arm64.tar.gz
  xattr -d com.apple.quarantine ProjectRover.app
  open ProjectRover.app
  ```

- **Windows** — extract and run `ProjectRover.exe`. Downloads are on the [Releases page](https://github.com/lextudio/ProjectRover/releases).

- **Linux** — extract and run the `ProjectRover` binary. Downloads are on the [Releases page](https://github.com/lextudio/ProjectRover/releases).

  Example:

  ```bash
  mkdir ProjectRover && tar -xzpf ProjectRover-linux-x64.tar.gz -C ProjectRover
  ./ProjectRover/ProjectRover
  ```

## Screenshots

Below are a few representative screenshots.

### macOS

<p align="center">
  <img src="images/macos.png" alt="macOS view" width="720" />
</p>
*macOS: main window and code view.*

### Windows

<p align="center">
  <img src="images/windows.png" alt="Windows view" width="720" />
</p>
*Windows: assembly tree and context menus.*

### Linux

<p align="center">
  <img src="images/linux.png" alt="Linux view" width="720" />
</p>
*Linux: decompiled code.*

## Build from source

.NET 10 SDK is required. Then run:

```bash
git clone https://github.com/LeXtudio/ProjectRover.git
cd ProjectRover
git submodule update --init --recursive
cd src/ProjectRover
dotnet run
```

## Before reporting any issues

Please read [TROUBLESHOOTING.md](TROUBLESHOOTING.md) for step-by-step instructions for shipped users and other debugging tips.

## Layout

- `src/ProjectRover` – Avalonia application, shims for view models and other supporting files.
- `src/AvaloniaEdit` – bundled text editor control.
- `src/ILSpy` - Original ILSpy source code, mostly unmodified.

## Status

- Active development, not production ready. Expect bugs and missing features.
- Current focus is on code reuse and keeping up with ILSpy.

## License

This project is [AGPL](COPYING) licensed. It depends on ILSpy (MIT); see [THIRD-PARTY-NOTICES](THIRD-PARTY-NOTICES.md).
