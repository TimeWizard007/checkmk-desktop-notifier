# Project state

Durable checkpoint for future sessions. Do not treat chat history as source of truth; this file and the rest of `docs/` are.

## Current phase

**Phase 4D (per-user installer / upgrade / uninstall) — COMPLETE / Windows-tested.**

Phase 4A, 4B, 4C, and 4D are **COMPLETE / Windows-tested**.

Do **not** start Phase 5.

## Git checkpoint

- Phase 3A completion: `7a108ff` — `Complete Phase 3A real Checkmk service integration`
- Phase 3B completion: `1ad02e3` — `Complete Phase 3B real Checkmk host integration`
- Phase 3C completion: `4604f01` — `Complete Phase 3C polling and persistence`
- Phase 2 completion: `2b85065` — `Mark Phase 2 as Windows-tested and complete`
- Phase 3D complete — `a255b7e` `Complete Phase 3D secure GUI configuration`
- Phase 4B complete — `1ce8616` `Complete Phase 4B notifications and sound controls`
- Phase 4C complete — `337c5a3` `Complete Phase 4C host grouping and autostart`
- Phase 4D: COMPLETE / Windows-tested

## Phase 1 — complete

- .NET 8 Core class library (`CheckmkDesktopNotifier.Core`)
- Domain models: `MonitoredObjectId`, `MonitoredProblem`, `ProblemSnapshot`, `OpenIncident`
- Local lifecycle: NEW / SEEN / RECOVERED
- Recurrence: service `last_time_ok`, host `last_time_up`
- HARD-only incident processing (`state_type` 1)
- `ICheckmkClient` + `MockCheckmkClient`
- `IAlertStateService` + JSON / in-memory persistence
- Unit tests (no WPF, no HTTP)

## Phase 2 — complete

WPF mock UI, Windows 11 self-contained win-x64, no Administrator privileges. Compact bar, expandable list, local Seen, ACK badge independent of Seen.

## Phase 3A — complete (service REST)

Verified `POST /domain-types/service/collections/all` mapped into Core. Mock remains default.

Sanitized live result:

```
HTTP status: 200
Service problems: 129
WARN: 15
CRIT: 111
UNKNOWN: 3
```

Automation account: Normal monitoring user, Everything contact group, no Administrator privileges.

## Phase 3B — complete (host REST)

Implemented:

- `CheckmkRestClient` (`ICheckmkClient` in Real mode): service POST + host GET, merged `ProblemSnapshot`
- Host monitoring: `GET /domain-types/host/collections/all` with repeated `columns=` query-string parameters, no JSON body
- Unfiltered host GET returns `name` only; monitoring uses the `columns=` GET
- Map HARD DOWN → Critical, HARD UNREACHABLE → Unknown; identity `extensions.name`; recurrence `last_time_up`
- Host Kind vs service Kind remain separate; ACK/downtime stay read-only metadata
- `CheckmkDesktopNotifier.ConnectionTest --hosts` probe kept
- If either HTTP call fails, the snapshot is failed
- No host POST, no `host_config`, no host-DOWN grouping

### Windows 11 live validation (Phase 3B)

Environment: Windows 11, corporate VPN, dedicated Checkmk automation account, **no Administrator privileges**.

Unfiltered `GET /domain-types/host/collections/all`:

```
HTTP status: 200
Host objects: 263
Identity field: extensions.name
Fields present: name
```

Same GET with documented repeated `columns=` query-string parameters:

```
HTTP status: 200
Host objects: 263
UP: 262
DOWN: 1
UNREACHABLE: 0
Identity field: extensions.name
Monitoring fields present: name, state, state_type, plugin_output, last_state_change, last_hard_state_change, last_time_up, last_time_down, last_time_unreachable, acknowledged, scheduled_downtime_depth, num_services_hard_crit, num_services_hard_warn, num_services_hard_unknown
Monitoring fields missing: (none)
```

No host names, credentials, secrets, Authorization headers, or plugin outputs recorded.

## Phase 3C — complete (polling + persistence)

Implemented:

