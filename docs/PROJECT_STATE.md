# Project state

Durable checkpoint for future sessions. Do not treat chat history as source of truth; this file and the rest of `docs/` are.

## Current phase

**Phase 3C (background polling + JSON persistence) — COMPLETE.**

Manually validated on Windows 11 over the corporate VPN against Checkmk CRE/RAW `2.4.0p34`.

Do **not** start Phase 3D yet. Do **not** start Phase 4 (tray / toasts / sound / host-DOWN grouping).

## Git checkpoint

- Phase 3A completion: `7a108ff` — `Complete Phase 3A real Checkmk service integration`
- Phase 3B completion: `1ad02e3` — `Complete Phase 3B real Checkmk host integration`
- Phase 2 completion: `2b85065` — `Mark Phase 2 as Windows-tested and complete`
- This record: Phase 3C polling and persistence, Windows-validated

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

## Tests

Last automated run (Linux agent, after marking Phase 3C complete):

```
dotnet build CheckmkDesktopNotifier.sln   → 0 errors, 0 warnings
dotnet test  CheckmkDesktopNotifier.sln   → 103 passed, 0 failed
  Core.Tests:            21 passed
  Infrastructure.Tests:  82 passed
```

Re-run after any further change. Record the new numbers here if they change.

## What is NOT implemented

- System tray, toast, sound, mute
- Windows startup / single-instance
- Host-DOWN notification coalescing
- Settings UI / DPAPI
- Persistent window position on disk
- MIT `LICENSE`, `README.md`, `README.pl.md`, packaging/release (Phase 5)
- Windows Service (out of V1 by decision)

## Immediate next steps

Do **not** start Phase 3D until explicitly approved. Do not start Phase 4. Do not invent a host POST. Do not use `host_config`.
