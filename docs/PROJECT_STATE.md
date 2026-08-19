# Project state

Durable checkpoint for future sessions. Do not treat chat history as source of truth; this file and the rest of `docs/` are.

## Current phase

**v1.3.0 FEATURE COMPLETE. CURRENT DEVELOPMENT CYCLE CLOSED. FEATURE FREEZE.**

First unified Windows + macOS product release. Version **1.3.0** is shared (`Directory.Build.props`, Windows installer, macOS Info.plist). Tags `v1.2.0` and `v1.3.0-beta.1` must not move.

Phase M0 COMPLETE / Windows-tested. Phases M1–M4 COMPLETE / Intel macOS tested.

Do not start M5 or another feature phase. Allowed later (not now): bug fixes, compatibility, signing/notarization, installer improvements, documentation, security.

Phase 1 COMPLETE. Phase 2 COMPLETE. Phase 3A COMPLETE. Phase 3B COMPLETE. Phase 3C COMPLETE. Phase 3D COMPLETE. Phase 4A COMPLETE. Phase 4B COMPLETE. Phase 4C COMPLETE. Phase 4D COMPLETE. Phase 5 COMPLETE / V1 READY (`v1.0.0` tagged). Phase 6A COMPLETE / Windows-tested. Phase 6B COMPLETE / Windows-tested. v1.1.0 tagged (no GitHub Release). Phase 7A COMPLETE / Windows-tested. **v1.2.0 tagged** (historical Windows). **v1.3.0-beta.1 tagged** (historical macOS tester).

Product version **1.3.0**. Historical v1.2.0 installer SHA-256 (`SHA256SUMS.txt`) must remain:

```
8B880CB7EE363A135DACECDEF8A90FF6AA806315EA33D5028D327F0D3B8362BB  CheckmkDesktopNotifier-Setup-x64.exe
```

v1.3.0 checksums belong in `SHA256SUMS-v1.3.0.txt` after the Windows EXE, macOS DMGs, and tagged source ZIP exist. Do not invent hashes.

## Git checkpoint

- Phase 3A completion: `7a108ff` — `Complete Phase 3A real Checkmk service integration`
- Phase 3B completion: `1ad02e3` — `Complete Phase 3B real Checkmk host integration`
- Phase 3C completion: `4604f01` — `Complete Phase 3C polling and persistence`
- Phase 2 completion: `2b85065` — `Mark Phase 2 as Windows-tested and complete`
- Phase 3D complete — `a255b7e` `Complete Phase 3D secure GUI configuration`
- Phase 4B complete — `1ce8616` `Complete Phase 4B notifications and sound controls`
- Phase 4C complete — `337c5a3` `Complete Phase 4C host grouping and autostart`
- Phase 4D complete — `0bfd177` `Complete Phase 4D Windows installer and packaging`
- Phase 5: COMPLETE / V1 READY
- Phase 6A: COMPLETE / Windows-tested (Take / shared ACK).
- Phase 6B: COMPLETE / Windows-tested (Open in Checkmk + Seen/Unseen). v1.1.0 tagged.
- Phase 7A: COMPLETE / Windows-tested (safe Release / Untake). v1.2.0 tagged.
- Phase M0: COMPLETE / Windows-tested (platform seams; Windows v1.2.0 behavior frozen).
- Phase M1: COMPLETE / Intel macOS tested (Avalonia host, Application Support, Keychain, connection/polling). Not a stable macOS product release.
- Phase M2: COMPLETE / Intel macOS tested (menu-bar status item, problem panel, filters/search, Open in Checkmk, local Seen/Unseen). Not a stable macOS product release.
- Phase M3: COMPLETE / Intel macOS tested (Take/Release, complete Settings, notifications, sound, Start at Login, single instance).
- Phase M4: COMPLETE / Intel macOS tested (problem panel / Settings / dialog polish, system appearance).
- macOS public tester: **v1.3.0-beta.1** (historical). Unified product: **v1.3.0**.

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

WPF mock UI, Windows 11 self-contained win-x64, no Administrator privileges. Compact bar, expandable list, local Seen, ACK badge independent of Seen.

## Phase 3A — complete (service REST)

Verified `POST /domain-types/service/collections/all` mapped into Core. Mock remains default.

Sanitized live result:

```
HTTP status: 200
Service problems: 129
WARN: 15
CRIT: 111
UNKNOWN: 3
```

Automation account: Normal monitoring user, Everything contact group, no Administrator privileges.

## Phase 3B — complete (host REST)

Implemented:

