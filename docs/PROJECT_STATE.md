# Project state

Durable checkpoint for future sessions. Do not treat chat history as source of truth; this file and the rest of `docs/` are.

## Current phase

**Phase 3D (GUI settings, first-run, Credential Manager) — COMPLETE.**

Phase 3D was manually validated on Windows 11. Do **not** start Phase 4 yet.

## Git checkpoint

- Phase 3A completion: `7a108ff` — `Complete Phase 3A real Checkmk service integration`
- Phase 3B completion: `1ad02e3` — `Complete Phase 3B real Checkmk host integration`
- Phase 3C completion: `4604f01` — `Complete Phase 3C polling and persistence`
- Phase 2 completion: `2b85065` — `Mark Phase 2 as Windows-tested and complete`
- This record: Phase 3D complete — `Complete Phase 3D secure GUI configuration`

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

### Phase 4 backlog (do not implement now)

1. Startup Initializing / Loading state
2. Prevent unsafe/awkward interaction before initialization is ready
3. Settings gear menu: Connection settings, Help / About, Exit
4. Help / About: product name, version from assembly/build metadata (not hardcoded), Author: TimeWizard007, clickable GitHub link `https://github.com/TimeWizard007/checkmk-desktop-notifier`
5. Proper graceful Exit action
6. System tray icon
7. Application / window / executable icon
8. Tray menu
9. Windows toast / popup notifications
10. Alert sound
11. Mute
12. Local Seen-aware notification behavior
13. Host DOWN / UNREACHABLE notification grouping/coalescing (one failed host must not storm child-service notifications)
14. Reuse the same commands/logic for Exit/Settings between compact-bar menu and tray where appropriate

### Phase 5 / V1 release (keep visible; do not start now)

- `README.md` (English) and `README.pl.md` (Polish), with language links between them
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
- Do not create README until Phase 5

## Tests

Last automated run (Linux agent, Phase 3D completion):

```
dotnet build CheckmkDesktopNotifier.sln   → 0 errors, 0 warnings
dotnet test  CheckmkDesktopNotifier.sln   → 154 passed, 0 failed
  Core.Tests:            36 passed
  Infrastructure.Tests:  118 passed
```

Re-run after any further change. Record the new numbers here if they change.

## What is NOT implemented

- Phase 4: Initializing/Loading, gear menu (Settings / Help About / Exit), icons, tray, toast, sound, mute, Seen-aware notifications, host-DOWN grouping
- Windows startup / single-instance
- Persistent window position on disk
- Phase 5: MIT `LICENSE`, `README.md`, `README.pl.md`, screenshots, install docs, packaging/release
- Windows Service (out of V1 by decision)

## Immediate next steps

Do not start Phase 4 until explicitly requested. Do not invent a host POST. Do not use `host_config`. Do not auto-delete the legacy root `alert-state.json`.
