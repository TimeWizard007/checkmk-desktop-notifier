# Roadmap

## Phase 1 — complete

Domain, incident engine, mock `ICheckmkClient`, persistence abstraction, unit tests. No WPF, no HTTP.

## Phase 2 — complete

Mock WPF UI on Core, manually validated on Windows 11 with a self-contained win-x64 publish (no Administrator privileges):

- Compact Always-on-Top bar stays running
- Expanded problem list (NEW first, then CRITICAL / WARNING / UNKNOWN)
- Local Seen (eye, mark all); ACK badge independent of Seen
- Host and service rows, plugin output, scrolling
- EN/PL resources
- Demo snapshot
- Owner-before-Show startup crash fixed and retested

Accepted leftovers (not Phase 3): in-memory window position, in-memory Seen store in the UI host, no in-app language switcher, no automated WPF tests.

## Phase 3A — complete

Real Checkmk **service** REST only (CRE/RAW 2.4.0p34), live-tested from Windows 11 over the corporate VPN:

- Infrastructure adapter behind `ICheckmkClient`
- Automation-user auth, local config / env (no committed secrets)
- Map `value[].extensions` → Core `MonitoredProblem`
- Keep `MockCheckmkClient`; switch via `Mode`
- One-shot connection test console

Sanitized live result:

```
HTTP status: 200
Service problems: 129
WARN: 15
CRIT: 111
UNKNOWN: 3
```

Automation account used **Normal monitoring user** + **Everything** contact group; no Administrator privileges.

Out of Phase 3A (still later): host GET, polling timer, tray/toast/sound, Checkmk ACK.

## Phase 3B — not started

Host monitoring. Do not start until host GET `columns` / `query` / item JSON are verified. Do not invent `POST /domain-types/host/collections/all`. Do not use `host_config`.

## Phase 3 (remaining, later)

- Polling (~60s), freeze on failure
- DPAPI credentials / settings UI for URL / site / user / interval / language
- Replace mock bootstrap in production builds; keep mock for tests

## Phase 4 — not started

- System tray + New/error icon
- Sound, Windows toast, mute
- Host-DOWN notification grouping/coalescing
- Tray-only mode

## Phase 5 — not started

- Logging with secret redaction
- Start with Windows (optional), single-instance
- `README.md` + `README.pl.md`, MIT `LICENSE`
- Packaging / `dotnet publish` release notes
- Open-source cleanup (no secrets, no Checkmk logos)