- `CheckmkRestClient` (`ICheckmkClient` in Real mode): service POST + host GET, merged `ProblemSnapshot`
- Host monitoring: `GET /domain-types/host/collections/all` with repeated `columns=` query-string parameters, no JSON body
- Unfiltered host GET returns `name` only; monitoring uses the `columns=` GET
- Map HARD DOWN → Critical, HARD UNREACHABLE → Unknown; identity `extensions.name`; recurrence `last_time_up`
- Host Kind vs service Kind remain separate; ACK/downtime stay read-only metadata
- `CheckmkDesktopNotifier.ConnectionTest --hosts` probe kept
- If either HTTP call fails, the snapshot is failed
- No host POST, no `host_config`, no host-DOWN grouping

### Windows 11 live validation (Phase 3B)

Environment: Windows 11, corporate VPN, dedicated Checkmk automation account, **no Administrator privileges**.

Unfiltered `GET /domain-types/host/collections/all`:

```
HTTP status: 200
Host objects: 263
Identity field: extensions.name
Fields present: name
```

Same GET with documented repeated `columns=` query-string parameters:

```
HTTP status: 200
Host objects: 263
UP: 262
DOWN: 1
UNREACHABLE: 0
Identity field: extensions.name
Monitoring fields present: name, state, state_type, plugin_output, last_state_change, last_hard_state_change, last_time_up, last_time_down, last_time_unreachable, acknowledged, scheduled_downtime_depth, num_services_hard_crit, num_services_hard_warn, num_services_hard_unknown
Monitoring fields missing: (none)
```

No host names, credentials, secrets, Authorization headers, or plugin outputs recorded.

## Phase 3C — complete (polling + persistence)

Implemented:

- `CheckmkPoller` + `CheckmkPollingHostedService` (desktop hosted service, not a Windows Service)
- Default interval 60s, minimum 10s (`PollIntervalSeconds`)
- Immediate first poll after startup; no overlapping polls (`SemaphoreSlim` single-flight)
- HTTP timeout shorter than the poll interval (`CheckmkOptions.CreateHttpTimeout()`)
- Failed snapshot does not recover/clear incidents; the next cycle continues
- Real mode: JSON alert state at `%LocalAppData%/CheckmkDesktopNotifier/alert-state.json`
- Diagnostics: `%LocalAppData%/CheckmkDesktopNotifier/last-poll.txt` (counts only; no secrets)
- Compact bar connection status: Connected / Refreshing / Connection error
- Mock mode: `DemoBootstrapper` + in-memory store; no REST polling
- Real mode: REST polling; no `DemoSnapshotFactory` injection

### Windows 11 live validation (Phase 3C)

Environment: Windows 11, corporate VPN, dedicated Checkmk automation account, **no Administrator privileges**.

Confirmed:

- Real mode starts successfully
- Immediate startup poll works
- Status transitions to Connected
- Real host + service problems are displayed in the WPF UI
- Background polling repeats approximately every 60 seconds
- `last-poll.txt` updates after successful polling
- Local Seen state survives application restart via JSON persistence
- When VPN / Checkmk connectivity is lost: status becomes Refreshing, then Connection error; existing problems remain visible; Seen is preserved; no false recoveries
- After connectivity is restored, polling resumes normally

No credentials, secrets, Authorization headers, host names, or plugin outputs recorded.

## Phase 3D — complete (GUI settings + Credential Manager)

Self-configuring Windows GUI. End users do not need `checkmk.local.json`, `CHECKMK_CONFIG`, or environment variables.

Implemented:

- First-run Settings when no usable production configuration exists (no failing poll loop)
- Settings UI: URL, site, username, automation secret (PasswordBox), poll interval; Test / Save / Cancel / Reset
- EN/PL resources
- HTTPS origin-only BaseUrl validation
- Automation secret in Windows Credential Manager (Generic Credential `CheckmkDesktopNotifier`, `CRED_PERSIST_LOCAL_MACHINE`, this Windows user, no Administrator)
- Non-secret settings in `%LocalAppData%/CheckmkDesktopNotifier/settings.json` (`baseUrl`, `site`, `username`, `pollIntervalSeconds` only)
- Incident state isolated per connection identity (normalized BaseUrl + Site) under `state/<hash>/alert-state.json`
- Legacy root `alert-state.json` is a **read fallback only** (not copied, moved, or auto-deleted)
- Live Apply without restart via `MonitoringCoordinator` (single poller session, no overlapping loops)
- Reset removes `settings.json` + Credential Manager secret; does not delete alert-state files
- Unconfigured / after Reset: compact bar shows **Setup required**; historical incidents may remain without implying **Connected**
- Mock remains developer-only (`checkmk.local.json` / `CHECKMK_MODE=Mock`); not in the Settings UI
- Compact-bar mouse handling walks Visual / Visual3D / ContentElement (`Run`) parents safely (`VisualTreeHelper.GetParent(Run)` must not be used)

