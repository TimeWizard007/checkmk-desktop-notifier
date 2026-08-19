# Architecture

Checkmk Desktop Notifier is a per-user desktop companion for Checkmk. The released Windows product is a WPF host (`CheckmkDesktopNotifier.App`, v1.2.0). The Avalonia macOS host (`CheckmkDesktopNotifier.App.MacOS`, v1.3.0-beta.1) shares Core and Infrastructure. It is not a replacement for the Checkmk web UI. It tracks **local** notification state for current monitoring problems. End-user documentation is `README.md` / `README.pl.md`. This file is the technical architecture. macOS is a **public beta**, not a stable product release.

English is the language of source code, identifiers, comments, and commit messages. User-visible UI is localizable (`en`, `pl`).

## Solution / projects

```
CheckmkDesktopNotifier.sln
  src/CheckmkDesktopNotifier.Core                 net8.0 class library
  src/CheckmkDesktopNotifier.Infrastructure       net8.0 class library (Checkmk REST, polling, settings)
  src/CheckmkDesktopNotifier.Platform.Windows     net8.0-windows (Credential Manager, HKCU Run, shell URI, LocalAppData paths)
  src/CheckmkDesktopNotifier.Platform.MacOS       net8.0 (Application Support, Keychain, /usr/bin/open, NSStatusItem)
  src/CheckmkDesktopNotifier.App                  net8.0-windows WPF (WinExe) — released Windows host; do not rename
  src/CheckmkDesktopNotifier.App.MacOS            net8.0 Avalonia (WinExe) — macOS host (M0–M4 COMPLETE / Intel macOS tested); v1.3.0-beta.1 public tester pre-release, not a stable product
  src/CheckmkDesktopNotifier.ConnectionTest       net8.0 console (one-shot service POST or `--hosts` GET)
  tests/CheckmkDesktopNotifier.Core.Tests         xUnit, net8.0
  tests/CheckmkDesktopNotifier.Infrastructure.Tests  xUnit, net8.0
  tests/CheckmkDesktopNotifier.Platform.MacOS.Tests  xUnit, net8.0 (no real Keychain / AppKit)
  tests/CheckmkDesktopNotifier.App.MacOS.Tests    xUnit, net8.0 (menu-bar/filter/startup projection)
```

Core has no WPF, no Avalonia, no `HttpClient`, and no Checkmk JSON envelope types.

Infrastructure references Core. It owns REST DTOs, authentication headers, `HttpClient`, and mapping `value[].extensions` → `MonitoredProblem`. It does **not** P/Invoke Credential Manager or Keychain.

`Platform.Windows` references Core + Infrastructure. It owns Windows-only implementations of shared ports.

`Platform.MacOS` references Core + Infrastructure. It owns macOS-only implementations of shared ports. It must not reference `Platform.Windows`.

App (Windows) references Core, Infrastructure, and Platform.Windows only. App.MacOS references Core, Infrastructure, and Platform.MacOS only. Tests: Core.Tests → Core only. Infrastructure.Tests → Core + Infrastructure. Platform.MacOS.Tests → Core + Infrastructure + Platform.MacOS.

The Avalonia macOS host must not convert or replace the WPF App.

## Core responsibilities

- Domain: `SiteId`, `ObjectKind`, `MonitoredObjectId`, `Severity`, `StateType`, `MonitoredProblem`, `ProblemSnapshot`
- Incident engine: `IAlertStateService` / `AlertStateService`
- Read-only Checkmk port: `ICheckmkClient` → `ProblemSnapshot`
- Optional Checkmk write port: `ICheckmkAcknowledgementClient` (ACK create/delete; not mixed into the read client)
- Take / Release workflow: `ITakeService` / `TakeEligibility` / `CdnTakeComment`
- Checkmk GUI navigation: `CheckmkGuiUriBuilder` / `ICheckmkProblemNavigator` (no REST `show` hrefs)
- URI opening port: `IUriLauncher` (Windows: `WindowsShellUriLauncher`; macOS: `MacOpenUriLauncher`)
- UI thread port: `IUiThread` (Windows WPF: `WpfDispatcherUiThread` in App; macOS Avalonia: `AvaloniaUiThread` in App.MacOS)
- User-data directory port: `IUserDataDirectory` (Windows: `WindowsUserDataDirectory` → `%LocalAppData%\CheckmkDesktopNotifier`; macOS: `MacUserDataDirectory` → `~/Library/Application Support/CheckmkDesktopNotifier`)
- Persistence port: `IAlertStateStore` (`InMemoryAlertStateStore`, `JsonAlertStateStore`)
- Mock: `MockCheckmkClient`, `DemoSnapshotFactory`

