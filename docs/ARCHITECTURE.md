# Architecture

Checkmk Desktop Notifier is a per-user Windows desktop companion for Checkmk. It is not a replacement for the Checkmk web UI. It tracks **local** notification state for current monitoring problems.

English is the language of source code, identifiers, comments, and commit messages. User-visible UI is localizable (`en`, `pl`).

## Solution / projects

```
CheckmkDesktopNotifier.sln
  src/CheckmkDesktopNotifier.Core          net8.0 class library
  src/CheckmkDesktopNotifier.App           net8.0-windows WPF (WinExe)
  tests/CheckmkDesktopNotifier.Core.Tests  xUnit, net8.0
```

Core has no WPF, no `HttpClient`, and no Checkmk JSON envelope types.

App references Core. Tests reference Core only.

## Core responsibilities

- Domain: `SiteId`, `ObjectKind`, `MonitoredObjectId`, `Severity`, `StateType`, `MonitoredProblem`, `ProblemSnapshot`
- Incident engine: `IAlertStateService` / `AlertStateService`
- Read-only Checkmk port: `ICheckmkClient` → `ProblemSnapshot`
- Persistence port: `IAlertStateStore` (`InMemoryAlertStateStore`, `JsonAlertStateStore`)
- Mock: `MockCheckmkClient`, `DemoSnapshotFactory`

Core must stay independently testable.

## App responsibilities

- Composition root (`App.xaml.cs` + `Microsoft.Extensions.Hosting` DI)
- WPF views (`CompactBarWindow`, `ProblemListWindow`)
- ViewModels that **project** Core state and **invoke** Core commands
- Localization (`Strings.resx`, `Strings.pl.resx`, `ILocalizationService`)
- Window chrome only in code-behind (drag, click vs drag)
- Phase 2 demo bootstrap (`DemoBootstrapper`) using the mock client

App must not implement NEW / SEEN / RECOVERED itself.

## Dependency flow

```
Views  →  ShellViewModel  →  IAlertStateService  →  in-memory/JSON state
                │
                └── ICheckmkClient (mock now; REST later in App or a Checkmk project)
```

Startup (Phase 2):

1. Build DI host.
2. `DemoBootstrapper` sets `MockCheckmkClient.NextSnapshot`, `ApplySnapshot`, then local `MarkSeen` on one demo incident.
3. Set `Application.MainWindow` to `CompactBarWindow`.
4. `UiShell.Show()` shows the bar, then attaches `ProblemListWindow.Owner`.

## State engine

`AlertStateService.ApplySnapshot`:

- Failed snapshot (`IsSuccess == false`): no mutations, no recoveries.
- Successful snapshot: current set = HARD non-OK problems only (`StateType.Hard`).
- Object missing from current set → Recovered (Seen discarded).
- New object → NEW incident (`IsSeen = false`).
- Same object, same recurrence marker → same incident; severity may change (`WARN → CRIT`).
- Same object, newer usable recurrence marker → Recovered + NEW (offline OK gap).

`MarkSeen` / `MarkAllNewAsSeen` are local only.

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

## WPF ViewModels

`ShellViewModel` reads `GetOpenIncidents()` and `LastSuccessfulPollUtc`. It calls `MarkSeen` / `MarkAllNewAsSeen` / expand toggle.

Sections:

- NEW: `!IsSeen`
- CRITICAL / WARNING / UNKNOWN: all open incidents of that severity (including NEW)

Eye button is visible only when `IsNew`. Checkmk ACK and downtime are badges only.

## Rule: no Checkmk REST DTOs in Core

Core types are English domain names (`HostName`, `PluginOutput`, `IsAcknowledgedInCheckmk`, …).

The future REST adapter (Phase 3) must live outside Core (App or a dedicated Checkmk project) and map:

`HTTP JSON value[]` → `MonitoredProblem` / `ProblemSnapshot`

Core must never reference `domainType`, `extensions`, `links`, or Checkmk request bodies.
