# Roadmap

Canonical high-level product roadmap for Checkmk Desktop Notifier.

This file describes **what has shipped** and **what is planned**. It is not a task tracker. Concrete bugs and implementation tickets may later live in GitHub Issues. GitHub Projects or Milestones may be introduced when active development of a planned version begins. Do not turn this document into a giant checklist.

## Principles

1. Windows and macOS **v1.3.0** are the stable baselines.
2. New platform work must not regress existing platforms.
3. Shared Core / Infrastructure should remain platform-neutral.
4. OS-specific behavior belongs in `Platform.*` / `App.*` projects.
5. Linux should reach practical feature parity before adding unrelated major features.
6. Update **notification** stays separate from automatic updating. The app must not download or install itself.
7. Signing, notarization, and distribution infrastructure can evolve independently from application features.
8. Releases should continue to include reproducible artifacts and SHA-256 checksums.
9. Real-device validation must be recorded honestly. Do not claim validation that has not happened.
10. Future roadmap items are plans and may change based on tester/user feedback.

---

## v1.3.0 — Windows + macOS

**Status: RELEASED / COMPLETE**

This is the **current stable release**. Do not change its historical status. Feature development for that cycle is closed.

**Platforms**

- Windows 11 x64 — EXE installer; real-machine validated
- macOS Intel x64 — DMG; real-machine validated
- macOS Apple Silicon arm64 — DMG packaged; **physical-device validation still pending**

**What shipped**

- Native Windows desktop (compact Always-on-Top bar + tray) and macOS menu-bar application
- Live Checkmk HARD host/service polling
- NEW / CRIT / WARN / UNKNOWN / TAKEN filters, search, local Seen / Unseen
- Take / Taken by / Release with Checkmk acknowledgement integration
- Open in Checkmk, Settings, notifications, sound, autostart, single instance
- Windows Credential Manager; macOS Keychain
- Unsigned Windows installer; unsigned / not-notarized macOS DMGs

Historical tags `v1.2.0` and `v1.3.0-beta.1` must not move.

---

## v1.4.0 — Linux support

**Status: PLANNED** (not started)

**Goal:** add Linux as the third supported desktop platform while reusing the existing shared Core and Infrastructure architecture.

**Initial target distributions**

- Debian / Ubuntu — amd64 `.deb`
- Fedora / RHEL-compatible — x86_64 `.rpm`

Optional portable formats such as AppImage may be evaluated later. They are **not** required for v1.4.0.

Linux is **not** supported in v1.3.0. Do not claim that Linux currently works.

**Architecture**

Follow the existing platform split. Expected projects (names may be refined during design):

- `CheckmkDesktopNotifier.Platform.Linux`
- `CheckmkDesktopNotifier.App.Linux`

Reuse shared Core / Infrastructure wherever possible. Linux support must **not** regress Windows or macOS behavior.

**Planned feature parity**

- live Checkmk polling and HARD host/service problems
- NEW / CRIT / WARN / UNKNOWN / TAKEN, search, Seen / Unseen
- Take / Taken by / Release and Checkmk acknowledgement integration
- Open in Checkmk, Settings
- secure credential storage, notifications, sound, autostart, single instance

**Linux-native integrations to evaluate** (not frozen here)

- XDG user directories
- Secret Service / libsecret for credentials
- `xdg-open` for browser navigation
- freedesktop / D-Bus desktop notifications
- XDG autostart and/or systemd `--user`
- desktop tray/status integration compatible with common Debian/Ubuntu and Fedora desktop environments

Exact implementation details belong in the v1.4.0 design phase.

---

## v1.5.0 — Update availability notification

**Status: PLANNED** (not started)

**Goal:** a cross-platform mechanism that informs the user when a newer **stable** Checkmk Desktop Notifier release is available.

This is **not** automatic updating. The application must **not**:

- automatically download binaries
- automatically install updates
- silently replace itself

Automatic updates do **not** exist today and are not part of v1.5.0.

**Expected behavior**

