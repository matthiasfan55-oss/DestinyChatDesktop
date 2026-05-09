# Destiny.gg Chat Desktop

Windows desktop wrapper for the live Destiny.gg embedded chat at `https://www.destiny.gg/embed/chat`.

## What it does

- Hosts the live Destiny.gg embed inside a native Windows window
- Keeps Destiny.gg links inside the app and opens non-Destiny links in your default browser
- Saves window size, last page, mute state, pin-on-top state, and zoom level
- Adds desktop controls for navigation, zoom, mute, and update checks
- Can update itself from GitHub Releases by downloading a new portable package and restarting through a small PowerShell handoff script

## Files

- `DestinyChatDesktop.cs`: WinForms + WebView2 source
- `appsettings.json`: Startup settings
- `build.ps1`: Downloads the pinned WebView2 SDK package, builds the app, and can emit a portable release zip
- `Run Destiny Chat.cmd`: Builds if needed, then launches the app
- `.github/workflows/release.yml`: Builds and publishes the portable package when you push a matching `v*` tag

## Usage

1. Run `Run Destiny Chat.cmd`
2. Sign in to Destiny.gg inside the app if you want full chat interaction
3. Use `F5` to reload and `F11` to toggle fullscreen
4. Use the `Update` toolbar button for a manual GitHub update check

## Notes

- The actual chat, login flow, and site functionality still come from the live Destiny.gg website.
- This project uses the archived `destinygg/website` repo for historical reference and the `destinygg/chat-gui` repo for current chat embed behavior.
- GitHub releases should be tagged as `v<appVersion>`, for example `v0.1.1`.
- The updater looks for the `DestinyChatDesktop-portable.zip` asset on the latest GitHub release for the configured repo.
