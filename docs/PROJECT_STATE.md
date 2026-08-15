# Project state

Durable checkpoint for future sessions. Do not treat chat history as source of truth; this file and the rest of `docs/` are.

## Current phase

**Phase 3A (service REST integration) — COMPLETE.**

Manually validated on Windows 11 over the corporate VPN against Checkmk CRE/RAW `2.4.0p34`.

Phase 3B (host monitoring) has **not** started.

## Git checkpoint

- Phase 2 completion: `2b85065` — `Mark Phase 2 as Windows-tested and complete`
- Prior public checkpoint: `a42d0c1` — `Add Phase 1 core and Phase 2 mock WPF UI`
- This record: Phase 3A implementation plus live service-REST validation

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

Implemented:

- WPF app (`net8.0-windows`), MVVM, DI
- Compact Always-on-Top bar
- Expandable problem list (NEW, CRITICAL, WARNING, UNKNOWN)
- Local eye / Mark all new as seen (never Checkmk ACK)
- EN + PL resource strings
- `DemoSnapshotFactory` + `DemoBootstrapper` mock scenario
- `LastSuccessfulPollUtc` exposed for the compact bar
- `Window.Owner` assigned only after `CompactBarWindow.Show()` (startup crash fix)

### Windows 11 manual validation (Phase 2)

Environment: self-contained **win-x64** executable, **no Administrator privileges**.

Confirmed: compact bar stays running, Always-on-Top, mock counters, expandable list, NEW first, severity sections, host and service rows, plugin output, local Seen/eye, ACK badge independent of Seen, scrolling, Owner-before-Show crash gone.

## Phase 3A — complete (service REST only)

Implemented:

- `CheckmkDesktopNotifier.Infrastructure` (`net8.0`): config, automation-user auth header, `HttpClient`, verified service POST, REST DTOs, mapping to Core `MonitoredProblem`
- `CheckmkDesktopNotifier.ConnectionTest`: one-shot read-only POST; prints HTTP status and WARN/CRIT/UNKNOWN counts only
- Mock/Real switch (`Mode`); default remains **Mock**; `MockCheckmkClient` kept
- Local config file + environment variables; secrets are not committed (`config/checkmk.local.json` gitignored; example committed)
- Infrastructure tests: JSON mapping, severity/state_type/ACK/downtime/unix timestamps, malformed JSON, HTTP errors, auth header, config validation, Core independence from REST DTOs

Core was not changed for Phase 3A. `ICheckmkClient` already returns `ProblemSnapshot`.

### Windows 11 live validation (Phase 3A)

Environment: Windows 11, corporate VPN, dedicated Checkmk automation account, **no Administrator privileges**.

Confirmed path:

Windows 11 → VPN → Checkmk REST API → automation authentication → `POST /domain-types/service/collections/all` → non-OK service query → REST response mapping → Core `ProblemSnapshot`

Sanitized live result from the one-shot connection test:

```
HTTP status: 200
Service problems: 129
WARN: 15
CRIT: 111
UNKNOWN: 3
```

Automation account (no secret recorded):

- Authenticates with an automation secret
- Role: **Normal monitoring user**
- Contact group: **Everything**
- Does not require Administrator privileges

Not in Phase 3A (still not started):

- Host GET collection / host monitoring
- Background polling timer
- Tray, toast, sound
- Checkmk acknowledge / downtime / comment APIs
- DPAPI credential store / settings UI
- Removing mock mode

## Tests

Last automated run (Linux agent, after Phase 3A completion docs):

```
dotnet build CheckmkDesktopNotifier.sln   → 0 errors, 0 warnings
dotnet test  CheckmkDesktopNotifier.sln   → 64 passed, 0 failed
  Core.Tests:            20 passed
  Infrastructure.Tests:  44 passed
```

Re-run after any further change. Record the new numbers here if they change.

## What is NOT implemented

- Host monitoring (Phase 3B)
- Polling timer (60s default is config only)
- System tray, toast, sound, mute
- Windows startup / single-instance
- Host-DOWN notification coalescing
- Settings UI / DPAPI
- Persistent window position on disk
- MIT `LICENSE`, `README.md`, `README.pl.md`, packaging/release (Phase 5)
- Windows Service (out of V1 by decision)

## Immediate next steps

Do **not** start Phase 3B (host GET) until remaining host facts in `docs/CHECKMK_API.md` are verified. Do not invent a host POST. Do not use `host_config`.