- periodically check the official project release source
- compare the current application version with the latest stable release
- notify the user when a newer stable version exists
- show the available version
- provide an action such as “Open download page”
- allow dismiss / remind later
- avoid repeated notification storms

**Target platforms**

- Windows
- macOS Intel
- macOS Apple Silicon
- Debian / Ubuntu
- Fedora

Prefer a shared update-checking abstraction in Core / Infrastructure, with platform-specific presentation only where required. Beta / pre-release versions should not be offered to normal stable users unless a future explicit beta channel is implemented.

---

## Future / post-v1.5 possibilities

**Status: BACKLOG / NOT COMMITTED**

These are ideas, not release promises. Do not assign a version number unless the repository already has a clear decision for one.

- Windows code signing
- macOS signing and notarization
- Apple Silicon physical-device validation
- automated GitHub Actions release builds
- automated EXE / DMG / DEB / RPM packaging
- APT repository and/or RPM repository
- release manifest and improved update channels
- additional Linux architectures if justified by demand
- ticket workflow / Zoho Desk integration (evaluate Checkmk ACK + an existing ticket system before any custom shared database)
- modern Windows toasts if the packaging / identity model changes
- additional notification audio formats if there is a clear need

---

## Historical phases

Completed work that produced v1.0.0 through v1.3.0. Kept for context; not a plan for new work.

### Phase 1 — complete

Domain, incident engine, mock `ICheckmkClient`, persistence abstraction, unit tests. No WPF, no HTTP.

### Phase 2 — complete

Mock WPF UI on Core, manually validated on Windows 11 with a self-contained win-x64 publish (no Administrator privileges):

- Compact Always-on-Top bar stays running
- Expanded problem list (NEW first, then CRITICAL / WARNING / UNKNOWN)
- Local Seen (eye, mark all); ACK badge independent of Seen
- Host and service rows, plugin output, scrolling
- EN/PL resources
- Demo snapshot
- Owner-before-Show startup crash fixed and retested

Accepted leftovers (not Phase 3): in-memory window position, no in-app language switcher, no automated WPF tests.

### Phase 3A — complete

Real Checkmk **service** REST only (CRE/RAW 2.4.0p34), live-tested from Windows 11:

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

### Phase 3B — complete

Real Checkmk **host** REST (CRE/RAW 2.4.0p34), live-tested from Windows 11:

- `GET /domain-types/host/collections/all`
- Unfiltered GET: 263 hosts, `extensions.name` only
- `columns=` GET: 263 hosts, UP 262 / DOWN 1 / UNREACHABLE 0, all monitoring fields present
- Wired into `CheckmkRestClient` (`ICheckmkClient`): HARD DOWN → Critical, HARD UNREACHABLE → Unknown, `last_time_up`
- No host POST, no `host_config`, no notification grouping

### Phase 3C — complete

Background polling for the real Checkmk client, wired into Core and the WPF UI. JSON persistence of open/Seen/recurrence state for Real mode. Manually validated on Windows 11:

- Hosted poller (default 60s, minimum 10s), first poll immediately, no overlapping polls
- Failed poll freezes lifecycle (no false RECOVERED); existing problems and Seen remain
- Connection status: Refreshing → Connected; on loss, Refreshing → Connection error
- `%LocalAppData%/CheckmkDesktopNotifier/state/<connection-id>/alert-state.json` (not secrets); Seen survives restart. A legacy root `alert-state.json` is read-fallback only.
- `last-poll.txt` updates after successful polls
- Mock keeps `DemoBootstrapper`; Real uses REST polling only

### Phase 3D — complete

GUI first-run / Settings, Windows Credential Manager for the automation secret, per-user `settings.json`. Manually validated on Windows 11 (no Administrator privileges): Test connection, Save, restart without `CHECKMK_CONFIG`, Credential Manager storage, isolated alert-state, poll-interval change, wrong/restored secret, VPN loss, Reset, and compact-bar `Run` mouse-input crash fix.

### Phase 3 (remaining leftovers)