### Configuration precedence (highest first)

1. `CHECKMK_CONFIG` file (explicit developer/CI override), then `CHECKMK_*` env overlays
2. GUI `settings.json` + Credential Manager (env vars ignored)
3. Discovered `checkmk.local.json` + `CHECKMK_*` env overlays
4. `CHECKMK_*` environment variables alone
5. Unconfigured → first-run Settings

The Settings window binds **only** to GUI `settings.json` + Credential Manager. Leftover developer files/env can still start Real monitoring while Settings fields look empty.

### Windows 11 live validation (Phase 3D)

Environment: Windows 11, dedicated Checkmk automation account, **no Administrator privileges**.

Configuration / credential flow:

- Application runs without Administrator privileges
- GUI Settings works; real Checkmk values can be entered entirely through the GUI
- Test connection succeeds; service and host monitoring endpoints are both reachable
- Save starts/applies real monitoring without manual JSON editing
- Restart via `.\CheckmkDesktopNotifier.exe` loads saved GUI configuration without `CHECKMK_CONFIG`

Secure storage:

- `settings.json` contains only `baseUrl`, `site`, `username`, `pollIntervalSeconds` — no automation secret, no Authorization header
- Automation secret stored as Windows Generic Credential named `CheckmkDesktopNotifier` (manually verified)
- Per-connection state: `%LocalAppData%\CheckmkDesktopNotifier\state\<connection-hash>\alert-state.json`
- Legacy root `alert-state.json` remains as non-destructive fallback

Manual tests A–L: **PASS**

- **A.** Settings works. Developer-config precedence was reviewed and documented. Truly unconfigured state uses **Setup required**. Historical incident data does not itself imply an active connection.
- **B.** Enter real Checkmk values.
- **C.** Test connection.
- **D.** Save and start monitoring.
- **E.** Restart without manual JSON/env setup.
- **F.** `settings.json` contains no secret.
- **G.** `alert-state` contains no secret.
- **H.** Poll interval 60 → 20 through Settings, applied without restart. Successful poll timestamps included `10:01:09.646` then `10:01:33.742` (~24s, consistent with a 20s interval plus request time). No overlapping loops.
- **I.** Incorrect secret: authentication/access error; no crash; secret not exposed; incident state intact.
- **J.** Restore correct secret: Test succeeded; monitoring reconnected; **Connected** returned; incident state intact.
- **K.** VPN / connectivity failure: **Refreshing** → **Connection error**; existing problems and Seen remained; no false recoveries; monitoring recovered after connectivity returned.
- **L.** Reset: removed GUI settings and the Credential Manager entry; stopped polling; **Setup required**; alert-state not deleted; no crash. Re-entering valid configuration restored monitoring.

Compact-bar `Run` crash regression: **PASS** after fix. Drag, label click, problem-list toggle, and settings gear work; no crash from `Run`/`TextBlock` routed mouse input.

Security review (confirmed): secret never in `settings.json`, `alert-state.json`, or `last-poll.txt`; Authorization headers never persisted; connection-test errors do not expose credentials; Credential Manager behind `ISecretStore`; Reset removes the stored credential; no hardcoded encryption key; no real secret committed to Git; `config/checkmk.local.json` remains gitignored.

No credentials, secrets, Authorization headers, host names, or plugin outputs recorded.

### Phase 4A — COMPLETE / Windows-tested (desktop shell / UX foundation)

Implemented and manually validated on Windows 11:

- Compact bar is shown immediately with **Initializing...** / **Uruchamianie...**; status is session-based (persisted last-poll time does not imply **Connected**)
- Gear opens a dark context menu: Connection settings, Help / About, Exit (does not drag or toggle the list)
- About dialog: product name, assembly informational version from `Directory.Build.props` (not hardcoded in UI), Author TimeWizard007, clickable GitHub URI
- Shared `IShellCommands` for gear and tray: ShowBar / ShowSettings / ShowAbout / Exit
- Single Settings and About windows (activate if already open)
- Graceful Exit: stop polling, close dialogs, dispose tray, `Application.Shutdown` (not `Environment.Exit`); `ShutdownMode=OnExplicitShutdown`
- Tray: `System.Windows.Forms.NotifyIcon` (no extra package); left-click Open; menu Open / Connection settings / Help About / Exit
- Original placeholder multi-size `Assets/app.ico` (monitor + heartbeat, no Checkmk logo); executable / windows / tray. Replaceable before V1; do not spend time redesigning it in code.
- **Windows 11 manual validation: PASSED** (Initializing, click/drag, gear, Settings, About, version `0.4.0`, GitHub, single-instance dialogs, tray Open/Settings/About, Exit, real monitoring, Seen, VPN). Visual leftover (system-boxed menus) carried into 4B.

