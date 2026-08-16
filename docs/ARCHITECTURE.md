# Architecture

Checkmk Desktop Notifier is a per-user Windows desktop companion for Checkmk. It is not a replacement for the Checkmk web UI. It tracks **local** notification state for current monitoring problems.

English is the language of source code, identifiers, comments, and commit messages. User-visible UI is localizable (`en`, `pl`).

## Solution / projects

```
CheckmkDesktopNotifier.sln
  src/CheckmkDesktopNotifier.Core              net8.0 class library
  src/CheckmkDesktopNotifier.Infrastructure    net8.0 class library (Checkmk REST)
  src/CheckmkDesktopNotifier.App               net8.0-windows WPF (WinExe)
  src/CheckmkDesktopNotifier.ConnectionTest    net8.0 console (one-shot service POST or `--hosts` GET)
  tests/CheckmkDesktopNotifier.Core.Tests      xUnit, net8.0
  tests/CheckmkDesktopNotifier.Infrastructure.Tests  xUnit, net8.0
```

Core has no WPF, no `HttpClient`, and no Checkmk JSON envelope types.

Infrastructure references Core. It owns REST DTOs, authentication headers, `HttpClient`, and mapping `value[].extensions` → `MonitoredProblem`.

App references Core and Infrastructure. Tests: Core.Tests → Core only. Infrastructure.Tests → Core + Infrastructure.

## Core responsibilities

- Domain: `SiteId`, `ObjectKind`, `MonitoredObjectId`, `Severity`, `StateType`, `MonitoredProblem`, `ProblemSnapshot`
- Incident engine: `IAlertStateService` / `AlertStateService`
- Read-only Checkmk port: `ICheckmkClient` → `ProblemSnapshot`
- Persistence port: `IAlertStateStore` (`InMemoryAlertStateStore`, `JsonAlertStateStore`)
- Mock: `MockCheckmkClient`, `DemoSnapshotFactory`

Core must stay independently testable.

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
- `PollDiagnosticsWriter` — `%LocalAppData%/CheckmkDesktopNotifier/last-poll.txt` (counts and error kind only)
- GUI settings store, `ISecretStore` / Windows Credential Manager, `CheckmkConfigurationResolver`, `MonitoringCoordinator`, `CheckmkConnectionTester`

## App responsibilities

- Composition root (`App.xaml.cs` + `Microsoft.Extensions.Hosting` DI)
- WPF views (`CompactBarWindow`, `ProblemListWindow`, `SettingsWindow`)
- ViewModels that **project** Core state and **invoke** Core commands
- Localization (`Strings.resx`, `Strings.pl.resx`, `ILocalizationService`)
- Window chrome only in code-behind (drag, click vs drag). Compact-bar mouse handling walks parents with `DependencyObjectAncestors` (ContentElement / `Run` before `VisualTreeHelper`) and ignores settings-gear `Button` descendants.
- Mock vs Real client selection via configuration resolver + `MonitoringCoordinator`
- Real-mode background polling via `AddCheckmkPolling` + `IHost.StartAsync()`
- Mock-only demo bootstrap (`DemoBootstrapper`); Real never injects `DemoSnapshotFactory`
- Connection status projection (`Setup required` / `Connected` / `Refreshing` / `Connection error`)

App must not implement NEW / SEEN / RECOVERED itself.

## Dependency flow

```
Views  →  ShellViewModel  →  IAlertStateService  →  in-memory (Mock) / JSON (Real)
                │
                ├── IProblemPoller (StateChanged → Reload)
                └── ICheckmkClient
                      ├── MockCheckmkClient          (Mode=Mock, default; no REST polling)
                      └── CheckmkRestClient          (Mode=Real: services + HARD host DOWN/UNREACH)
```

Startup:

1. Resolve configuration (`CheckmkConfigurationResolver`): `CHECKMK_CONFIG` → GUI `settings.json` + Credential Manager → discovered `checkmk.local.json` / env → unconfigured.
2. Build DI host.
   - Mock: `InMemoryAlertStateStore`, `MockCheckmkClient`.
   - Real / first-run: isolated `JsonAlertStateStore` when a connection identity exists (legacy root file is read fallback only), `DelegatingCheckmkClient` + `MonitoringCoordinator`.
