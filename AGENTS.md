# Agent / contributor rules

## Goals

- Preserve **behavior parity** with the Destiny.gg embedded chat shell (navigation, bigscreen, dual chat, popouts, updater, self-tests).
- Prefer **small, reviewable changes** over sweeping edits to `EmbeddedApplication.cs`.
- Keep **build green**: `dotnet build DestinyChatDesktop.sln -c Release` and `dotnet test`.

## Project map

- **WPF entry**: `src/DestinyChatDesktop.Wpf/` — startup, `MainWindow`, `AppBootstrap.cs`.
- **Legacy UI host**: `src/DestinyChatDesktop.EmbeddedWinForms/` — `EmbeddedApplication.cs` (large), `JsonLegacy.cs`.
- **Tests**: `tests/DestinyChatDesktop.Tests/`.

Do **not** reintroduce a second standalone compile path for the old root-level single `.cs` file; the embedded project is the UI source of truth.

## Editing guidelines

1. **Touch scope**: When changing chat/embed behavior, locate the relevant handler in `EmbeddedApplication.cs` (search for feature keywords: `bigscreen`, `dual`, `postMessage`, `WebMessageReceived`).
2. **Serialization**: Use the existing `JavaScriptSerializer` shim API (`Serialize` / `Deserialize` / `DeserializeObject`) so loose JSON shapes remain compatible with page scripts.
3. **Threading**: WebView2 events may arrive off the UI thread; follow existing `BeginInvoke` / `Invoke` patterns already in the file.
4. **Self-tests**: Preserve `--self-test-*` CLI switches and `split-self-test.json` output contracts when modifying related code paths.
5. **Updater**: Changing GitHub asset layout or exe naming requires updating `README.md`, release workflow expectations, and possibly `appsettings.json`.

## Verification checklist

After substantive edits:

```powershell
dotnet build DestinyChatDesktop.sln -c Release
dotnet test DestinyChatDesktop.sln -c Release
.\build.ps1
```

Smoke manually: launch `dist\DestinyChatDesktop.exe`, confirm embed loads, navigation + bigscreen toggles, update dialog (dry-run if needed).

## Pull request hygiene

- Describe **user-visible behavior** and **risk areas** (WebView2, window chrome, updater).
- If touching files under `EmbeddedApplication.cs`, cite **feature area** in the PR title (e.g. “bigscreen layout”, “dual chat”, “updater”).