- Optional in-app language switcher / DPAPI is not used (Credential Manager is the secret store)
- Keep mock for UI development and tests

### Phase 4A — COMPLETE / Windows-tested

Desktop shell / UX foundation: Initializing status, gear menu, About, graceful Exit, system tray, application icon. Manually validated on Windows 11. Remaining boxed menu visuals were carried into Phase 4B (no separate polish phase).

Implemented and Windows-tested:

- Startup Initializing / Loading state; block unsafe actions until ready
- Settings gear menu: Connection settings, Help / About, Exit (Mute added in 4B)
- Help / About with assembly version and GitHub link
- Graceful Exit (shared with tray)
- System tray icon + menu (Open / Connection settings, Help About / Exit; Mute added in 4B)
- Application / window / executable / tray icon (`Assets/app.ico`, replaceable original placeholder)
- Shared shell commands between gear and tray

### Phase 4B — COMPLETE / Windows-tested

Windows balloon notifications, alert sound (bundled WAV + optional imported custom WAV, per-app volume), mute, Seen-aware / de-duplicated notify-on-Opened, dark gear/tray menu polish, hide-to-tray, dark problem-list chrome, presentation-only problem-list filter with counter toggle, Settings Connection/Notifications tabs. Manually validated on Windows 11.

### Phase 4C — COMPLETE / Windows-tested

- Host DOWN / UNREACHABLE notification grouping/coalescing from the merged snapshot (no child-service balloon storm; full host/service visibility in the problem list)
- Per-user Start with Windows (`HKCU\Software\Microsoft\Windows\CurrentVersion\Run` value `CheckmkDesktopNotifier`, shared with the Phase 4D installer)
- Settings General tab

Manually validated on Windows 11. Phase 4D packaging is COMPLETE / Windows-tested.

### Phase 4D — COMPLETE / Windows-tested

Per-user Inno Setup 6 installer to `%LocalAppData%\Programs\CheckmkDesktopNotifier`, Start Menu shortcut, optional desktop shortcut, same HKCU Run autostart as the app, upgrade that preserves user data, optional uninstall data wipe, central versioning via `Directory.Build.props`, unsigned builds, per-user single-instance mutex. Manually validated on Windows 11.

### Phase 5 / V1 release — COMPLETE / V1 READY

User-facing 1.0.0 documentation, MIT license, sanitized screenshots, `SHA256SUMS.txt`, version **1.0.0**. Tag `v1.0.0` is part of close-out.

### v1.1.0 / v1.2.0 (Windows)

**v1.1.0** is tagged and was **not** published as a GitHub Release. Phase 6A COMPLETE / Windows-tested. Phase 6B COMPLETE / Windows-tested.

**v1.2.0 is RELEASED (Windows frozen).** Tag `v1.2.0` exists.

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

### macOS host (Phases M0–M4)

**Phase M0 COMPLETE / Windows-tested.** Shared platform seams for a future Avalonia macOS host. Windows v1.2.0 remains released and behavior-frozen.

**Phase M1 COMPLETE / real-macOS tested.** First Avalonia macOS host, Application Support paths, Keychain, connection settings, and shared poller smoke. Not a macOS product release.

**Phase M2 COMPLETE / real-macOS tested.** macOS menu-bar status item and problem panel, reusing shared filter/poller state. Intel left-click crash (`objc_msgSend`/`NSRect`) was hotfixed and retested. Not a macOS product release.

**Phase M3 COMPLETE / Intel macOS tested:** feature parity — Take/Release, complete Settings, native notifications from a `.app` bundle, sound, Start at Login via LaunchAgent `/usr/bin/open`, single instance. Shared Core/Infrastructure reused. Raw-executable `UNUserNotificationCenter` startup crash is treated as a packaging/init bug, not a C# try/catch.

**Phase M4 COMPLETE / Intel macOS tested:** macOS UI/UX polish of the M3 surface. Light-mode polish across different Macs still needs broader real-world coverage.

Unified Windows + macOS product release is **v1.3.0** (see above).