3. Register `CheckmkPoller` + `CheckmkPollingHostedService`. HTTP timeout is shorter than the poll interval.
4. Mock: `DemoBootstrapper` sets `MockCheckmkClient.NextSnapshot`, `ApplySnapshot`, then local `MarkSeen` on one demo incident.
   Real with usable config: `MonitoringCoordinator.ApplyAsync`. Unconfigured: no poll loop; Settings is shown.
5. Resolve `CompactBarWindow`, set `Application.MainWindow`, resolve `UiShell` so `IProblemPoller.StateChanged` is subscribed.
6. `IHost.StartAsync()`: when polling is enabled, poll immediately, then every `PollIntervalSeconds`.
7. `UiShell.Show()` shows the bar, then attaches `ProblemListWindow.Owner`. First-run opens Settings.

On exit: `IHost.StopAsync()` cancels the poll loop.

## Configuration and secrets

No URL, site, username, or automation secret is hardcoded.

End-user sources:

- `%LocalAppData%/CheckmkDesktopNotifier/settings.json` (non-secret fields only)
- Windows Credential Manager Generic Credential `CheckmkDesktopNotifier` (automation secret)

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

`MarkSeen` / `MarkAllNewAsSeen` are local only. Checkmk `acknowledged` is metadata, never local Seen.

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

Persisted: open incidents, local Seen, recurrence markers, `LastSuccessfulPollUtc`.

Not persisted: automation secret, Authorization header, Checkmk URL/credentials (`config/checkmk.local.json` / environment remains separate).

## Mock client

`ICheckmkClient.GetCurrentProblemsAsync` returns a `ProblemSnapshot`. `MockCheckmkClient` never performs HTTP. `DemoSnapshotFactory` builds the Phase 2 scenario (host + services, ACK, downtime, mix of severities). Local Seen is applied by the bootstrapper, not by the snapshot.

Real mode does not call `DemoBootstrapper` and does not start from `DemoSnapshotFactory` data. Mock remains available for UI development. The Mock hosted poller does not call `ICheckmkClient`.

## Polling

`CheckmkPoller.RunLoopAsync`:

1. Poll immediately (`RefreshAsync` → `ICheckmkClient.GetCurrentProblemsAsync` → `IAlertStateService.ApplySnapshot`).
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

Incident identity includes Checkmk **site name**, not the server URL. Persisted incidents are therefore isolated by `ConnectionIdentity` (normalized BaseUrl + Site) so two servers that share a site name cannot collide. Changing server/site switches files; it does not merge or delete the previous file. Reset configuration deletes `settings.json` and the Credential Manager secret only.

## WPF ViewModels

`ShellViewModel` reads `GetOpenIncidents()` and `LastSuccessfulPollUtc`. It calls `MarkSeen` / `MarkAllNewAsSeen` / expand toggle. After each poll, `IProblemPoller.StateChanged` reloads counters, the problem list, last-check time, and connection status on the WPF dispatcher. The UI does not implement NEW / SEEN / RECOVERED.

Connection status (compact bar): `Setup required` when no poll session is active (unconfigured / after Reset); otherwise `Connected`, `Refreshing`, or `Connection error`. Historical incidents and last-check time may remain visible without implying an active connection. Last successful check remains `HH:mm` from Core (`LastSuccessfulPollUtc` updates only on success). A connection error does not replace the problem list. No exception stacks in the main UI.

Sections:

- NEW: `!IsSeen`
- CRITICAL / WARNING / UNKNOWN: all open incidents of that severity (including NEW)

Eye button is visible only when `IsNew`. Checkmk ACK and downtime are badges only.

## Rule: no Checkmk REST DTOs in Core

Core types are English domain names (`HostName`, `PluginOutput`, `IsAcknowledgedInCheckmk`, …).

The REST adapter lives in Infrastructure and maps:

`HTTP JSON value[]` / `extensions` → `MonitoredProblem` / `ProblemSnapshot`

Core must never reference `domainType`, `extensions`, `links`, or Checkmk request bodies.