### Phase 4B — COMPLETE / Windows-tested (notifications, mute, sound, filter polish)

Implemented and manually validated on Windows 11 (no Administrator privileges):

- Notify only `AlertDelta.Opened` (NEW incidents). Same uninterrupted incident, Seen, WARN→CRIT, failed polls, recoveries, ACK, and downtime do not emit extra notifications.
- Startup baseline: empty local state (`openCount == 0` and `LastSuccessfulPollUtc is null`) — first successful snapshot is ingested into the UI **without** toasts/sound. Later NEW incidents notify. Persisted state continues normal lifecycle (no replay on restart).
- Visual: unpackaged WinForms `NotifyIcon.ShowBalloonTip` (no Windows App SDK, no extra NuGet). Sound: bundled `Assets/notifier.wav` via `SoundPlayer` at per-app PCM volume (default 30%); optional imported custom WAV in LocalAppData `assets/custom-notification.wav`. WAV-only in V1. Deleting the original source file does not break playback.
- Mute: visual still shown; sound off; not pause/Seen/ACK. Gear/tray/Settings share `IUserPreferences`. Persisted in `preferences.json` with volume and Default/Custom (Reset configuration does not clear these).
- Settings: General / Connection / Notifications tabs. Notifications include Default notifier sound / Custom WAV / Volume / Test notification sound / Restore default sound / Mute. Test sound bypasses Mute and does not create incidents.
- Compact-bar counters toggle the problem list (same filter closes; a different filter switches in place with no close/reopen flash). Gear does not change the filter.
- Dark problem list, dark scrollbar, no empty strip after the gear, content-driven compact-bar width. Hide/restore tray and left-click tray toggle work.

**Windows 11 manual validation: PASSED** (counters, Notifications sound UI, volume 100/30/0, custom WAV import + source deletion, restart persistence, Restore default, Mute/Unmute, one NEW → one balloon + one sound, no poll/restart replay). Previously validated 4A/4B behavior also confirmed: baseline suppression, filters, Seen, Mark all new as seen, tray, VPN, Credential Manager, About/Exit, compact-bar sizing.

Host-DOWN grouping/coalescing and Start with Windows are Phase 4C (**COMPLETE / Windows-tested**).

### Phase 4C — COMPLETE / Windows-tested (host grouping + Start with Windows)

Implemented and manually validated on Windows 11 (no Administrator privileges):

**Host DOWN / UNREACHABLE grouping (notification-only):**

- Core `HostFailureNotificationGrouping` plans balloons from the same successful `ProblemSnapshot` + `AlertDelta`. No wall-clock wait.
- A host is grouping-active only when the snapshot contains that **host object** as HARD DOWN (`Severity.Critical`) or HARD UNREACHABLE (`Severity.Unknown`). Child services are never used to infer host failure. Same `SiteId` + `HostName` is required.
- NEW grouping host → one grouped balloon + one sound. Child NEW services on that host in the same snapshot are not notified separately.
- If the host is already DOWN and more child services become NEW later, those children stay NEW in the UI with **no** extra balloons/sounds and **no** repeated host balloon.
- ProblemListWindow / Core still show host + every service incident (severity, NEW/Seen, ACK, downtime, plugin output). Identities are not merged. Grouping does not call `MarkSeen`.
- Affected-service count = number of **service** problems in the merged snapshot with the same `SiteId` + `HostName`. REST `num_services_hard_*` is not mapped onto `MonitoredProblem` and is not used (the snapshot matches what the UI lists).
- DOWN vs UNREACHABLE: `HOST DOWN` / Critical vs `HOST UNREACHABLE` / Unknown.
- ACK / scheduled downtime on the host or on children do **not** suppress the grouped balloon (same as Phase 4B individual notify). They remain metadata, distinct from local Seen and from grouping suppression.
- Mute: grouped balloon yes, sound no. Failed snapshots emit nothing. Recurrence after recovery may notify again.

**Start with Windows (per-user):**

