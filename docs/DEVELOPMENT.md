# Development

## Prerequisites

- .NET 8 SDK
- For **running** the WPF app: Windows 10/11
- Linux can **build** Core, tests, and (with Windows targeting) the WPF project
- No Administrator privileges required for build, test, or running the per-user app

## Repository structure

```
checkmk-desktop-notifier/
  CheckmkDesktopNotifier.sln
  docs/                          ← durable project memory (read this first)
  src/CheckmkDesktopNotifier.Core/
  src/CheckmkDesktopNotifier.App/
  tests/CheckmkDesktopNotifier.Core.Tests/
```

## Linux — build and test

From the repository root:

```bash
dotnet build CheckmkDesktopNotifier.sln
dotnet test CheckmkDesktopNotifier.sln
```

The App project sets `EnableWindowsTargeting` so WPF can compile on non-Windows agents. That does **not** make the UI runnable on Linux.

## Windows — run the mock UI

```powershell
dotnet run --project src/CheckmkDesktopNotifier.App/CheckmkDesktopNotifier.App.csproj
```

Or set `CheckmkDesktopNotifier.App` as the startup project in Visual Studio.

UI language follows Windows UI culture (`en` default, `pl` when UI culture is Polish).

### Manual UI checklist

1. Compact Always-on-Top bar stays open (does not exit immediately).
2. Bar shows NEW / CRIT / WARN / UNKNOWN counts and last check time.
3. NEW is emphasized when count > 0 (no flashing).
4. Bar is draggable; click (not drag) expands the problem list.
5. NEW section first; eye only on NEW; Mark all new as seen works locally.
6. Seen rows remain under real severity. ACK and downtime are badges, not Seen.
7. Closing the list collapses it; closing the bar exits the app.

## Windows — self-contained win-x64 publish

No admin required. From the repository root:

```powershell
dotnet publish src/CheckmkDesktopNotifier.App/CheckmkDesktopNotifier.App.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=false `
  -o publish/win-x64
```

Run `publish\win-x64\CheckmkDesktopNotifier.exe` on Windows 11.

`publish/` is gitignored. Do not commit publish output.

## Tests

```bash
dotnet test CheckmkDesktopNotifier.sln
```

Core tests cover lifecycle, persistence, identities, recurrence, and the demo snapshot mix. There is no WPF UI test project yet.

## Secrets

Never commit:

- Checkmk automation secrets / passwords
- API tokens
- `.bin` DPAPI blobs
- production URLs with embedded credentials
- Event Log dumps that contain Authorization headers

Phase 2 has no credential store. When Phase 3 adds one, keep it under `%LocalAppData%` and out of git.

## Architecture reminders

- Do not put incident logic in WPF code-behind.
- Do not put Checkmk REST DTOs in Core.
- Do not call Checkmk ACK from the eye button.
- Read `docs/CHECKMK_API.md` before any HTTP work. Host monitoring is verified as **GET**, not an invented POST.
