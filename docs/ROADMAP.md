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

## Phase 3 — not started

Real Checkmk integration (do not start until explicitly approved):

- Confirm remaining host GET facts (`docs/CHECKMK_API.md` UNVERIFIED)
- Read-only REST adapter mapping `value` → Core DTOs
- Automation-user credentials in DPAPI (not in git)
- Polling (~60s), freeze on failure
- Settings for URL / site / user / interval / language
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