- Settings **General** tab checkbox. Source of truth is the OS entry, not `preferences.json`.
- Mechanism: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` value `CheckmkDesktopNotifier`. Quoted current executable path only. No secrets, no HKLM, no scheduled task, no elevation.
- Enable writes/updates the entry; disable deletes only this value. Opening Settings (and app startup) repairs a stale path if the entry already exists.
- Phase 4D installer must use this **same** HKCU Run value. Do not also create a Startup-folder shortcut for the same option.

**Windows 11 manual validation: PASSED.** Autostart: Settings → General → Start with Windows creates the per-user HKCU Run entry; restart preserves enabled; disable removes the entry; no UAC; Settings / Credential Manager unchanged. Grouping: one grouped host balloon and exactly one sound; child service incidents stayed visible/NEW; child service notifications were suppressed while grouping-active; no service storm; later polls while the host stayed failed did not repeat; after recovery, a later host failure produced a new grouped notification.

### Phase 4D — COMPLETE / Windows-tested (per-user installer)

Implemented and manually validated on Windows 11 (no Administrator privileges):

- **Technology:** Inno Setup 6 (`installer/CheckmkDesktopNotifier.iss`). Free for this use, mature, per-user `PrivilegesRequired=lowest`, simple upgrades via stable `AppId`. MSIX was rejected because packaged identity conflicts with the unpackaged WinForms balloon design.
- **Install:** `%LocalAppData%\Programs\CheckmkDesktopNotifier` (not Program Files). Output `artifacts/CheckmkDesktopNotifier-Setup-x64.exe` (gitignored). Portable `publish/win-x64/` remains the same exe.
- **Version:** `Directory.Build.props` (`1.0.0`) is the single source for assembly/file/product/About and `iscc /DMyAppVersion`.
- **Shortcuts:** per-user Start Menu always; optional desktop shortcut (default off).
- **Autostart:** same HKCU Run value `CheckmkDesktopNotifier` as the app. No Startup folder, scheduled task, or HKLM. Interactive wizard checkbox follows the real Run value; checked writes the installed quoted path, unchecked deletes only that value. Silent install repairs the path if the value already exists and does not delete it.
- **Upgrade:** replaces `{app}` binaries only. User data stays in `%LocalAppData%\CheckmkDesktopNotifier`. Credential Manager `CheckmkDesktopNotifier` is not touched on ordinary upgrade.
- **Running app:** compact bar cancels WM_CLOSE, so Setup uses `AppMutex` (`Local\TimeWizard007.CheckmkDesktopNotifier`) and asks the user to Exit from the tray. No silent kill.
- **Uninstall:** removes binaries, shortcuts, and the app Run value. User data is kept unless the user confirms optional removal (LocalAppData app folder + `cmdkey /delete:CheckmkDesktopNotifier`).
- **Unsigned:** no certificate in-tree. SmartScreen may warn. `SignTool` is left as a commented placeholder.
- **Single-instance:** per-user `Local\` mutex; a second launch shows the existing bar. Portable and installed share the mutex for this Windows user.

**Windows 11 manual validation: PASSED.** `CheckmkDesktopNotifier-Setup-x64.exe` builds and runs as a normal user with no UAC. Install path is `%LocalAppData%\Programs\CheckmkDesktopNotifier`. The installed app launches; the Start Menu shortcut works; existing GUI settings and Credential Manager remain usable. HKCU autostart points at the installed executable and matches Settings. Starting the installed exe while the notifier is already running reuses/activates the existing instance (no second poller, no duplicate notifications).

### Phase 5 — COMPLETE / V1 READY

User-facing 1.0.0 documentation, MIT license, sanitized screenshots, installer checksum, version **1.0.0**. No new product features.

- `README.md` / `README.pl.md` with relative language links and screenshots
- MIT `LICENSE` (copyright TimeWizard007)
- `docs/RELEASE_NOTES_1.0.0.md`
- `SHA256SUMS.txt` for `CheckmkDesktopNotifier-Setup-x64.exe`
- About on Windows 11: **1.0.0**; FileVersion **1.0.0.0**, ProductVersion **1.0.0**
- Screenshots under `docs/images/` (compact bar, problem list, connection, notifications, tray, About; General also stored)
- Git tag `v1.0.0` is created as part of this close-out. GitHub Release is a separate follow-up and is **not** created here.

Post-V1 (do not implement now): ticket workflow (create/open ticket, Zoho Desk or similar). Evaluate those integrations **before** any custom shared database.

### Phase 6A — COMPLETE / Windows-tested (Take / shared ACK)

v1.1.0 FEATURE COMPLETE / READY FOR RELEASE. Checkmk RAW 2.4.0p34 is the source of truth for Take/ACK. Do not implement Untake.

Validated on Windows 11 (and cross-client sync also on Windows 7 and a second PC). Checkmk RAW 2.4.0p34 is the source of truth.

- **Seen** stays local per Windows user (eye / Mark all). Never writes Checkmk.
- **Take** is optional (Settings → General, disabled by default for existing 1.0 installs). Creates a Checkmk **sticky** acknowledgement (`sticky=true`, `persistent=false`, `notify=false`). No `expire_on` (RAW 2.4.0p34 returned HTTP 400).
- Host Take ACKs that host only. Service Take ACKs that service only. No child auto-ACK. Rows stay visible; severity unchanged; local Seen unchanged. Downtime and Taken can coexist.
- Taken-by identity is parsed from the CDN ACK comment (`cdn.v1 take name="..."` preferred; flattened `Taken by {name} via Checkmk Desktop Notifier` also accepted). Never from the Checkmk author (live automation user, e.g. `checkmk-desktop-notifier`). Generic ACK (GUI/other tool, or first-line-only `"Taken by {name}"`) shows **ACK**.
- **Write format is single-line.** Checkmk RAW 2.4 stores/reads ACK comments as one line; `\n` is truncated (live GO-S11). Do not revert to a multiline comment. Example: `Taken by Michał via Checkmk Desktop Notifier cdn.v1 take name="Michał"`.
- Problem list search (host / service / Taken-by) composes with ALL / NEW / CRIT / WARN / UNK / **TAKEN**. TAKEN is CDN Takes only. Compact-bar TAKEN count is global notifier-Taken incidents. No local Taken store.
- Take confirmation is an application-owned dark modal (Take / Cancel). Enter confirms; Escape or close cancels. No optimistic Taken after confirm; the next successful poll is authoritative.
- Display name is a non-secret per-user preference in `preferences.json` (not Credential Manager, not `settings.json`).
- Read client stays read-only (`ICheckmkClient`). Writes go through `ICheckmkAcknowledgementClient` / `CheckmkTakeService`. Same automation credentials. Take needs `action.acknowledge`. Read-only / 403 accounts continue to monitor; Take is unavailable; no false Taken state.
- Failed Take / network failure does not invent Taken state.
- No Untake/Release in v1.1. ACK ends when Checkmk returns OK/UP. Manual removal remains in the Checkmk UI.
- No custom backend/database. No ticketing / Zoho. Mock/demo does not perform real writes.
- Notifications: an already-acknowledged Opened incident stays locally NEW but produces **no balloon and no sound**. Host grouping: an already ACK’d grouping host produces no grouped balloon/sound; child incidents stay listed.
- Concurrent Takes: no lock server; display the newest valid CDN Take by `entry_time`.
- Cross-machine: a second notifier instance sees the same Checkmk-backed Taken state after polling.

Manual checklist L (WARN → Take → CRIT stays Taken) and M (CRIT → OK → later CRIT is a new non-Taken incident) were **not reproduced live** (unsafe in the current environment). They remain covered by automated tests (`Warn_then_crit_keeps_taken_state`, `Recurrence_does_not_keep_stale_taken_by`). They are **not** failures.

### Phase 6B — COMPLETE / Windows-tested (Open in Checkmk + Seen/Unseen)

Final v1.1.0 UX convenience. Version **1.1.0**. Feature freeze. No Untake/Release.

Validated on Windows 11. Phase 6A Take behavior remains intact.

- **Open in Checkmk:** compact row icon. Builds an interactive GUI URL from configured BaseUrl + site + host/service (`/{site}/check_mk/index.py?start_url=view.py?...`). REST `urn:com.checkmk:rels/show` hrefs are API invoke endpoints and are **not** used. Opens the default Windows browser. No credentials/secrets in the URL. Never mutates Seen, severity, ACK, Take, or downtime. Missing/malformed target or browser launch failure fails safely.
- **Seen / Unseen:** the existing eye is a local toggle. NEW → Mark seen → Seen → Mark unseen → NEW. Same `IsSeen` field and alert-state JSON. No Checkmk write. Independent of ACK/Take/downtime. Mark unseen immediately returns the incident to the NEW counter/filter. It does **not** create `AlertDelta.Opened` and therefore does **not** replay balloon/sound.
- **Mark all new as seen** is unchanged. No bulk Unseen.
- Untake/Release remains **v1.2.0** and requires live validation of `POST /domain-types/acknowledge/actions/delete/invoke`. The notifier must never remove generic/manual ACK blindly.

**Windows 11 Phase 6B validation: PASSED.**

### Phase 7A — COMPLETE / Windows-tested (safe Release / Untake)

Version **1.2.0**. FEATURE COMPLETE / RELEASE CANDIDATE. Live Checkmk RAW 2.4.0p34 validated `POST /domain-types/acknowledge/actions/delete/invoke` (service payload HTTP **204**). Host uses the same endpoint with `acknowledge_type: host`. No comment id. No secrets in the payload. Windows-tested.

- Release is offered only for CDN Takes (`Taken by <name>`). Generic/manual ACK stays a non-clickable **ACK** badge and is never deleted.
- Any admin may Release any CDN Take (team coordination, not ACL). Display name identifies who took the problem; it does not restrict who may release it.
- Before delete: refresh when practical and proceed only if still `IsAcknowledgedInCheckmk && IsTakenByNotifier`. Abort and refresh if the problem is no longer a CDN Take.
- No optimistic UI. Flow: Taken by → dark confirm → Releasing... → delete REST → immediate refresh → show Take only after Checkmk `acknowledged == 0`. Successful Take/Release does **not** show a native MessageBox; waiting uses `Taking...` / `Releasing...` until read-back confirms. Errors use the dark in-app dialog.
- Failed refresh after a successful delete keeps the previous Taken state (or a waiting message). Do not invent local released state.
- ACK metadata is overwritten on every successful snapshot. Same incident identity must not keep `TakenByDisplayName` after Checkmk reports `acknowledged = 0` / `acknowledgement_type = 0`.
- Release does not change local Seen/Unseen or NEW counts.
- Release does not emit a notifier balloon or sound (`AlertDelta.Opened` stays empty). Checkmk itself may resume its own notifications; the confirm dialog says so.
- 400/422/no-longer-acknowledged are tolerated; never crash; never remove generic ACK.

**Windows 11 Phase 7A validation: PASSED.**

### Phase M0 — COMPLETE / Windows-tested (platform seams for a future macOS host)

Windows v1.2.0 behavior remains released and frozen. M0 added ports only.

**Windows smoke validation: PASSED** (start, polling, Credential Manager, Open in Checkmk, Take / Taken / Release, tray / Exit).

- `IUiThread` + WPF `WpfDispatcherUiThread` (ViewModels no longer use `Application.Current.Dispatcher`)
- `IUserDataDirectory` / `AppStoragePaths.For` (Windows still `%LocalAppData%\CheckmkDesktopNotifier`)
- `IUriLauncher` in Core; Windows `Process.Start` + `UseShellExecute` in `Platform.Windows`
- `WindowsCredentialSecretStore` and `WindowsHkcuRunAutostartStore` moved to `CheckmkDesktopNotifier.Platform.Windows`
- Single-instance remains Windows `Local\` mutex in App; macOS must plug in at its composition root later

No Avalonia UI, Keychain, UserNotifications, or login items were in M0.

### Phase M1 — COMPLETE / real-macOS tested (first macOS host and Checkmk connection)

Windows v1.2.0 remains released and frozen. M1 added `Platform.MacOS` and `App.MacOS` without changing the WPF host.

- Avalonia 11 + net8.0 host (`CheckmkDesktopNotifier.App.MacOS`) with a minimal connection window
- `~/Library/Application Support/CheckmkDesktopNotifier` via `MacUserDataDirectory`
- Keychain via `MacKeychainSecretStore` / `SecurityFrameworkKeychain` (no plaintext / no `InMemorySecretStore` in the macOS host)
- `/usr/bin/open` URI launcher; `AvaloniaUiThread`
- Shared `GuiConfigurationService`, `CheckmkConnectionTester`, `MonitoringCoordinator`, `CheckmkPoller`
- Diagnostic poller summary (counts + last poll). No problem-list UX, Take/Release UI, notifications, login items, or notarization

**Intel macOS validation: PASSED** (x86_64 self-contained host, Application Support path, login Keychain, settings.json has no secret, restart restores config, real Checkmk over VPN, shared poller + problem counts, Open Checkmk in default browser, no Windows Credential Manager / Registry at runtime).

### Phase M2 — COMPLETE / real-macOS tested (macOS menu-bar + problem list)

Not a macOS product release. Windows v1.2.0 remains frozen.

- Menu-bar `NSStatusItem` with compact counts (`N: C: W: U: T:`) and connection state
- Click toggles a compact problem panel (not a Windows-style floating bar). Native status-item callbacks marshal panel show/hide through Avalonia `Dispatcher.UIThread` (`PostDeferred`). Intel x86_64 does not query `NSRect` via `objc_msgSend` (that ABI crashes); panel falls back to a default position.
- Shared `ProblemListFilterLogic` for ALL/NEW/CRIT/WARN/UNK/TAKEN and host/service/Taken-by search
- Local Seen/Unseen via `IAlertStateService` (same semantics as Windows)
- Per-row Open in Checkmk via `CheckmkGuiUriBuilder` + `MacOpenUriLauncher`
- M1 connection window is Settings; shown on first run only
- Take/Release UI, UserNotifications, login items, signing: not in M2

**Intel macOS validation: PASSED** (x86_64 self-contained host). A–C PASS on first pass. D failed on first pass (left-click SIGSEGV from Intel `NSRect` `objc_msgSend`); hotfix retested and D PASS. E–S PASS (filters, search, Open in Checkmk, poll refresh, Settings, Quit). Extra: Mark seen / Seen-Unseen, hide/show panel repeatedly, Open Checkmk, Quit exits cleanly. Checklist P–Q (VPN disconnect/reconnect) were not separately re-run in M2; M1 already validated VPN Checkmk on this Mac.

See `docs/DEVELOPMENT.md` for the Phase M2 real-Mac checklist (A–S).

### Phase M3 — COMPLETE / Intel macOS tested (feature parity)

Not a stable macOS product release. Windows v1.2.0 remains frozen. Reuses shared `CheckmkTakeService`, `NotificationCoordinator`, `NotificationSoundMixer` / `NotificationSoundStore`, `AutostartService`, and `JsonUserPreferencesStore`.

- Take/Release UI on the problem panel using shared eligibility, confirmation, waiting visuals, and Checkmk ACK read-back. Generic ACK is not releasable.
- Complete Settings: General (Start at Login, Take enable + display name), Connection (URL/site/user/secret/poll interval, Test/Save/Cancel/Reset), Notifications (mute, volume, default/custom WAV, test, restore)
- Native macOS notification delivery (`NSUserNotificationCenter`) only after a real `.app` bundle identifier is present. `UNUserNotificationCenter.currentNotificationCenter` is never called from a raw executable — that API asserts and kills the process. Policy stays in Core/Infrastructure.
- macOS `NSSound` playback; no Windows `SoundPlayer`
- Start at Login via per-user LaunchAgent (`RunAtLoad`). SMAppService / signed `.app` is deferred until packaging.
- File-lock single instance (not Windows `Local\` mutex). Second start activates the existing instance.

**Intel macOS validation: PASSED** (bundled `.app`, x86_64). Startup without SIGSEGV, native menu-bar, live Checkmk counts, problem panel, filters/search, Seen/Unseen, Open in Checkmk, Take / Taken by / TAKEN / Release, Settings General / Connection / Notifications, Keychain, LaunchAgent Start at Login, single-instance, polling, Quit.

Broader beta still required: native notification delivery across real-world usage, notification permission prompts, sleep/wake, VPN disconnect/reconnect, Apple Silicon devices, signing/notarization, long-running stability.

### Phase M4 — COMPLETE / Intel macOS tested (UI/UX polish)

- Dark professional panel/Settings/dialogs with system appearance (`RequestedThemeVariant=Default`) and readable light-mode dictionaries
- Compact live menu-bar counts with shortening; redesigned problem cards, filter chips with counts, Take/Release confirmations
- Escape hides panel/Settings/dialogs; closing those windows does not quit

**Intel macOS validation: PASSED** (redesigned M4 UI usable). Light-mode polish across different Macs still needs broader beta coverage.

### macOS v1.3.0 (unified release)

First normal macOS distribution: `Checkmk Desktop Notifier.app` inside architecture-specific DMGs. Intel x64 real-device validated (M0–M4). Apple Silicon arm64 build/package validated. Unsigned / not notarized. LaunchAgent Start at Login. Secrets in Keychain. Do not move `v1.2.0` or `v1.3.0-beta.1`.

## Tests

Last automated run (Linux, v1.3.0 close-out source):

```
dotnet build CheckmkDesktopNotifier.sln   → 0 errors, 0 warnings
dotnet test  CheckmkDesktopNotifier.sln   → 546 passed, 0 failed
  Core.Tests:              246 passed
  Infrastructure.Tests:    228 passed
  Platform.MacOS.Tests:     34 passed
  App.MacOS.Tests:          38 passed
