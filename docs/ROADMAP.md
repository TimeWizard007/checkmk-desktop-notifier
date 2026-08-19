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
- `%LocalAppData%/CheckmkDesktopNotifier/state/<connection-id>/alert-state.json` (not secrets); Seen survives restart. A legacy root `alert-state.json` is read-fallback only.
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

Per-user Inno Setup 6 installer to `%LocalAppData%\Programs\CheckmkDesktopNotifier`, Start Menu shortcut, optional desktop shortcut, same HKCU Run autostart as the app, upgrade that preserves user data, optional uninstall data wipe, central versioning via `Directory.Build.props`, unsigned builds, per-user single-instance mutex. Manually validated on Windows 11.

## Phase 5 / V1 release — COMPLETE / V1 READY

User-facing 1.0.0 documentation, MIT license, sanitized screenshots, `SHA256SUMS.txt`, version **1.0.0**. Tag `v1.0.0` is part of close-out. GitHub Release is a separate follow-up.

## After V1 / v1.1.0 tagged / v1.2.0 FEATURE COMPLETE / RELEASE CANDIDATE

**v1.1.0** is tagged and was **not** published as a GitHub Release. Phase 6A COMPLETE / Windows-tested. Phase 6B COMPLETE / Windows-tested.

**v1.2.0 is RELEASED (Windows frozen).** Tag `v1.2.0` exists.

**Phase M0 COMPLETE / Windows-tested.** Shared platform seams for a future Avalonia macOS host. Windows v1.2.0 remains released and behavior-frozen.

**Phase M1 COMPLETE / real-macOS tested.** First Avalonia macOS host, Application Support paths, Keychain, connection settings, and shared poller smoke. Not a macOS product release.

Phase 6A delivered:

- Take / ACK **in Checkmk** (sticky, optional, host or service only)
- Shared CDN Taken-by vs generic ACK (Checkmk is source of truth)
- Single-line acknowledgement comments (`Taken by {name} via Checkmk Desktop Notifier cdn.v1 take name="..."`). Checkmk RAW 2.4 truncates multiline ACK comments; do not revert.
- Problem search + TAKEN filter/counter (presentation-only)
- Dark in-app Take confirmation
- ACK-aware notification suppression and host grouping
- No Untake, no `expire_on`, no custom backend

Phase 6B delivered:

- Open the corresponding Checkmk host/service GUI in the default browser
- Reversible local Seen / Unseen (returns to NEW; no notification replay)

**v1.2.0 / Phase 7A COMPLETE / Windows-tested:**

- Safe Release / Untake of **CDN Takes only** via live-validated `POST /domain-types/acknowledge/actions/delete/invoke`
- The notifier never removes generic/manual ACK
- Taken-by is clickable for CDN Takes; dark Release confirmation; Checkmk remains source of truth
- ACK metadata refreshes on every successful snapshot (Taken must clear when `acknowledged = 0`)
- Successful Take/Release uses row waiting states (`Taking...` / `Releasing...`); no native Windows MessageBox

**Phase M2 COMPLETE / real-macOS tested.** macOS menu-bar status item and problem panel, reusing shared filter/poller state. Intel left-click crash (`objc_msgSend`/`NSRect`) was hotfixed and retested. Not a macOS product release.

**Phase M3 COMPLETE / Intel macOS tested:** feature parity — Take/Release, complete Settings, native notifications from a `.app` bundle, sound, Start at Login via LaunchAgent `/usr/bin/open`, single instance. Shared Core/Infrastructure reused. Raw-executable `UNUserNotificationCenter` startup crash is treated as a packaging/init bug, not a C# try/catch. Broader beta still required (notifications, sleep/wake, VPN reconnect, Apple Silicon, signing/notarization, long-running use).

**Phase M4 COMPLETE / Intel macOS tested:** macOS UI/UX polish of the M3 surface. Light-mode polish across different Macs still needs broader beta coverage.

**v1.3.0 FEATURE COMPLETE / FEATURE FREEZE.** First unified Windows + macOS release. Windows v1.2.0 behavior preserved under product version 1.3.0. macOS menu-bar `.app` for Intel x64 (real-device validated) and Apple Silicon arm64 (packaged). Historical tags `v1.2.0` and `v1.3.0-beta.1` must not move.

**Future / optional (after user feedback; not a new phase now):**

- Signing/notarization
- Ticket workflow
- Zoho Desk integration

Evaluate Checkmk ACK + an existing ticket system **before** any custom shared database.

Possible later: App SDK toasts if packaging identity changes; more audio formats if justified.
