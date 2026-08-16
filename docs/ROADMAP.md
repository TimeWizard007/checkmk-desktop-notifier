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

Accepted leftovers (not Phase 3): in-memory window position, no in-app language switcher, no automated WPF tests.

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

Out of Phase 3A: polling timer, tray/toast/sound, Checkmk ACK.

## Phase 3B — complete

Real Checkmk **host** REST (CRE/RAW 2.4.0p34), live-tested from Windows 11 over VPN:

- `GET /domain-types/host/collections/all`
- Unfiltered GET: 263 hosts, `extensions.name` only
- `columns=` GET: 263 hosts, UP 262 / DOWN 1 / UNREACHABLE 0, all monitoring fields present
- Wired into `CheckmkRestClient` (`ICheckmkClient`): HARD DOWN → Critical, HARD UNREACHABLE → Unknown, `last_time_up`
- No host POST, no `host_config`, no notification grouping

## Phase 3C — complete

Background polling for the real Checkmk client, wired into Core and the WPF UI. JSON persistence of open/Seen/recurrence state for Real mode. Manually validated on Windows 11 over VPN:

- Hosted poller (default 60s, minimum 10s), first poll immediately, no overlapping polls
- Failed poll freezes lifecycle (no false RECOVERED); existing problems and Seen remain
- Connection status: Refreshing → Connected; on loss, Refreshing → Connection error
- `%LocalAppData%/CheckmkDesktopNotifier/alert-state.json` (not secrets); Seen survives restart
- `last-poll.txt` updates after successful polls
- Mock keeps `DemoBootstrapper`; Real uses REST polling only

Do not start Phase 4 until explicitly requested.

## Phase 3D — complete

GUI first-run / Settings, Windows Credential Manager for the automation secret, per-user `settings.json`. Manually validated on Windows 11 (no Administrator privileges): Test connection, Save, restart without `CHECKMK_CONFIG`, Credential Manager storage, isolated alert-state, poll-interval change, wrong/restored secret, VPN loss, Reset, and compact-bar `Run` mouse-input crash fix. Do not start Phase 4 yet.

## Phase 3 (remaining, later)

- Optional in-app language switcher / DPAPI is not used (Credential Manager is the secret store)
- Keep mock for UI development and tests

## Phase 4 — not started (backlog only)

Do not implement these until explicitly requested:

1. Startup Initializing / Loading state
2. Prevent unsafe/awkward interaction before initialization is ready
3. Settings gear menu with exactly: Connection settings, Help / About, Exit
4. Help / About: Checkmk Desktop Notifier; application version from assembly/build metadata (not hardcoded in UI); Author: TimeWizard007; clickable GitHub repository link `https://github.com/TimeWizard007/checkmk-desktop-notifier`
5. Proper graceful Exit action
6. System tray icon
7. Application / window / executable icon
8. Tray menu
9. Windows toast / popup notifications
10. Alert sound
11. Mute
12. Local Seen-aware notification behavior
13. Host DOWN / UNREACHABLE notification grouping/coalescing so one failed host does not create a notification storm from all child services
14. Reuse the same commands/logic for Exit/Settings between compact-bar menu and tray where appropriate

## Phase 5 / V1 release — not started

Keep this visible; do not start until Phase 4 is done unless a later decision says otherwise:

- `README.md` (English) and `README.pl.md` (Polish), with language links between the files
- Screenshots
- Installation/setup instructions
- Explanation of NEW / Seen / Checkmk ACK / downtime
- Build-from-source documentation
- Review/update of `docs/`
- Clean self-contained Windows package
- Final Windows regression tests
- GitHub tag/release
- MIT / open-source release hygiene
- No Checkmk logos/trademarks bundled without permission
- Logging with secret redaction
- Start with Windows (optional), single-instance