```

Phase 6A close-out was 382 passed. Phase 6B / v1.1.0 was 415 passed. v1.2.0 close-out was 457 passed. Phase M0 added seam tests (464). Phase M1 added macOS path/URI/Keychain-identifier/isolation tests (487). Phase M2 added menu-bar/filter/startup projection tests (501), then status-item crash-hotfix tests (512). Phase M3/M4 added Take/Settings/notification/sound/login/single-instance, UI, and `.app` bundle tests. v1.3.0-beta.1 packaging/docs tests brought the Linux suite to 545. v1.3.0 unified-version tests bring it to 546. Linux does not call native Keychain or AppKit.

## What is NOT implemented

- Authenticode signing / trusted SmartScreen reputation
- macOS signing / notarization
- Persistent window position on disk
- Shared/team Seen (local Seen remains per OS user)
- Ticketing / Zoho; custom shared backend/database
- `expire_on` ACK expiry (HTTP 400 on validated RAW 2.4.0p34)
- Windows Service (out of V1 by decision)
- SMAppService login items (LaunchAgent is used)
- Physical Apple Silicon device validation (arm64 is packaged)

## Immediate next steps

**v1.3.0 FEATURE COMPLETE / FEATURE FREEZE.** Finish remaining packaging on real machines (Windows Inno installer, macOS `hdiutil` DMGs, smoke tests), then tag `v1.3.0` and publish the GitHub Release. Do not start a new feature phase. Do not move `v1.2.0` or `v1.3.0-beta.1`. Do not revert CDN Take comments to a multiline format.
