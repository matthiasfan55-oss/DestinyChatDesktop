# Architecture

## Overview

The application is a **.NET 8 WPF executable** that owns process lifetime and window chrome at the OS level, while the interactive Destiny.gg shell remains implemented as **WinForms + WebView2** for full behavioral parity with the original single-file app.

```
┌─────────────────────────────────────────────┐
│  DestinyChatDesktop.exe (WPF)             │
│  ┌───────────────────────────────────────┐  │
│  │ WindowsFormsHost                       │  │
│  │  ┌─────────────────────────────────┐  │  │
│  │  │ MainForm (+ MediaPopoutForm)    │  │  │
│  │  │ WinForms + WebView2              │  │  │
│  │  └─────────────────────────────────┘  │  │
│  └───────────────────────────────────────┘  │
└─────────────────────────────────────────────┘
```

### Projects

| Project | Role |
|---------|------|
| **DestinyChatDesktop.Wpf** | `Application` entry, `MainWindow`, bootstrap (`AppBootstrap`), copied `appsettings.json`, DPI/manifest wiring |
| **DestinyChatDesktop.EmbeddedWinForms** | All legacy UI: `MainForm`, embed scripts, bigscreen/dual chat, updater, self-tests, Kickstiny injection |
| **DestinyChatDesktop.Tests** | Fast regression checks (expand over time) |

### JSON serialization

The embedded host historically used `JavaScriptSerializer`. The embedded library ships a small **`JavaScriptSerializer`-compatible shim** (`JsonLegacy.cs`) backed by **Newtonsoft.Json** so the project builds on modern .NET without `System.Web.Extensions`.

### Paths and updates

- Config: `appsettings.json` next to the main `.exe` (publish copies from repo root via the WPF project link).
- State: `%LocalAppData%\DestinyChatDesktop\state.json`
- Self-tests / logs: same `%LocalAppData%\DestinyChatDesktop\` paths as before
- GitHub updater resolves `Application.ExecutablePath` → the **WPF** entry binary (`DestinyChatDesktop.exe`), which is intentional for portable packages.

### DPI / hosting caveats

Hosting WinForms inside WPF can introduce DPI edge cases on multi-monitor setups. If subtle layout differences appear versus a pure WinForms process, prefer validating at 100% display scaling first.

### Future modularization

`EmbeddedApplication.cs` is still large by design (parity migration). Safe refactors:

1. Split `MainForm` into `partial` files by feature (toolbar, bigscreen, dual chat, updates, web messages).
2. Move injected script strings into `.js` embedded resources.
3. Gradually replace `WindowsFormsHost` with native WPF layout once each subsystem is ported.