- `CheckmkPoller` + `CheckmkPollingHostedService` (desktop hosted service, not a Windows Service)
- Default interval 60s, minimum 10s (`PollIntervalSeconds`)
- Immediate first poll after startup; no overlapping polls (`SemaphoreSlim` single-flight)
- HTTP timeout shorter than the poll interval (`CheckmkOptions.CreateHttpTimeout()`)
- Failed snapshot does not recover/clear incidents; the next cycle continues
- Real mode: JSON alert state at `%LocalAppData%/CheckmkDesktopNotifier/alert-state.json`
- Diagnostics: `%LocalAppData%/CheckmkDesktopNotifier/last-poll.txt` (counts only; no secrets)
- Compact bar connection status: Connected / Refreshing / Connection error
- Mock mode: `DemoBootstrapper` + in-memory store; no REST polling
- Real mode: REST polling; no `DemoSnapshotFactory` injection

### Windows 11 live validation (Phase 3C)

Environment: Windows 11, corporate VPN, dedicated Checkmk automation account, **no Administrator privileges**.

Confirmed:

- Real mode starts successfully
- Immediate startup poll works
- Status transitions to Connected
- Real host + service problems are displayed in the WPF UI
- Background polling repeats approximately every 60 seconds
- `last-poll.txt` updates after successful polling
- Local Seen state survives application restart via JSON persistence
- When VPN / Checkmk connectivity is lost: status becomes Refreshing, then Connection error; existing problems remain visible; Seen is preserved; no false recoveries
- After connectivity is restored, polling resumes normally

No credentials, secrets, Authorization headers, host names, or plugin outputs recorded.

## Phase 3D — complete (GUI settings + Credential Manager)

Self-configuring Windows GUI. End users do not need `checkmk.local.json`, `CHECKMK_CONFIG`, or environment variables.

Implemented:

- First-run Settings when no usable production configuration exists (no failing poll loop)
- Settings UI: URL, site, username, automation secret (PasswordBox), poll interval; Test / Save / Cancel / Reset
- EN/PL resources
- HTTPS origin-only BaseUrl validation
- Automation secret in Windows Credential Manager (Generic Credential `CheckmkDesktopNotifier`, `CRED_PERSIST_LOCAL_MACHINE`, this Windows user, no Administrator)
- Non-secret settings in `%LocalAppData%/CheckmkDesktopNotifier/settings.json` (`baseUrl`, `site`, `username`, `pollIntervalSeconds` only)
- Incident state isolated per connection identity (normalized BaseUrl + Site) under `state/<hash>/alert-state.json`
- Legacy root `alert-state.json` is a **read fallback only** (not copied, moved, or auto-deleted)
- Live Apply without restart via `MonitoringCoordinator` (single poller session, no overlapping loops)
- Reset removes `settings.json` + Credential Manager secret; does not delete alert-state files
- Unconfigured / after Reset: compact bar shows **Setup required**; historical incidents may remain without implying **Connected**
- Mock remains developer-only (`checkmk.local.json` / `CHECKMK_MODE=Mock`); not in the Settings UI
- Compact-bar mouse handling walks Visual / Visual3D / ContentElement (`Run`) parents safely (`VisualTreeHelper.GetParent(Run)` must not be used)

### Configuration precedence (highest first)

1. `CHECKMK_CONFIG` file (explicit developer/CI override), then `CHECKMK_*` env overlays
2. GUI `settings.json` + Credential Manager (env vars ignored)
3. Discovered `checkmk.local.json` + `CHECKMK_*` env overlays
4. `CHECKMK_*` environment variables alone
5. Unconfigured → first-run Settings

The Settings window binds **only** to GUI `settings.json` + Credential Manager. Leftover developer files/env can still start Real monitoring while Settings fields look empty.

### Windows 11 live validation (Phase 3D)

Environment: Windows 11, dedicated Checkmk automation account, **no Administrator privileges**.

Configuration / credential flow:

- Application runs without Administrator privileges
- GUI Settings works; real Checkmk values can be entered entirely through the GUI
- Test connection succeeds; service and host monitoring endpoints are both reachable
- Save starts/applies real monitoring without manual JSON editing
- Restart via `.\CheckmkDesktopNotifier.exe` loads saved GUI configuration without `CHECKMK_CONFIG`

Secure storage:

- `settings.json` contains only `baseUrl`, `site`, `username`, `pollIntervalSeconds` — no automation secret, no Authorization header
- Automation secret stored as Windows Generic Credential named `CheckmkDesktopNotifier` (manually verified)
- Per-connection state: `%LocalAppData%\CheckmkDesktopNotifier\state\<connection-hash>\alert-state.json`
- Legacy root `alert-state.json` remains as non-destructive fallback