Core must stay independently testable. Core does not read `%LocalAppData%` or `%LocalAppData%\Programs\CheckmkDesktopNotifier`; `InstallLayout` takes an explicit LocalAppData root.

## Infrastructure responsibilities

- `CheckmkOptions` / loader / validation (`Mode`, `BaseUrl`, `Site`, `Username`, `Secret`, `PollIntervalSeconds`)
- Automation-user header: `Authorization: Bearer <username> <automation_secret>`
- `CheckmkRestClient` : `ICheckmkClient` (Real mode) — service POST + host GET with `columns=`, merged snapshot
- `CheckmkServiceClient` — verified `POST /domain-types/service/collections/all`
- `CheckmkHostClient` — verified `GET /domain-types/host/collections/all` (name-only or `columns=`)
- REST request/response DTOs (Infrastructure only)
- `ServiceProblemMapper` / `HostProblemMapper` → Core `MonitoredProblem`
- Failed HTTP/JSON → `ProblemSnapshot.Failure` (`Unavailable` / `Authentication` / `Protocol` / `Configuration`)
- `CheckmkPoller` / `CheckmkPollingHostedService` — background polling while the desktop app is running (not a Windows Service)
- `PollDiagnosticsWriter` — `last-poll.txt` under the platform user-data directory
- GUI settings store, `ISecretStore` (interface + in-memory), `CheckmkConfigurationResolver`, `MonitoringCoordinator`, `CheckmkConnectionTester`
- Notification policy (`INotificationCoordinator` / `NotificationCoordinator`) maps `AlertDelta.Opened` to desktop alerts after ACK suppression and host-failure grouping; `IUserPreferences` / `preferences.json` (mute, volume, Default vs Custom WAV, Take enabled, display name)
- `ICheckmkAcknowledgementClient` / `CheckmkAcknowledgementClient` — optional Take writes (`POST .../acknowledge/collections/service` and `.../host`) and Release deletes (`POST .../acknowledge/actions/delete/invoke`)
- `CheckmkTakeService` — ACK POST or delete then `IProblemPoller.RefreshWhenIdleAsync` (no second poll loop)
- `INotificationService` / `IAlertSoundService` abstractions (Windows balloon/sound live in App); PCM volume scaling and WAV validation live in Core

## App responsibilities

- Composition root (`App.xaml.cs` + `Microsoft.Extensions.Hosting` DI)
- WPF views (`CompactBarWindow`, `ProblemListWindow`, `SettingsWindow`, `AboutWindow`)
- ViewModels that **project** Core state and **invoke** Core / shell commands
- Localization (`Strings.resx`, `Strings.pl.resx`, `ILocalizationService`)
- Shared shell commands (`IShellCommands` / `UiShell`) for bar, Hide to tray, Settings, About, Exit
- `IUiThread` / `WpfDispatcherUiThread` for poller → list marshaling (no `Application.Current.Dispatcher` in ViewModels)
- System tray (`NotifyIconTray` via WinForms `NotifyIcon`) including balloon notifications; left-click toggles bar visibility
- Alert sound (`WindowsAlertSoundService` / bundled `notifier.wav`), per-app volume, optional imported custom WAV, and mute (shared `IUserPreferences`)
- Dark compact gear/tray menu styling; dark problem-list `WindowChrome`, scrollbar, and `Button` templates (no default Aero2 chrome)
- Presentation-only problem-list filter (`ProblemListFilter`); compact-bar counters **toggle** filtered views
- Window chrome only in code-behind (drag, click vs drag). Compact-bar mouse handling walks parents with `DependencyObjectAncestors` (ContentElement / `Run` before `VisualTreeHelper`) and ignores settings-gear and counter `Button` descendants.
- Mock vs Real client selection via configuration resolver + `MonitoringCoordinator`
- Real-mode background polling via `AddCheckmkPolling` + `IHost.StartAsync()`
- Mock-only demo bootstrap (`DemoBootstrapper`); Real never injects `DemoSnapshotFactory`
- Connection status projection (`Initializing` / `Setup required` / `Connected` / `Refreshing` / `Connection error`)

