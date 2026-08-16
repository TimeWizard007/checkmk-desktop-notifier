# Development

## Prerequisites

- .NET 8 SDK
- For **running** the WPF app: Windows 10/11
- Linux can **build** Core, Infrastructure, tests, ConnectionTest, and (with Windows targeting) the WPF project
- No Administrator privileges required for build, test, or running the per-user app

## Repository structure

```
checkmk-desktop-notifier/
  CheckmkDesktopNotifier.sln
  docs/                          ← durable project memory (read this first)
  config/checkmk.local.json.example
  src/CheckmkDesktopNotifier.Core/
  src/CheckmkDesktopNotifier.Infrastructure/
  src/CheckmkDesktopNotifier.App/
  src/CheckmkDesktopNotifier.ConnectionTest/
  tests/CheckmkDesktopNotifier.Core.Tests/
  tests/CheckmkDesktopNotifier.Infrastructure.Tests/
```

## Linux — build and test

From the repository root:

```bash
dotnet build CheckmkDesktopNotifier.sln
dotnet test CheckmkDesktopNotifier.sln
```

The App project sets `EnableWindowsTargeting` so WPF can compile on non-Windows agents. That does **not** make the UI runnable on Linux.

## Windows — run the mock UI

Default `Mode` is **Mock**. No Checkmk server required:

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

## Local Checkmk configuration (Phase 3A)

Never commit `config/checkmk.local.json`. Copy the example:

```bash
cp config/checkmk.local.json.example config/checkmk.local.json
```

Edit the `Checkmk` object:

| Field | Notes |
|-------|--------|
| `Mode` | `Mock` (default) or `Real` |
| `BaseUrl` | Origin only, e.g. `https://checkmk.example.invalid` — no site path, no credentials |
| `Site` | Site name, e.g. `itssrv` |
| `Username` | Automation user |
| `Secret` | Automation secret (never commit) |
| `PollIntervalSeconds` | Default `60`, minimum `10`. Used by the Real-mode background poller. |

Environment variables override the file: `CHECKMK_MODE`, `CHECKMK_BASE_URL`, `CHECKMK_SITE`, `CHECKMK_USERNAME`, `CHECKMK_SECRET`, `CHECKMK_POLL_INTERVAL_SECONDS`. Optional path: `CHECKMK_CONFIG`.

The loader also searches `%LocalAppData%/CheckmkDesktopNotifier/checkmk.local.json` and walks up from the current directory looking for `config/checkmk.local.json`.

API URI built from config: `{BaseUrl}/{Site}/check_mk/api/1.0/`.

## One-shot connection test (read-only)

Performs **one** `POST /domain-types/service/collections/all` and prints only HTTP status and problem counts (no secret, no Authorization header, no raw JSON, no plugin output).

From the repository root, with `config/checkmk.local.json` set to `Mode: Real`:

```bash
dotnet run --project src/CheckmkDesktopNotifier.ConnectionTest/CheckmkDesktopNotifier.ConnectionTest.csproj
```

Expected output shape:

```
HTTP status: 200
Service problems: N
WARN: N
CRIT: N
UNKNOWN: N
```

### Windows 11 live validation (Phase 3A)

Confirmed over the corporate VPN with a dedicated automation account (Normal monitoring user, Everything contact group, no Administrator privileges). Sanitized result:

```
HTTP status: 200
Service problems: 129
WARN: 15
CRIT: 111
UNKNOWN: 3
```

Do not log or commit the automation secret, Authorization header, or plugin outputs.

## Host connection test (Phase 3B, complete)

Performs a verified `GET /domain-types/host/collections/all` (no query string), then the live-confirmed `columns=` GET. Prints HTTP status, host object counts, UP/DOWN/UNREACH counts when `state` is present, and monitoring field names. Does not print secrets, Authorization, raw JSON, host names, or plugin output values.

From the repository root, with `config/checkmk.local.json` set to `Mode: Real`:

```powershell
dotnet run --project src/CheckmkDesktopNotifier.ConnectionTest/CheckmkDesktopNotifier.ConnectionTest.csproj -- --hosts
```

Windows self-contained publish of the connection test (no admin):

```powershell
dotnet publish src/CheckmkDesktopNotifier.ConnectionTest/CheckmkDesktopNotifier.ConnectionTest.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -o publish/win-x64-connectiontest
```

Run on Windows 11 over VPN:

```powershell
.\publish\win-x64-connectiontest\CheckmkDesktopNotifier.ConnectionTest.exe --hosts
```

### Windows 11 live validation (Phase 3B)

Unfiltered GET:

```
HTTP status: 200
Host objects: 263
Identity field: extensions.name
Fields present: name
```

GET with repeated `columns=` query-string parameters:

```
HTTP status: 200
Host objects: 263
UP: 262
DOWN: 1
UNREACHABLE: 0
```

Real mode in the WPF app uses that `columns=` GET (HARD DOWN/UNREACHABLE only) together with the service POST. Background polling is Phase 3C (complete).

## Phase 3C — background polling (complete)

Polling runs only while the desktop app is running (`BackgroundService`, not a Windows Service).

| Mode | Startup | Persistence | Polling |
|------|---------|-------------|---------|
| Mock | `DemoBootstrapper` + `DemoSnapshotFactory` | In-memory | Hosted service is a no-op (no REST) |
| Real | No demo snapshot | `%LocalAppData%/CheckmkDesktopNotifier/alert-state.json` | Immediate first poll, then every `PollIntervalSeconds` |

Diagnostics file (no secrets, no Authorization, no host names, no plugin output):

`%LocalAppData%\CheckmkDesktopNotifier\last-poll.txt`

Example success:

