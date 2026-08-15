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

Phase 3A/3B do **not** implement acknowledge APIs or a polling timer.

## App responsibilities

- Composition root (`App.xaml.cs` + `Microsoft.Extensions.Hosting` DI)
- WPF views (`CompactBarWindow`, `ProblemListWindow`)
- ViewModels that **project** Core state and **invoke** Core commands
- Localization (`Strings.resx`, `Strings.pl.resx`, `ILocalizationService`)
- Window chrome only in code-behind (drag, click vs drag)
- Mock vs Real client selection via `AddCheckmkClient(options)`
- Phase 2 demo bootstrap (`DemoBootstrapper`) **only when `Mode=Mock`**

App must not implement NEW / SEEN / RECOVERED itself.

## Dependency flow

```
Views  →  ShellViewModel  →  IAlertStateService  →  in-memory/JSON state
                │
                └── ICheckmkClient
                      ├── MockCheckmkClient          (Mode=Mock, default)
                      └── CheckmkRestClient          (Mode=Real: services + HARD host DOWN/UNREACH)
```

Startup:

1. Load `CheckmkOptions` (file + environment). Validate.
2. Build DI host. Register mock **or** real client, not both as the active `ICheckmkClient`.
3. Mock: `DemoBootstrapper` sets `MockCheckmkClient.NextSnapshot`, `ApplySnapshot`, then local `MarkSeen` on one demo incident.
   Real: one `GetCurrentProblemsAsync()` (no timer), then `ApplySnapshot`.
4. Set `Application.MainWindow` to `CompactBarWindow`.
5. `UiShell.Show()` shows the bar, then attaches `ProblemListWindow.Owner`.

## Configuration and secrets

No URL, site, username, or automation secret is hardcoded.

Sources (later values override file):

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

Phase 2 UI uses `InMemoryAlertStateStore` (state dies with the process). Disk persistence exists in Core for tests and later phases.

## Mock client

`ICheckmkClient.GetCurrentProblemsAsync` returns a `ProblemSnapshot`. `MockCheckmkClient` never performs HTTP. `DemoSnapshotFactory` builds the Phase 2 scenario (host + services, ACK, downtime, mix of severities). Local Seen is applied by the bootstrapper, not by the snapshot.

Real mode does not call `DemoBootstrapper`. Mock remains available.

## WPF ViewModels

`ShellViewModel` reads `GetOpenIncidents()` and `LastSuccessfulPollUtc`. It calls `MarkSeen` / `MarkAllNewAsSeen` / expand toggle.

Sections:

- NEW: `!IsSeen`
- CRITICAL / WARNING / UNKNOWN: all open incidents of that severity (including NEW)

Eye button is visible only when `IsNew`. Checkmk ACK and downtime are badges only.

## Rule: no Checkmk REST DTOs in Core

Core types are English domain names (`HostName`, `PluginOutput`, `IsAcknowledgedInCheckmk`, …).

The REST adapter lives in Infrastructure and maps:

`HTTP JSON value[]` / `extensions` → `MonitoredProblem` / `ProblemSnapshot`

Core must never reference `domainType`, `extensions`, `links`, or Checkmk request bodies.