Manual tests A–L: **PASS**

- **A.** Settings works. Developer-config precedence was reviewed and documented. Truly unconfigured state uses **Setup required**. Historical incident data does not itself imply an active connection.
- **B.** Enter real Checkmk values.
- **C.** Test connection.
- **D.** Save and start monitoring.
- **E.** Restart without manual JSON/env setup.
- **F.** `settings.json` contains no secret.
- **G.** `alert-state` contains no secret.
- **H.** Poll interval 60 → 20 through Settings, applied without restart. Successful poll timestamps included `10:01:09.646` then `10:01:33.742` (~24s, consistent with a 20s interval plus request time). No overlapping loops.
- **I.** Incorrect secret: authentication/access error; no crash; secret not exposed; incident state intact.
- **J.** Restore correct secret: Test succeeded; monitoring reconnected; **Connected** returned; incident state intact.
- **K.** VPN / connectivity failure: **Refreshing** → **Connection error**; existing problems and Seen remained; no false recoveries; monitoring recovered after connectivity returned.
- **L.** Reset: removed GUI settings and the Credential Manager entry; stopped polling; **Setup required**; alert-state not deleted; no crash. Re-entering valid configuration restored monitoring.

Compact-bar `Run` crash regression: **PASS** after fix. Drag, label click, problem-list toggle, and settings gear work; no crash from `Run`/`TextBlock` routed mouse input.

Security review (confirmed): secret never in `settings.json`, `alert-state.json`, or `last-poll.txt`; Authorization headers never persisted; connection-test errors do not expose credentials; Credential Manager behind `ISecretStore`; Reset removes the stored credential; no hardcoded encryption key; no real secret committed to Git; `config/checkmk.local.json` remains gitignored.

No credentials, secrets, Authorization headers, host names, or plugin outputs recorded.

### Phase 4A — COMPLETE / Windows-tested (desktop shell / UX foundation)

Implemented and manually validated on Windows 11:

- Compact bar is shown immediately with **Initializing...** / **Uruchamianie...**; status is session-based (persisted last-poll time does not imply **Connected**)
- Gear opens a dark context menu: Connection settings, Help / About, Exit (does not drag or toggle the list)
- About dialog: product name, assembly version (`0.4.0` from project metadata, not hardcoded in UI), Author TimeWizard007, clickable GitHub URI
- Shared `IShellCommands` for gear and tray: ShowBar / ShowSettings / ShowAbout / Exit
- Single Settings and About windows (activate if already open)
- Graceful Exit: stop polling, close dialogs, dispose tray, `Application.Shutdown` (not `Environment.Exit`); `ShutdownMode=OnExplicitShutdown`
- Tray: `System.Windows.Forms.NotifyIcon` (no extra package); left-click Open; menu Open / Connection settings / Help About / Exit
- Original placeholder multi-size `Assets/app.ico` (monitor + heartbeat, no Checkmk logo); executable / windows / tray. Replaceable before V1; do not spend time redesigning it in code.
- **Windows 11 manual validation: PASSED** (Initializing, click/drag, gear, Settings, About, version `0.4.0`, GitHub, single-instance dialogs, tray Open/Settings/About, Exit, real monitoring, Seen, VPN). Visual leftover (system-boxed menus) carried into 4B.

### Phase 4B — COMPLETE / Windows-tested (notifications, mute, sound, filter polish)

Implemented and manually validated on Windows 11 (no Administrator privileges):