```
TimeUtc: ...
Success: true
Problems: N
Hosts: N
Services: N
```

### Windows 11 live validation (Phase 3C)

Confirmed on Windows 11 over the corporate VPN with a dedicated automation account (no Administrator privileges):

- Real mode starts; immediate startup poll; status becomes **Connected**
- Real host + service problems are displayed in the WPF UI
- Background polling repeats approximately every 60 seconds
- `last-poll.txt` updates after successful polling
- Local Seen survives application restart via `%LocalAppData%/CheckmkDesktopNotifier/alert-state.json`
- Connectivity loss: **Refreshing** then **Connection error**; existing problems remain; Seen preserved; no false recoveries
- After connectivity is restored, polling resumes normally

Do not log or commit the automation secret, Authorization header, host names, or plugin outputs.

## Phase 3D — Settings / first-run (complete)

Normal published Windows users configure Checkmk in the GUI. They do not need `checkmk.local.json`, `CHECKMK_CONFIG`, or environment variables.

| Item | Location |
|------|----------|
| Non-secret settings | `%LocalAppData%\CheckmkDesktopNotifier\settings.json` (`baseUrl`, `site`, `username`, `pollIntervalSeconds` only) |
| Automation secret | Windows Credential Manager Generic Credential `CheckmkDesktopNotifier` (this Windows user; no Administrator) |
| Incidents / Seen | `%LocalAppData%\CheckmkDesktopNotifier\state\<identity>\alert-state.json` |
| Diagnostics | `%LocalAppData%\CheckmkDesktopNotifier\last-poll.txt` |

Precedence (highest first): `CHECKMK_CONFIG` → GUI settings + Credential Manager → discovered `checkmk.local.json` + env → env only → first-run Settings.

The Settings window binds only to GUI settings + Credential Manager. Leftover developer files/env can start Real monitoring while Settings fields look empty. Truly unconfigured state shows **Setup required**; historical incident files do not themselves imply **Connected**.

Mock/Demo remains a developer path (`Mode=Mock` in a discovered file or `CHECKMK_MODE=Mock`). It is not offered in Settings.

### Windows 11 live validation (Phase 3D)

Confirmed on Windows 11 with a dedicated automation account (**no Administrator privileges**). Tests A–L passed. Compact-bar `Run` mouse-input crash was found, fixed, and retested.

- GUI Settings, Test connection (services + hosts reachable), Save, restart without `CHECKMK_CONFIG`
- `settings.json` has no secret; Credential Manager entry `CheckmkDesktopNotifier` verified
- Isolated `state\<hash>\alert-state.json`; legacy root file is read-fallback only
- Poll interval 60 → 20 applied live; subsequent successful polls ~24s apart (20s interval plus request time); no overlapping loops
- Wrong secret: auth/access error, no crash, secret not exposed, incident state intact; restoring the secret reconnects
- VPN loss: **Refreshing** → **Connection error**; problems and Seen remain; no false recoveries; recovers when connectivity returns
- Reset removes GUI settings and the Credential Manager entry, stops polling, returns **Setup required**, does not delete alert-state; re-entering config restores monitoring
- Compact bar: drag, label click, problem-list toggle, settings gear; no crash from `Run`/`TextBlock` routed input

Do not log or commit the automation secret, Authorization header, host names, or plugin outputs.

### Legacy `alert-state.json`

Phase 3C wrote `%LocalAppData%\CheckmkDesktopNotifier\alert-state.json`. Phase 3D reads that file **only** when the isolated `state\<hash>\alert-state.json` does not exist yet. It is not copied or auto-deleted. After the isolated file exists, the root file is unused and may be removed **manually** later. Do not delete it as part of Reset or startup.

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

From Linux (cross-compile the Windows app):

```bash
dotnet publish src/CheckmkDesktopNotifier.App/CheckmkDesktopNotifier.App.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=false \
  -o publish/win-x64
```

Run `publish\win-x64\CheckmkDesktopNotifier.exe` on Windows 11.

`publish/` is gitignored. Do not commit publish output.

## Tests

```bash
dotnet test CheckmkDesktopNotifier.sln
```

Core tests cover lifecycle, persistence (including isolated vs legacy alert-state fallback), identities, recurrence, the demo snapshot mix, and compact-bar ancestor/pointer-origin logic (`Run` vs Visual vs Button).

Infrastructure tests cover service JSON mapping, WARN/CRIT/UNKNOWN, SOFT/HARD, ACK/downtime, Unix timestamps, malformed JSON, HTTP non-success, auth header construction, config validation, Core independence from REST DTOs, host collection inspection/mapping, merged service+host snapshots, polling (immediate first poll, interval, no overlap, cancellation, failed poll freeze, persistence reload), GUI settings / Credential Manager / connection tester, and Mock vs Real startup flags.

There is no WPF UI test project yet.

## Secrets

Never commit:

- Checkmk automation secrets / passwords
- API tokens
- `.bin` DPAPI blobs
- production URLs with embedded credentials
- Event Log dumps that contain Authorization headers
- `config/checkmk.local.json` (gitignored)

Phase 3D stores the automation secret in Windows Credential Manager (this Windows user). Developer/CI may still use `config/checkmk.local.json` (gitignored) or `CHECKMK_*` / `CHECKMK_CONFIG`. Do not commit secrets.

## Architecture reminders

- Do not put incident logic in WPF code-behind.
- Do not put Checkmk REST DTOs in Core.
- Do not call Checkmk ACK from the eye button.
- Read `docs/CHECKMK_API.md` before any HTTP work. Host monitoring is verified **GET** with repeated `columns=` query parameters, not an invented POST.
- Phase 3C is complete. Phase 3D is complete. Do not start Phase 4 until explicitly requested.
