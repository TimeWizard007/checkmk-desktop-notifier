# Project state

Durable checkpoint for future sessions. Do not treat chat history as source of truth; this file and the rest of `docs/` are.

## Current phase

**Phase 2 (mock WPF UI) — COMPLETE.**

Manually validated on Windows 11 with a self-contained win-x64 publish (no Administrator privileges).

Phase 3 (real Checkmk HTTP) has **not** started.

## Git checkpoint

- Prior public checkpoint: `a42d0c1` (`a42d0c12c8f533fed61a08ed5fa850b31e5c572d`) — `Add Phase 1 core and Phase 2 mock WPF UI`
- After that commit: Owner-before-Show crash fix, `docs/` added, then this Phase 2 completion record

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

### Windows 11 manual validation

Environment: self-contained **win-x64** executable, **no Administrator privileges**.

Confirmed:

- Process starts and does not exit immediately
- `CompactBarWindow` opens and remains running
- Always-on-Top compact bar is visible
- Mock counters display correctly
- Clicking the compact bar opens `ProblemListWindow`
- NEW section is displayed first
- CRITICAL / WARNING / UNKNOWN sections are displayed
- Host and service problems render correctly
- Plugin output is displayed
- Seen / eye controls are available for NEW incidents
- ACK badge is displayed independently from Seen
- Scrolling works
- Previous Owner-before-Show Event Log crash is gone

Known Phase 2 limitations (accepted, not blockers):

- Window position is in-memory only
- UI language follows OS culture (no in-app switcher)
- App uses `InMemoryAlertStateStore` (Seen resets on restart)
- No automated WPF UI tests

## Tests

Last automated run (Linux agent, after Phase 2 completion docs):

```
dotnet build CheckmkDesktopNotifier.sln   → 0 errors, 0 warnings
dotnet test  CheckmkDesktopNotifier.sln   → 20 passed, 0 failed
```

Re-run after any further change. Record the new numbers here if they change.

## What is NOT implemented

- Real Checkmk REST client, credentials, DPAPI
- Polling timer (60s default is a decision only)
- System tray, toast, sound, mute
- Windows startup / single-instance
- Host-DOWN notification coalescing (model allows it; UI/notify layer does not)
- Settings UI
- Persistent window position on disk
- MIT `LICENSE`, `README.md`, `README.pl.md`, packaging/release (Phase 5)
- Windows Service (out of V1 by decision)

## Immediate next steps

Phase 3 is the next implementation phase, but it must not start until explicitly approved.

When approved: verify remaining host GET facts in `docs/CHECKMK_API.md` (UNVERIFIED), then implement a read-only REST adapter behind `ICheckmkClient`.