App must not implement NEW / SEEN / RECOVERED itself.

## Platform split (Phase M0 COMPLETE / Windows-tested; Phases M1–M4 COMPLETE / Intel macOS tested; macOS v1.3.0-beta.1 public tester pre-release)

**Shared**

- Core: domain, incident engine, Take/Release eligibility, GUI URI builder, `IUiThread`, `IUriLauncher`, `IUserDataDirectory`, `IAutostartStore` policy (`AutostartService`)
- Infrastructure: Checkmk REST, polling, settings/preferences JSON, `ISecretStore` port, notification *policy*, WAV import/validation helpers, `GuiConfigurationService`, `CheckmkConnectionTester`, `MonitoringCoordinator`

**Windows (released v1.2.0 host — do not convert to Avalonia)**

- WPF shell (`CheckmkDesktopNotifier.App`)
- WinForms tray / balloon (`NotifyIconTray`)
- `SoundPlayer` (`WindowsAlertSoundService`)
- `WpfDispatcherUiThread`
- `SingleInstanceGuard` using `Local\` mutex/event (`SingleInstanceIdentity`)
- Inno Setup per-user installer
- `Platform.Windows`: Credential Manager, HKCU Run, `WindowsShellUriLauncher`, `WindowsUserDataDirectory` / `WindowsInstallLayout`

Windows user-data and install paths are unchanged:

- data: `%LocalAppData%\CheckmkDesktopNotifier`
- binaries: `%LocalAppData%\Programs\CheckmkDesktopNotifier`

**macOS (Phases M1–M4 COMPLETE / Intel macOS tested — v1.3.0-beta.1 public tester pre-release, not a stable product)**

- Avalonia host (`CheckmkDesktopNotifier.App.MacOS`) — composition root `MacDesktopHost`; host version override `1.3.0-beta.1`
- Phase M1: connection Settings window, Keychain, shared poller smoke — COMPLETE / Intel macOS tested
- Phase M2 COMPLETE / Intel macOS tested: `NSStatusItem` menu-bar counts, problem panel, shared filters/search, local Seen/Unseen, Open in Checkmk. Native IMPs marshal panel show/hide through Avalonia `PostDeferred`. Intel does not query `NSRect` via `objc_msgSend`.
- Phase M3 COMPLETE / Intel macOS tested: shared Take/Release (`CheckmkTakeService`), complete Settings, `NotificationCoordinator` + native `NSUserNotificationCenter` delivery **only from a `.app` bundle**, `NSSound` via `MacAlertSoundService`, LaunchAgent Start at Login via `/usr/bin/open` of the `.app`, file-lock single instance. `UNUserNotificationCenter.currentNotificationCenter` is gated on `NSBundle.mainBundle.bundleIdentifier`; a raw executable must not call it.
- Phase M4 COMPLETE / Intel macOS tested: polished panel/Settings/dialogs, system appearance Dark/Light dictionaries
- `Platform.MacOS`: `MacUserDataDirectory`, `MacKeychainSecretStore` / `SecurityFrameworkKeychain`, `MacOpenUriLauncher` (`/usr/bin/open`), `NativeMacStatusItem`, `NativeMacNotificationService`, `MacAlertSoundPlayer`, `MacLaunchAgentAutostartStore`, `MacSingleInstanceLock`
- `AvaloniaUiThread` in App.MacOS (not WPF)
- User data: `~/Library/Application Support/CheckmkDesktopNotifier` (`settings.json`, `preferences.json`, `state/`, `last-poll.txt`)
- Automation secret: macOS Keychain generic password, service `CheckmkDesktopNotifier`, account = `SecretStoreKeys.AutomationSecret`. No plaintext fallback. No `InMemorySecretStore` in the macOS host.
- Start at Login: per-user LaunchAgent plist (not HKCU, not SMAppService). SMAppService needs a signed `.app` and is deferred.
- Distribution: unsigned `Checkmk Desktop Notifier.app` ZIPs for `osx-x64` and `osx-arm64` (see `scripts/build-macos-beta.sh`). Not notarized. No DMG/PKG.
- Broader beta still required: native notification delivery, permission prompts, sleep/wake, VPN reconnect, Apple Silicon devices, light-mode polish, long-running stability
- Not yet: signing/notarization, stable macOS product release

`CheckmkGuiUriBuilder` stays shared and unchanged. App.MacOS must not reference WPF, WinForms, Registry, Credential Manager, or the Inno installer.

## Dependency flow

```
Views  →  ShellViewModel  →  IAlertStateService  →  in-memory (Mock) / JSON (Real)
                │
                ├── ITakeService (optional Take/Release → Checkmk ACK write/delete, then refresh)
                ├── ICheckmkProblemNavigator (GUI URL + default browser; no state change)
                ├── IProblemPoller (StateChanged → Reload)
                │         └── INotificationCoordinator (AlertDelta.Opened only; ACK’d Opened = no balloon/sound)
                └── ICheckmkClient (read-only)
                      ├── MockCheckmkClient          (Mode=Mock, default; no REST polling; no real ACK writes)
                      └── CheckmkRestClient          (Mode=Real: services + HARD host DOWN/UNREACH)
