# Project state

Durable checkpoint for future sessions. Do not treat chat history as source of truth; this file and the rest of `docs/` are.

## Current phase

**Phase 2 (mock WPF UI)** — in progress, with a Windows startup crash **fixed after** git checkpoint `a42d0c1`.

Phase 3 (real Checkmk HTTP) has **not** started.

## Git checkpoint

- Commit: `a42d0c1` (`a42d0c12c8f533fed61a08ed5fa850b31e5c572d`)
- Message: `Add Phase 1 core and Phase 2 mock WPF UI`
- Branch: `main` (pushed to origin at that commit)

Work after that checkpoint:

- Windows startup crash fix (`UiShell` Owner assignment)
- This `docs/` directory

## Phase 1 — complete

- .NET 8 Core class library (`CheckmkDesktopNotifier.Core`)
- Domain models: `MonitoredObjectId`, `MonitoredProblem`, `ProblemSnapshot`, `OpenIncident`
- Local lifecycle: NEW / SEEN / RECOVERED
- Recurrence: service `last_time_ok`, host `last_time_up`
- HARD-only incident processing (`state_type` 1)
- `ICheckmkClient` + `MockCheckmkClient`
- `IAlertStateService` + JSON / in-memory persistence
- Unit tests (no WPF, no HTTP)

## Phase 2 — current

Completed:

- WPF app (`net8.0-windows`), MVVM, DI
- Compact Always-on-Top bar
- Expandable problem list (NEW, CRITICAL, WARNING, UNKNOWN)
- Local eye / Mark all new as seen (never Checkmk ACK)
- EN + PL resource strings
- `DemoSnapshotFactory` + `DemoBootstrapper` mock scenario
- `LastSuccessfulPollUtc` exposed for the compact bar

Fixed after `a42d0c1`:

- **Windows 11 crash:** `System.InvalidOperationException: Cannot set Owner property to a Window that has not been shown previously.`
- Cause: `ProblemListWindow.Owner = CompactBarWindow` in `UiShell` constructor, before the bar had an HWND.
- Fix: assign `Owner` only after `CompactBarWindow.Show()`.

## Tests

At last full run after the Owner fix (Linux agent):

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
- Persistent window position on disk (in-memory only)
- MIT `LICENSE`, `README.md`, `README.pl.md`, packaging/release (Phase 5)
- Windows Service (out of V1 by decision)

## Immediate next steps

1. Re-publish self-contained win-x64 and confirm the compact bar stays open on Windows 11 (no Event Log crash).
2. Manual UI checklist in `docs/DEVELOPMENT.md`.
3. Only then start **Phase 3**: verify remaining host GET query/columns, then implement read-only REST behind `ICheckmkClient`.

Do not start Phase 3 until the Windows startup fix is confirmed on a real Windows 11 run.
