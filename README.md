# Destiny.gg Chat Desktop

Windows desktop wrapper for the live Destiny.gg embedded chat at `https://www.destiny.gg/embed/chat`.

## What it does

- Hosts the live Destiny.gg embed inside a native Windows window
- Keeps Destiny.gg links inside the app and opens non-Destiny links in your default browser
- Saves window size, last page, mute state, pin-on-top state, and zoom level
- Adds desktop controls for navigation, zoom, mute, and update checks
- Can update itself from GitHub Releases by downloading a new portable package and restarting through a small PowerShell handoff script

## Requirements

- [.NET 8 SDK](https://aka.ms/dotnet/download) (Windows)

## Repository layout

| Path | Purpose |
|------|---------|
| [`DestinyChatDesktop.sln`](DestinyChatDesktop.sln) | Solution (WPF shell + embedded UI host + tests) |
| [`src/DestinyChatDesktop.Wpf/`](src/DestinyChatDesktop.Wpf/) | **Entry executable** — WPF window hosting the UI shell |
| [`src/DestinyChatDesktop.EmbeddedWinForms/`](src/DestinyChatDesktop.EmbeddedWinForms/) | WinForms + WebView2 chat shell (full legacy UI and behavior) |
| [`tests/DestinyChatDesktop.Tests/`](tests/DestinyChatDesktop.Tests/) | Smoke/unit tests |
| [`appsettings.json`](appsettings.json) | Startup settings (copied next to the built `.exe`) |
| [`build.ps1`](build.ps1) | `dotnet publish` to `dist/` (optional portable zip to `artifacts/`) |
| `Run Destiny Chat.cmd` | Build then launch `dist\DestinyChatDesktop.exe` |
| [`.github/workflows/ci.yml`](.github/workflows/ci.yml) | CI build + test |
| [`.github/workflows/release.yml`](.github/workflows/release.yml) | Tagged releases |

See [`ARCHITECTURE.md`](ARCHITECTURE.md) and [`AGENTS.md`](AGENTS.md) for structure and AI/agent contribution rules.

## Build

```powershell
.\build.ps1
```

Portable package (same artifact name as before):

```powershell
.\build.ps1 -CreatePortablePackage
```

Output:

- Published app: `dist\` (includes `DestinyChatDesktop.exe`, `DestinyChatDesktop.EmbeddedWinForms.dll`, WebView2 loader, `appsettings.json`)
- Optional zip + checksum: `artifacts\DestinyChatDesktop-portable.zip`, `artifacts\DestinyChatDesktop-portable.sha256`

## Usage

1. Run `Run Destiny Chat.cmd` (or run `dist\DestinyChatDesktop.exe` after build)
2. Sign in to Destiny.gg inside the app if you want full chat interaction
3. Use `F5` to reload and `F11` to toggle fullscreen
4. Use the `Update` toolbar button for a manual GitHub update check

## Notes

- The actual chat, login flow, and site functionality still come from the live Destiny.gg website.
- GitHub releases should be tagged as `v<appVersion>`, for example `v0.1.0` (must match `appVersion` in `appsettings.json`).
- The updater looks for the `DestinyChatDesktop-portable.zip` asset on the latest GitHub release for the configured repo.