- Notify only `AlertDelta.Opened` (NEW incidents). Same uninterrupted incident, Seen, WARN→CRIT, failed polls, recoveries, ACK, and downtime do not emit extra notifications.
- Startup baseline: empty local state (`openCount == 0` and `LastSuccessfulPollUtc is null`) — first successful snapshot is ingested into the UI **without** toasts/sound. Later NEW incidents notify. Persisted state continues normal lifecycle (no replay on restart).
- Visual: unpackaged WinForms `NotifyIcon.ShowBalloonTip` (no Windows App SDK, no extra NuGet). Sound: bundled `Assets/notifier.wav` via `SoundPlayer` at per-app PCM volume (default 30%); optional imported custom WAV in LocalAppData `assets/custom-notification.wav`. WAV-only in V1. Deleting the original source file does not break playback.
- Mute: visual still shown; sound off; not pause/Seen/ACK. Gear/tray/Settings share `IUserPreferences`. Persisted in `preferences.json` with volume and Default/Custom (Reset configuration does not clear these).
- Settings: General / Connection / Notifications tabs. Notifications include Default notifier sound / Custom WAV / Volume / Test notification sound / Restore default sound / Mute. Test sound bypasses Mute and does not create incidents.
- Compact-bar counters toggle the problem list (same filter closes; a different filter switches in place with no close/reopen flash). Gear does not change the filter.
- Dark problem list, dark scrollbar, no empty strip after the gear, content-driven compact-bar width. Hide/restore tray and left-click tray toggle work.

**Windows 11 manual validation: PASSED** (counters, Notifications sound UI, volume 100/30/0, custom WAV import + source deletion, restart persistence, Restore default, Mute/Unmute, one NEW → one balloon + one sound, no poll/restart replay). Previously validated 4A/4B behavior also confirmed: baseline suppression, filters, Seen, Mark all new as seen, tray, VPN, Credential Manager, About/Exit, compact-bar sizing.

Host-DOWN grouping/coalescing and Start with Windows are Phase 4C (**COMPLETE / Windows-tested**).

### Phase 4C — COMPLETE / Windows-tested (host grouping + Start with Windows)

Implemented and manually validated on Windows 11 (no Administrator privileges):

**Host DOWN / UNREACHABLE grouping (notification-only):**

- Core `HostFailureNotificationGrouping` plans balloons from the same successful `ProblemSnapshot` + `AlertDelta`. No wall-clock wait.
- A host is grouping-active only when the snapshot contains that **host object** as HARD DOWN (`Severity.Critical`) or HARD UNREACHABLE (`Severity.Unknown`). Child services are never used to infer host failure. Same `SiteId` + `HostName` is required.
- NEW grouping host → one grouped balloon + one sound. Child NEW services on that host in the same snapshot are not notified separately.
- If the host is already DOWN and more child services become NEW later, those children stay NEW in the UI with **no** extra balloons/sounds and **no** repeated host balloon.
- ProblemListWindow / Core still show host + every service incident (severity, NEW/Seen, ACK, downtime, plugin output). Identities are not merged. Grouping does not call `MarkSeen`.
- Affected-service count = number of **service** problems in the merged snapshot with the same `SiteId` + `HostName`. REST `num_services_hard_*` is not mapped onto `MonitoredProblem` and is not used (the snapshot matches what the UI lists).
- DOWN vs UNREACHABLE: `HOST DOWN` / Critical vs `HOST UNREACHABLE` / Unknown.
- ACK / scheduled downtime on the host or on children do **not** suppress the grouped balloon (same as Phase 4B individual notify). They remain metadata, distinct from local Seen and from grouping suppression.
- Mute: grouped balloon yes, sound no. Failed snapshots emit nothing. Recurrence after recovery may notify again.

**Start with Windows (per-user):**