```

Startup:

1. Acquire per-user single-instance mutex (`Local\TimeWizard007.CheckmkDesktopNotifier`). A second launch signals the existing instance to show the bar and exits.
2. Resolve configuration (`CheckmkConfigurationResolver`): `CHECKMK_CONFIG` → GUI `settings.json` + Credential Manager → discovered `checkmk.local.json` / env → unconfigured.
3. Build DI host (Mock vs Real stores/clients as above). Register poller + hosted service.
4. Resolve `CompactBarWindow`, set `Application.MainWindow`, resolve `UiShell`, **show the bar and tray immediately** with status **Initializing**. Problem list owner is attached after the bar is shown. Expanding the list is disabled until Ready.
5. Mock: `DemoBootstrapper`. Real with usable config: `MonitoringCoordinator.ApplyAsync`. Unconfigured: no poll loop.
6. `IHost.StartAsync()` starts polling when enabled.
7. Mark the shell **Ready**. First-run then opens Settings.

On Exit: cancel monitoring, close Settings/About, hide the problem list, dispose tray, `Application.Shutdown()`. OnExit: `IHost.StopAsync()` cancels the poll loop.

## Configuration and secrets

No URL, site, username, or automation secret is hardcoded.

End-user sources:

- `%LocalAppData%/CheckmkDesktopNotifier/settings.json` (non-secret fields only; Windows)
- `~/Library/Application Support/CheckmkDesktopNotifier/settings.json` (non-secret fields only; macOS M1)
- `%LocalAppData%/CheckmkDesktopNotifier/preferences.json` (mute, volume, Default vs Custom, Take enabled, display name; not secrets; not cleared by Reset; **not** autostart)
- `%LocalAppData%/CheckmkDesktopNotifier/assets/custom-notification.wav` (imported custom sound copy)
- `%LocalAppData%/CheckmkDesktopNotifier/` — Windows user data (settings, preferences, custom WAV, alert-state). **Not** overwritten by the installer.
- `%LocalAppData%/Programs/CheckmkDesktopNotifier/` — installed binaries (Phase 4D, COMPLETE / Windows-tested). Separate from user data.
- Windows Credential Manager Generic Credential `CheckmkDesktopNotifier` (automation secret)
- macOS Keychain generic password service `CheckmkDesktopNotifier` (automation secret; Phase M1). Never written to settings JSON.
- Per-user autostart: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` value `CheckmkDesktopNotifier` (quoted exe path only; OS is source of truth; installer and Settings share this value)

