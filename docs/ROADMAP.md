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

## Phase 4C — COMPLETE / Windows-tested

- Host DOWN / UNREACHABLE notification grouping/coalescing from the merged snapshot (no child-service balloon storm; full host/service visibility in the problem list)
- Per-user Start with Windows (`HKCU\Software\Microsoft\Windows\CurrentVersion\Run` value `CheckmkDesktopNotifier`, shared with the Phase 4D installer)
- Settings General tab

Manually validated on Windows 11. Phase 4D packaging is COMPLETE / Windows-tested.

## Phase 4D — COMPLETE / Windows-tested

Per-user Inno Setup 6 installer to `%LocalAppData%\Programs\CheckmkDesktopNotifier`, Start Menu shortcut, optional desktop shortcut, same HKCU Run autostart as the app, upgrade that preserves user data, optional uninstall data wipe, central `0.4.1` versioning, unsigned builds, per-user single-instance mutex. Manually validated on Windows 11. Do not start Phase 5.

## Phase 5 / V1 release — not started

Keep this visible; do not start until asked:

- Version `1.0.0`
- `README.md` (English)
- `README.pl.md` (Polish)
- Language links between READMEs
- Project overview
- Screenshots
- Feature list
- Windows requirements
- Checkmk requirements
- Installation instructions
- Portable usage instructions
- First-run configuration
- Checkmk automation-user setup
- Credential Manager / security explanation
- NEW vs Seen explanation
- Checkmk ACK / downtime explanation
- HOST DOWN / UNREACHABLE grouping explanation
- Notification / custom WAV / volume / mute documentation
- Tray behavior
- Start with Windows
- Installer upgrade/uninstall behavior
- Build-from-source documentation
- Installer build documentation
- Unsigned installer / SmartScreen note
- License review
- Final icon review
- Final regression checklist
- Final installer build
- Git tag `v1.0.0`
- GitHub Release
- MIT / open-source release hygiene
- No Checkmk logos/trademarks bundled without permission
- Logging with secret redaction
