# AJ Dock

AJ Dock is a Windows 11 desktop dock built with C#, .NET 8, WPF, MVVM, and native Windows APIs. It is a modern replacement-style dock inspired by Windows 11, macOS Dock, Nexus Dock, and RocketDock.

The goal is simple: make a desktop dock that feels polished, fast, personal, and useful enough to replace the default taskbar for everyday launching and control.

## Features

- Floating bottom-center dock on the primary monitor
- Frameless transparent WPF window with rounded corners and Windows 11 DWM backdrop hints
- Configurable icon size, dock size, spacing, magnification, animation speed, transparency, and blur setting
- Smooth pointer hover magnification
- Pin `.exe` and `.lnk` files by drag and drop
- Launch pinned apps
- Detect running applications and show an indicator under matching pinned apps
- Activate or minimize running applications from the dock
- Context menu actions: open, run as administrator, unpin, open file location, choose custom icon, close application
- JSON settings persistence at `%LOCALAPPDATA%\AJDock\settings.json`
- Auto-hide, always-on-top, hide Windows taskbar, and start-with-Windows settings
- Single-instance guard
- Taskbar restoration on normal exit and handled crashes
- Multi-monitor-ready positioning model through `DockPosition`
- Spotify/Plex media controls
- Always-on audio line visualizer with adjustable sensitivity
- Weather, Wi-Fi, calendar, volume, and per-app audio controls
- Theme presets and dock color customization

## Project Structure

```text
src/
  AJDock.Core/
    Models/              Shared dock settings and app models
  AJDock.App/
    Converters/          WPF binding converters
    Native/              Windows API declarations
    Services/            Persistence, launch, icon, shortcut, startup, taskbar, window services
    ViewModels/          MVVM state and commands
    Views/               Dock and settings WPF windows
tests/
  AJDock.Tests/          No-dependency console test runner
```

## Build

Install the .NET 8 SDK with Windows Desktop workload support, then run:

```powershell
dotnet restore
dotnet build .\AJDock.sln -c Release
dotnet test .\AJDock.sln -c Release
dotnet run --project .\src\AJDock.App\AJDock.App.csproj -c Release
```

## Publish A Windows Build

AJ Dock currently supports two release formats:

- Portable zip: extract and run `AJDock.exe`.
- Windows installer: guided install with beta terms, Start Menu shortcut, optional desktop shortcut, and uninstall entry.

To create a shareable Windows x64 zip:

```powershell
.\scripts\publish.ps1
```

The zip is written to:

```text
dist\AJDock-v0.1.1-win-x64.zip
```

To create a Windows installer, install Inno Setup 6 and run:

```powershell
.\scripts\build-installer.ps1 -Version 0.1.1
```

The installer is written to:

```text
dist\installer\AJDockSetup-v0.1.1-win-x64.exe
```

Upload the zip and/or installer to a GitHub Release.

To sign release binaries after you have a code-signing certificate:

```powershell
$env:AJDOCK_CERT_PASSWORD = "your-pfx-password"
.\scripts\build-installer.ps1 -Version 0.1.1 -Sign -CertificatePath C:\Path\To\AJDock.pfx
```

See [docs/DISTRIBUTION.md](docs/DISTRIBUTION.md) for SmartScreen, signing, installer, and private tester guidance.

## Install

Portable:

1. Download the latest `AJDock-*-win-x64.zip` from Releases.
2. Extract the zip.
3. Run `AJDock.exe`.

Installer:

1. Download the latest `AJDockSetup-*-win-x64.exe` from Releases.
2. Run the installer.
3. Review and accept the beta terms.
4. Launch AJ Dock from the installer, Start Menu, or desktop shortcut.

Windows SmartScreen may warn because early builds are not code-signed yet. Choose **More info** and **Run anyway** only if you trust the build. Some Windows 11 systems with Smart App Control may block unsigned apps more strictly.

## Uninstall

If installed with the setup exe, uninstall from Windows Settings > Apps > Installed apps > AJ Dock, or use the Start Menu uninstall shortcut. The uninstaller confirms removal and leaves user settings in `%LOCALAPPDATA%\AJDock` so future builds can reuse them.

## GitHub Release Checklist

```powershell
.\scripts\publish.ps1 -Version 0.1.1
.\scripts\build-installer.ps1 -Version 0.1.1
git tag v0.1.1
git push origin main --tags
```

Then create a GitHub Release for `v0.1.1` and upload the zip from `dist\` plus the installer from `dist\installer\`.

## Notes

- Hiding the native Windows taskbar is intentionally reversible. AJ Dock restores the taskbar on normal exit and through handled exception/process-exit hooks.
- Some elevated or protected process paths may not be readable by a non-elevated dock process, so those windows can be skipped by V1 running-app matching.
- The V1 UI targets the primary monitor. The settings and position model are structured so future per-monitor docks can be added without reshaping the app.

## Roadmap

- Dock folders
- System tray replacement
- Widgets
- Plugins
- Themes
- Rainmeter integration
- Multiple docks and per-monitor docks
- Window previews
- Virtual desktop integration
