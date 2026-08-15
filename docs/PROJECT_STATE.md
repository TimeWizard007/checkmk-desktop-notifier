# Project state

Durable checkpoint for future sessions. Do not treat chat history as source of truth; this file and the rest of `docs/` are.

## Current phase

**Phase 3B (host REST integration) — COMPLETE.**

Manually validated on Windows 11 over the corporate VPN against Checkmk CRE/RAW `2.4.0p34`.

Phase 3C (polling) has **not** started.

## Git checkpoint

- Phase 3A completion: `7a108ff` — `Complete Phase 3A real Checkmk service integration`
- Phase 2 completion: `2b85065` — `Mark Phase 2 as Windows-tested and complete`
- This record: Phase 3B host GET with `columns=` wired into the real client

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
- No host POST, no `host_config`, no polling, no host-DOWN grouping

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

## Tests

Last automated run (Linux agent, after Phase 3B wiring):

```
dotnet build CheckmkDesktopNotifier.sln   → 0 errors, 0 warnings
dotnet test  CheckmkDesktopNotifier.sln   → 86 passed, 0 failed
  Core.Tests:            20 passed
  Infrastructure.Tests:  66 passed
```

Re-run after any further change. Record the new numbers here if they change.

## What is NOT implemented

- Polling timer (60s default is config only) — Phase 3C
- System tray, toast, sound, mute
- Windows startup / single-instance
- Host-DOWN notification coalescing
- Settings UI / DPAPI
- Persistent window position on disk
- MIT `LICENSE`, `README.md`, `README.pl.md`, packaging/release (Phase 5)
- Windows Service (out of V1 by decision)

## Immediate next steps

Do **not** start Phase 3C (polling) until explicitly approved. Do not invent a host POST. Do not use `host_config`. Server-side host `query` filters remain unused.