Developer/CI sources (do not override saved GUI settings unless `CHECKMK_CONFIG` is set):

- `config/checkmk.local.json` (gitignored) or `CHECKMK_CONFIG`
- Environment: `CHECKMK_MODE`, `CHECKMK_BASE_URL`, `CHECKMK_SITE`, `CHECKMK_USERNAME`, `CHECKMK_SECRET`, `CHECKMK_POLL_INTERVAL_SECONDS`

Committed example only: `config/checkmk.local.json.example`. Default `Mode=Mock`, default `PollIntervalSeconds=60`.

API base URI: `{BaseUrl}/{Site}/check_mk/api/1.0/`.

## State engine

`AlertStateService.ApplySnapshot`:

- Failed snapshot (`IsSuccess == false`): no mutations, no recoveries.
- Successful snapshot: current set = HARD non-OK problems only (`StateType.Hard`).
- Object missing from current set → Recovered (Seen discarded).
- New object → NEW incident (`IsSeen = false`).
- Same object, same recurrence marker → same incident; severity may change (`WARN → CRIT`).
- Same object, newer usable recurrence marker → Recovered + NEW (offline OK gap).

`MarkSeen` / `MarkUnseen` / `MarkAllNewAsSeen` are local only. Checkmk `acknowledged` is shared metadata, never local Seen. Take writes ACK through `ICheckmkAcknowledgementClient`; Release deletes a CDN Take the same way. Neither marks Seen. Same incident identity always refreshes ACK/Taken fields from the current snapshot. Mark unseen does not create `AlertDelta.Opened`.

Notifications (Phase 4B COMPLETE / Windows-tested; Phase 4C grouping COMPLETE / Windows-tested; Phase 6A ACK suppression COMPLETE / Windows-tested) consume `AlertDelta.Opened` after `HostFailureNotificationGrouping`. If the Opened incident is already acknowledged in that snapshot, no balloon and no sound are emitted (the row may remain locally NEW). Releasing an ACK is not a new Opened incident. Grouping hosts that are already ACK’d produce no grouped balloon/sound; child incidents stay listed. Core does not depend on WPF, WinForms, toast APIs, or the Windows registry. Grouping never hides Core incidents. Autostart uses an `IAutostartStore` abstraction; the Windows implementation writes HKCU Run only. Version numbers come from `Directory.Build.props` (`1.2.0`). Phase 4D Inno Setup (COMPLETE / Windows-tested) installs binaries under `%LocalAppData%/Programs/CheckmkDesktopNotifier`; user data stays under `%LocalAppData%/CheckmkDesktopNotifier`. Phase 5 (COMPLETE / V1 READY) is documentation, versioning, and packaging for 1.0.0. Phase 6A (COMPLETE / Windows-tested) is optional Take / shared sticky Checkmk ACK. Phase 6B (COMPLETE / Windows-tested) is Open in Checkmk plus reversible local Seen/Unseen. Phase 7A (COMPLETE / Windows-tested) is safe Release of CDN Takes. v1.2.0 is FEATURE COMPLETE / RELEASE CANDIDATE.

**Virgin baseline:** `openIncidentCount == 0 && LastSuccessfulPollUtc is null` before `ApplySnapshot`. If that first snapshot succeeds, Opened incidents are persisted for the UI and **must not** emit notifications/sound. Subsequent successful polls notify only newly Opened incidents.

**Host-failure grouping (Phase 4C, COMPLETE / Windows-tested, notification layer only):** `HostFailureNotificationGrouping.SelectAlerts` runs on the merged successful snapshot. Grouping hosts are HARD host problems with Critical (DOWN) or Unknown (UNREACHABLE). Child services with the same `SiteId` + `HostName` are omitted from balloons/sound while that host is grouping-active. Affected-service count is the snapshot service-problem count for that host. Core incidents stay separate and may remain NEW.

