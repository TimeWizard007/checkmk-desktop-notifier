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

Do not start Phase 4C (host-DOWN grouping / autostart) until asked. Phase 4B is complete.

## Phase 3D — complete

GUI first-run / Settings, Windows Credential Manager for the automation secret, per-user `settings.json`. Manually validated on Windows 11 (no Administrator privileges): Test connection, Save, restart without `CHECKMK_CONFIG`, Credential Manager storage, isolated alert-state, poll-interval change, wrong/restored secret, VPN loss, Reset, and compact-bar `Run` mouse-input crash fix.

## Phase 4A — COMPLETE / Windows-tested

Desktop shell / UX foundation: Initializing status, gear menu, About, graceful Exit, system tray, application icon. Manually validated on Windows 11. Remaining boxed menu visuals were carried into Phase 4B (no separate polish phase).

## Phase 3 (remaining, later)

- Optional in-app language switcher / DPAPI is not used (Credential Manager is the secret store)
- Keep mock for UI development and tests

## Phase 4A — desktop shell (complete)

Implemented and Windows-tested:

- Startup Initializing / Loading state; block unsafe actions until ready
- Settings gear menu: Connection settings, Help / About, Exit (Mute added in 4B)
- Help / About with assembly version and GitHub link
- Graceful Exit (shared with tray)
- System tray icon + menu (Open / Connection settings / Help About / Exit; Mute added in 4B)
- Application / window / executable / tray icon (`Assets/app.ico`, replaceable original placeholder)
- Shared shell commands between gear and tray

## Phase 4B — COMPLETE / Windows-tested

Windows balloon notifications, alert sound (bundled WAV + optional imported custom WAV, per-app volume), mute, Seen-aware / de-duplicated notify-on-Opened, dark gear/tray menu polish, hide-to-tray, dark problem-list chrome, presentation-only problem-list filter with counter toggle, Settings Connection/Notifications tabs. Manually validated on Windows 11.

## Phase 4C — not started

- Host DOWN / UNREACHABLE notification grouping/coalescing
- Avoid notification storms from child services caused by the failed host
- Preserve full host/service visibility in the problem list
- Start with Windows / per-user autostart
- Shared autostart state for application Settings and a future installer

## Phase 4D — not started

- Per-user installer/package
- Install without Administrator privileges where practical
- Upgrade behavior
- Start Menu shortcut
- Optional desktop shortcut
- Installer option for Start with Windows
- Preserving Settings / Credential Manager / Seen state on upgrade
- Uninstall behavior

## Phase 5 / V1 release — not started

Keep this visible; do not start until Phase 4 is done unless a later decision says otherwise:

- `README.md` (English) and `README.pl.md` (Polish), with language links between the files
- Screenshots
- Installation/setup instructions
- Explanation of NEW / Seen / Checkmk ACK / downtime
- Build-from-source documentation
- Review/update of `docs/`
- Final icon review
- Clean self-contained Windows package
- Final Windows regression tests
- Version / GitHub tag / GitHub Release
- MIT / open-source release hygiene
- No Checkmk logos/trademarks bundled without permission
- Logging with secret redaction
- Single-instance
