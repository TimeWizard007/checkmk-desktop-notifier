# Roadmap

## Phase 1 — complete

Domain, incident engine, mock `ICheckmkClient`, persistence abstraction, unit tests. No WPF, no HTTP.

## Phase 2 — current

Mock WPF UI on Core:

- Compact Always-on-Top bar
- Expanded problem list
- Local Seen (eye, mark all)
- EN/PL resources
- Demo snapshot

Post-checkpoint `a42d0c1`: fix Windows Owner crash; add `docs/`.

Still Phase 2 polish (not Phase 3): confirm the win-x64 build stays running on Windows 11 after the Owner fix. Window position is in-memory only.

## Phase 3 — not started

Real Checkmk integration:

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