## Incident identity

```
Service: (SiteId, Kind=Service, HostName, ServiceDescription)
Host:    (SiteId, Kind=Host,    HostName)
```

A host named `web01` and a service `web01` / `CPU` are different incidents. Host identity must not include a service description.

## Recurrence markers

- Service: `MonitoredProblem.LastTimeOk` (`last_time_ok`)
- Host: `MonitoredProblem.LastTimeUp` (`last_time_up`)
- Bound on the open incident as `BoundRecurrenceMarker`
- Usable only if timestamp `> UnixEpoch`
- Recurrence if both usable and current `> bound`

## Persistence

`AlertStateDocument` schema version 1. JSON store writes a private DTO (site id, kind, host name, …), not Checkmk REST `value`/`extensions` types. Atomic replace via temp file.

- **Mock:** `InMemoryAlertStateStore` (state dies with the process).
- **Real:** `JsonAlertStateStore` at `%LocalAppData%/CheckmkDesktopNotifier/state/<connection-id>/alert-state.json`.
- **Legacy Phase 3C file:** `%LocalAppData%/CheckmkDesktopNotifier/alert-state.json` is a **read fallback only**. It is not copied, moved, or deleted automatically. Saves always go to the isolated path. Fallback stops as soon as the isolated file exists. After that, the root file may be removed **manually** if desired; do not auto-delete user data.

Persisted: open incidents, local Seen, recurrence markers, `LastSuccessfulPollUtc`, normalized ACK fields (`IsAcknowledgedInCheckmk`, `AcknowledgementType`, `TakenByDisplayName`, `IsTakenByNotifier`). Raw `comments_with_extra_info` is not persisted.

Not persisted: automation secret, Authorization header, Checkmk URL/credentials (`config/checkmk.local.json` / environment remains separate), raw REST JSON.

## Mock client

`ICheckmkClient.GetCurrentProblemsAsync` returns a `ProblemSnapshot`. `MockCheckmkClient` never performs HTTP. `DemoSnapshotFactory` builds the Phase 2 scenario (host + services, ACK, downtime, mix of severities). Local Seen is applied by the bootstrapper, not by the snapshot.

Real mode does not call `DemoBootstrapper` and does not start from `DemoSnapshotFactory` data. Mock remains available for UI development. The Mock hosted poller does not call `ICheckmkClient`.

## Polling

`CheckmkPoller.RunLoopAsync`:

1. Poll immediately (`RefreshAsync` → `ICheckmkClient.GetCurrentProblemsAsync` → capture virgin local state → `IAlertStateService.ApplySnapshot` → `INotificationCoordinator.Process`).
2. Wait `Interval - elapsed` (aligned from poll start). If the cycle ran longer than the interval, start the next poll immediately.
3. Repeat until cancellation.

Single-flight: `SemaphoreSlim(1,1)`. Timer/`RefreshAsync` uses `WaitAsync(0)` and **skips** if a cycle is already running. `RefreshWhenIdleAsync` waits, then polls (for a later manual refresh). Only one HTTP request cycle at a time. A failed snapshot is applied as failure (Core does not recover). The loop continues after failures.

`CheckmkPollingHostedService` is a `BackgroundService` started with the WPF process. It runs the loop only when `CheckmkRuntimeProfile.UseBackgroundPolling` is true (`Mode=Real`). It is not a Windows Service. Phase 3C polling and JSON persistence were manually validated on Windows 11.

Live reconfiguration (Phase 3D) uses `MonitoringCoordinator`: cancel the current poll session, replace the REST client and interval, optionally `ReplaceStore` when BaseUrl/Site identity changes, then start a new session. No overlapping poll loops.

## Configuration precedence

Highest first:

1. `CHECKMK_CONFIG` (explicit developer/CI file) + `CHECKMK_*` environment overlays
2. GUI `%LocalAppData%/CheckmkDesktopNotifier/settings.json` + Windows Credential Manager (environment ignored)
3. Discovered `checkmk.local.json` + `CHECKMK_*` overlays
4. `CHECKMK_*` environment variables alone
5. Unconfigured → first-run Settings (no Mock, no polling)