- Settings **General** tab checkbox. Source of truth is the OS entry, not `preferences.json`.
- Mechanism: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` value `CheckmkDesktopNotifier`. Quoted current executable path only. No secrets, no HKLM, no scheduled task, no elevation.
- Enable writes/updates the entry; disable deletes only this value. Opening Settings (and app startup) repairs a stale path if the entry already exists.
- Phase 4D installer must use this **same** HKCU Run value. Do not also create a Startup-folder shortcut for the same option.

**Windows 11 manual validation: PASSED.** Autostart: Settings → General → Start with Windows creates the per-user HKCU Run entry; restart preserves enabled; disable removes the entry; no UAC; Settings / Credential Manager unchanged. Grouping: one grouped host balloon and exactly one sound; child service incidents stayed visible/NEW; child service notifications were suppressed while grouping-active; no service storm; later polls while the host stayed failed did not repeat; after recovery, a later host failure produced a new grouped notification.

### Phase 4D — COMPLETE / Windows-tested (per-user installer)

Implemented and manually validated on Windows 11 (no Administrator privileges):

- **Technology:** Inno Setup 6 (`installer/CheckmkDesktopNotifier.iss`). Free for this use, mature, per-user `PrivilegesRequired=lowest`, simple upgrades via stable `AppId`. MSIX was rejected because packaged identity conflicts with the unpackaged WinForms balloon design.
- **Install:** `%LocalAppData%\Programs\CheckmkDesktopNotifier` (not Program Files). Output `artifacts/CheckmkDesktopNotifier-Setup-x64.exe` (gitignored). Portable `publish/win-x64/` remains the same exe.
- **Version:** `Directory.Build.props` (`0.4.1`) is the single source for assembly/file/product/About and `iscc /DMyAppVersion`. Not v1.0.0.
- **Shortcuts:** per-user Start Menu always; optional desktop shortcut (default off).
- **Autostart:** same HKCU Run value `CheckmkDesktopNotifier` as the app. No Startup folder, scheduled task, or HKLM. Interactive wizard checkbox follows the real Run value; checked writes the installed quoted path, unchecked deletes only that value. Silent install repairs the path if the value already exists and does not delete it.
- **Upgrade:** replaces `{app}` binaries only. User data stays in `%LocalAppData%\CheckmkDesktopNotifier`. Credential Manager `CheckmkDesktopNotifier` is not touched on ordinary upgrade.
- **Running app:** compact bar cancels WM_CLOSE, so Setup uses `AppMutex` (`Local\TimeWizard007.CheckmkDesktopNotifier`) and asks the user to Exit from the tray. No silent kill.
- **Uninstall:** removes binaries, shortcuts, and the app Run value. User data is kept unless the user confirms optional removal (LocalAppData app folder + `cmdkey /delete:CheckmkDesktopNotifier`).
- **Unsigned:** no certificate in-tree. SmartScreen may warn. `SignTool` is left as a commented placeholder.
- **Single-instance:** per-user `Local\` mutex; a second launch shows the existing bar. Portable and installed share the mutex for this Windows user.

**Windows 11 manual validation: PASSED.** `CheckmkDesktopNotifier-Setup-x64.exe` builds and runs as a normal user with no UAC. Install path is `%LocalAppData%\Programs\CheckmkDesktopNotifier`. The installed app launches; the Start Menu shortcut works; existing GUI settings and Credential Manager remain usable. HKCU autostart points at the installed executable and matches Settings. Starting the installed exe while the notifier is already running reuses/activates the existing instance (no second poller, no duplicate notifications).

### Phase 5 / V1 release (keep visible; do not start now)

- Version `1.0.0`
- `README.md` (English)
- `README.pl.md` (Polish)
- Language links between READMEs
- Project overview
- Screenshots
- Feature list
- Windows requirements
- Checkmk requirements
- Installation instructions
- Portable usage instructions
- First-run configuration
- Checkmk automation-user setup
- Credential Manager / security explanation
- NEW vs Seen explanation
- Checkmk ACK / downtime explanation
- HOST DOWN / UNREACHABLE grouping explanation
- Notification / custom WAV / volume / mute documentation
- Tray behavior
- Start with Windows
- Installer upgrade/uninstall behavior
- Build-from-source documentation
- Installer build documentation
- Unsigned installer / SmartScreen note
- License review
- Final icon review
- Final regression checklist
- Final installer build
- Git tag `v1.0.0`
- GitHub Release
- MIT / open-source release hygiene
- No Checkmk logos/trademarks bundled without permission
- Do not create README until Phase 5

## Tests

Last automated run (Linux agent, Phase 4D close-out):

```
dotnet build CheckmkDesktopNotifier.sln   → 0 errors, 0 warnings
dotnet test  CheckmkDesktopNotifier.sln   → 295 passed, 0 failed
  Core.Tests:            131 passed
  Infrastructure.Tests:  164 passed
```

Re-run after any further change. Record the new numbers here if they change.

## What is NOT implemented

- Authenticode signing / trusted SmartScreen reputation
- Persistent window position on disk
- Phase 5: MIT `LICENSE`, `README.md`, `README.pl.md`, screenshots, install docs, GitHub Release, v1.0.0
- Windows Service (out of V1 by decision)

## Immediate next steps

Do not start Phase 5 until asked. Do not create v1.0.0, a Git tag, or a GitHub Release yet. Do not invent a host POST. Do not use `host_config`.