GUI BaseUrl is the server origin only (https), not `/{site}/check_mk/api/1.0/`.

Incident identity includes Checkmk **site name**, not the server URL. Persisted incidents are therefore isolated by `ConnectionIdentity` (normalized BaseUrl + Site) so two servers that share a site name cannot collide. Changing server/site switches files; it does not merge or delete the previous file. Reset configuration deletes `settings.json` and the Credential Manager secret only. `preferences.json` (mute) is left in place.

## WPF ViewModels

`ShellViewModel` reads `GetOpenIncidents()` and `LastSuccessfulPollUtc`. It calls `MarkSeen` / `MarkUnseen` / `MarkAllNewAsSeen` / Open in Checkmk / expand toggle / presentation filter. After each poll, `IProblemPoller.StateChanged` reloads counters, the (possibly filtered) problem list, last-check time, and connection status on the WPF dispatcher. The UI does not implement NEW / SEEN / RECOVERED. The active filter is not stored in Core incident state.

Connection status (compact bar): `Initializing...` until startup finishes; then `Setup required` when no poll session is active; otherwise `Connected`, `Refreshing`, or `Connection error` from **this session's** poller. Persisted `LastSuccessfulPollUtc` may still fill last-check time; it must not imply Connected. A connection error does not replace the problem list. No exception stacks in the main UI.

Sections (ALL filter):

- NEW: `!IsSeen`
- CRITICAL / WARNING / UNKNOWN: all open incidents of that severity (including NEW)

Single-selection filters NEW / CRIT / WARN / UNK / TAKEN show a flat list (Seen CRIT remains visible under CRIT; Seen leaves the NEW view). TAKEN is open incidents with `IsTakenByNotifier` (valid CDN Take). Generic/manual Checkmk ACK is not Taken. Empty filters show a localized empty-state line.

A search field sits below the filter chips and above the list. Search is presentation-only (trim + `OrdinalIgnoreCase` on host name, service description, and Taken-by display name). It composes with the active filter and does not mutate incidents, Seen, or polling. A non-empty search always uses the flat list, including on ALL.

Compact-bar NEW/CRIT/WARN/UNKNOWN/TAKEN counts are global (not search-scoped). Clicking a count **toggles** that filter (same filter closes; a different filter switches in place). Clicking Checkmk / non-counter area toggles the list and opens ALL. Gear does not change the filter.

Eye button is on every active row: open eye = Mark seen (NEW), slashed eye = Mark unseen (Seen). Mark unseen returns the incident to local NEW immediately and never replays balloon/sound (`AlertDelta.Opened` is unchanged). Compact Open-in-Checkmk icon is first in the action cluster, then eye, then Take when available. Open uses `ICheckmkProblemNavigator` and does not mutate incident state. Take is a compact row action when the feature is enabled and the problem is not already ACK’d. CDN Takes show **Taken by {name}**; other ACKs show **ACK**. Checkmk ACK and downtime are badges only and may coexist with Taken. Take never removes the row. Take confirmation is an application-owned dark modal (Take / Cancel), not a Windows MessageBox. Release uses the same chrome. After a successful write, waiting uses the row visual (`Taking...` / `Releasing...`) until Checkmk read-back confirms; do not show a native MessageBox for that. CDN Take write comments are **single-line**; Checkmk RAW 2.4 truncates `\n` in ACK comments. Taken-by is the Release action for CDN Takes in v1.2.0 / Phase 7A.

## Rule: no Checkmk REST DTOs in Core

Core types are English domain names (`HostName`, `PluginOutput`, `IsAcknowledgedInCheckmk`, …).

The REST adapter lives in Infrastructure and maps:

`HTTP JSON value[]` / `extensions` → `MonitoredProblem` / `ProblemSnapshot`

Core must never reference `domainType`, `extensions`, `links`, or Checkmk request bodies.
