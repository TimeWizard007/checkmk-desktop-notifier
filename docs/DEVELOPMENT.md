# Development

## Prerequisites

- .NET 8 SDK
- For **running** the WPF app: Windows 10/11
- Linux can **build** Core, Infrastructure, tests, ConnectionTest, and (with Windows targeting) the WPF project
- No Administrator privileges required for build, test, or running the per-user app

## Repository structure

```
checkmk-desktop-notifier/
  README.md / README.pl.md
  LICENSE
  Directory.Build.props          ← Version 1.1.0
  CheckmkDesktopNotifier.sln
  docs/                          ← durable project memory (read this first)
  installer/CheckmkDesktopNotifier.iss
  scripts/
  config/checkmk.local.json.example
  src/CheckmkDesktopNotifier.Core/
  src/CheckmkDesktopNotifier.Infrastructure/
  src/CheckmkDesktopNotifier.App/
  src/CheckmkDesktopNotifier.ConnectionTest/
  tests/CheckmkDesktopNotifier.Core.Tests/
  tests/CheckmkDesktopNotifier.Infrastructure.Tests/
```

## Linux — build and test

From the repository root:

```bash
dotnet build CheckmkDesktopNotifier.sln
dotnet test CheckmkDesktopNotifier.sln
```

The App project sets `EnableWindowsTargeting` so WPF can compile on non-Windows agents. That does **not** make the UI runnable on Linux.

## Windows — run the mock UI

Default `Mode` is **Mock**. No Checkmk server required:

```powershell
dotnet run --project src/CheckmkDesktopNotifier.App/CheckmkDesktopNotifier.App.csproj
```

Or set `CheckmkDesktopNotifier.App` as the startup project in Visual Studio.

UI language follows Windows UI culture (`en` default, `pl` when UI culture is Polish).

### Manual UI checklist

1. Compact Always-on-Top bar stays open (does not exit immediately).
2. Bar shows NEW / CRIT / WARN / UNKNOWN counts and last check time.
3. NEW is emphasized when count > 0 (no flashing).
4. Bar is draggable; click (not drag) expands the problem list.
5. NEW section first; eye only on NEW; Mark all new as seen works locally.
6. Seen rows remain under real severity. ACK and downtime are badges, not Seen.
7. Closing the list collapses it. Closing the compact bar **hides to tray** (does not exit). Exit is gear/tray **Exit** only.

## Local Checkmk configuration (developer override)

End users of the **installed** app use Settings → Connection. They do not need this file. The following is for developers and CI only.

Never commit `config/checkmk.local.json`. Copy the example:

```bash
cp config/checkmk.local.json.example config/checkmk.local.json
```

Edit the `Checkmk` object:

| Field | Notes |
|-------|--------|
| `Mode` | `Mock` (default) or `Real` |
| `BaseUrl` | Origin only, e.g. `https://checkmk.example.invalid` — no site path, no credentials |
| `Site` | Site name, e.g. `mysite` |
| `Username` | Automation user |
| `Secret` | Automation secret (never commit) |
| `PollIntervalSeconds` | Default `60`, minimum `10`. Used by the Real-mode background poller. |

Environment variables override the file: `CHECKMK_MODE`, `CHECKMK_BASE_URL`, `CHECKMK_SITE`, `CHECKMK_USERNAME`, `CHECKMK_SECRET`, `CHECKMK_POLL_INTERVAL_SECONDS`. Optional path: `CHECKMK_CONFIG`.

The loader also searches `%LocalAppData%/CheckmkDesktopNotifier/checkmk.local.json` and walks up from the current directory looking for `config/checkmk.local.json`.

API URI built from config: `{BaseUrl}/{Site}/check_mk/api/1.0/`.

## One-shot connection test (read-only)

Performs **one** `POST /domain-types/service/collections/all` and prints only HTTP status and problem counts (no secret, no Authorization header, no raw JSON, no plugin output).

From the repository root, with `config/checkmk.local.json` set to `Mode: Real`:

```bash
dotnet run --project src/CheckmkDesktopNotifier.ConnectionTest/CheckmkDesktopNotifier.ConnectionTest.csproj
```

Expected output shape:

```
HTTP status: 200
Service problems: N
WARN: N
CRIT: N
UNKNOWN: N
```

### Windows 11 live validation (Phase 3A)

Confirmed over the corporate VPN with a dedicated automation account (Normal monitoring user, Everything contact group, no Administrator privileges). Sanitized result:

```
HTTP status: 200
Service problems: 129
WARN: 15
CRIT: 111
UNKNOWN: 3
```

Do not log or commit the automation secret, Authorization header, or plugin outputs.

## Host connection test (Phase 3B, complete)

Performs a verified `GET /domain-types/host/collections/all` (no query string), then the live-confirmed `columns=` GET. Prints HTTP status, host object counts, UP/DOWN/UNREACH counts when `state` is present, and monitoring field names. Does not print secrets, Authorization, raw JSON, host names, or plugin output values.

From the repository root, with `config/checkmk.local.json` set to `Mode: Real`:

```powershell
dotnet run --project src/CheckmkDesktopNotifier.ConnectionTest/CheckmkDesktopNotifier.ConnectionTest.csproj -- --hosts
```

Windows self-contained publish of the connection test (no admin):

```powershell
dotnet publish src/CheckmkDesktopNotifier.ConnectionTest/CheckmkDesktopNotifier.ConnectionTest.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -o publish/win-x64-connectiontest
```

Run on Windows 11 over VPN:

```powershell
.\publish\win-x64-connectiontest\CheckmkDesktopNotifier.ConnectionTest.exe --hosts
```

### Windows 11 live validation (Phase 3B)

Unfiltered GET:

```
HTTP status: 200
Host objects: 263
Identity field: extensions.name
Fields present: name
```

GET with repeated `columns=` query-string parameters:

```
HTTP status: 200
Host objects: 263
UP: 262
DOWN: 1
UNREACHABLE: 0
```

Real mode in the WPF app uses that `columns=` GET (HARD DOWN/UNREACHABLE only) together with the service POST. Background polling is Phase 3C (complete).

## Phase 3C — background polling (complete)

Polling runs only while the desktop app is running (`BackgroundService`, not a Windows Service).

| Mode | Startup | Persistence | Polling |
|------|---------|-------------|---------|
| Mock | `DemoBootstrapper` + `DemoSnapshotFactory` | In-memory | Hosted service is a no-op (no REST) |
| Real | No demo snapshot | `%LocalAppData%/CheckmkDesktopNotifier/state/<connection-hash>/alert-state.json` (legacy root file is read-fallback only) | Immediate first poll, then every `PollIntervalSeconds` |

Diagnostics file (no secrets, no Authorization, no host names, no plugin output):

`%LocalAppData%\CheckmkDesktopNotifier\last-poll.txt`

Example success:

```
TimeUtc: ...
Success: true
Problems: N
Hosts: N
Services: N
```

### Windows 11 live validation (Phase 3C)

Confirmed on Windows 11 over the corporate VPN with a dedicated automation account (no Administrator privileges):

- Real mode starts; immediate startup poll; status becomes **Connected**
- Real host + service problems are displayed in the WPF UI
- Background polling repeats approximately every 60 seconds
- `last-poll.txt` updates after successful polling
- Local Seen survives application restart via `%LocalAppData%/CheckmkDesktopNotifier/state/<connection-hash>/alert-state.json`
- Connectivity loss: **Refreshing** then **Connection error**; existing problems remain; Seen preserved; no false recoveries
- After connectivity is restored, polling resumes normally

Do not log or commit the automation secret, Authorization header, host names, or plugin outputs.

## Phase 3D — Settings / first-run (complete)

Normal published Windows users configure Checkmk in the GUI. They do not need `checkmk.local.json`, `CHECKMK_CONFIG`, or environment variables.

| Item | Location |
|------|----------|
| Non-secret settings | `%LocalAppData%\CheckmkDesktopNotifier\settings.json` (`baseUrl`, `site`, `username`, `pollIntervalSeconds` only) |
| Automation secret | Windows Credential Manager Generic Credential `CheckmkDesktopNotifier` (this Windows user; no Administrator) |
| Incidents / Seen | `%LocalAppData%\CheckmkDesktopNotifier\state\<identity>\alert-state.json` |
| Diagnostics | `%LocalAppData%\CheckmkDesktopNotifier\last-poll.txt` |

Precedence (highest first): `CHECKMK_CONFIG` → GUI settings + Credential Manager → discovered `checkmk.local.json` + env → env only → first-run Settings.

The Settings window binds only to GUI settings + Credential Manager. Leftover developer files/env can start Real monitoring while Settings fields look empty. Truly unconfigured state shows **Setup required**; historical incident files do not themselves imply **Connected**.

Mock/Demo remains a developer path (`Mode=Mock` in a discovered file or `CHECKMK_MODE=Mock`). It is not offered in Settings.

### Windows 11 live validation (Phase 3D)

Confirmed on Windows 11 with a dedicated automation account (**no Administrator privileges**). Tests A–L passed. Compact-bar `Run` mouse-input crash was found, fixed, and retested.

- GUI Settings, Test connection (services + hosts reachable), Save, restart without `CHECKMK_CONFIG`
- `settings.json` has no secret; Credential Manager entry `CheckmkDesktopNotifier` verified
- Isolated `state\<hash>\alert-state.json`; legacy root file is read-fallback only
- Poll interval 60 → 20 applied live; subsequent successful polls ~24s apart (20s interval plus request time); no overlapping loops
- Wrong secret: auth/access error, no crash, secret not exposed, incident state intact; restoring the secret reconnects
- VPN loss: **Refreshing** → **Connection error**; problems and Seen remain; no false recoveries; recovers when connectivity returns
- Reset removes GUI settings and the Credential Manager entry, stops polling, returns **Setup required**, does not delete alert-state; re-entering config restores monitoring
- Compact bar: drag, label click, problem-list toggle, settings gear; no crash from `Run`/`TextBlock` routed input

Do not log or commit the automation secret, Authorization header, host names, or plugin outputs.

### Legacy `alert-state.json`

Phase 3C wrote `%LocalAppData%\CheckmkDesktopNotifier\alert-state.json`. Phase 3D reads that file **only** when the isolated `state\<hash>\alert-state.json` does not exist yet. It is not copied or auto-deleted. After the isolated file exists, the root file is unused and may be removed **manually** later. Do not delete it as part of Reset or startup.

## Phase 4A — desktop shell (COMPLETE / Windows-tested)

Gear menu and tray share `IShellCommands` (`ShowBar`, `HideToTray`, `ToggleBar`, `ShowSettings`, `ShowAbout`, `Exit`). Exit stops polling, closes Settings/About, hides the problem list, disposes the tray icon, then `Application.Shutdown()`. `ShutdownMode` is `OnExplicitShutdown`. Closing Settings, About, or the problem list does not exit the app. **Hide to tray** hides the existing compact bar and problem list; monitoring, polling, and notifications continue.

Tray uses built-in `System.Windows.Forms.NotifyIcon` (no extra NuGet). Left-click toggles bar visibility. Tray **Open** always restores the existing compact bar.

Version in About is `AssemblyInformationalVersion` / assembly version from `Directory.Build.props` (currently `1.1.0`), not a string literal in XAML. Icon: original `src/CheckmkDesktopNotifier.App/Assets/app.ico` (16–256, no Checkmk logo). A final visual replacement may still be supplied before the public GitHub Release; do not invent a new icon in this phase.

Windows 11 checklist A–T: **PASSED**. Gear/tray visual leftover (system-boxed menus) is addressed in Phase 4B.

## Phase 4B — notifications (COMPLETE / Windows-tested)

Notifications are driven by Core `AlertDelta.Opened` only. The UI does not reimplement incident lifecycle.

**Implementation:** unpackaged WinForms balloon via the existing tray `NotifyIcon.ShowBalloonTip`. No Windows App SDK, no extra NuGet, no Administrator rights, no Start Menu shortcut / AppUserModelID registration. Works from a self-contained unpackaged exe on Windows 10/11. Limitation: balloon chrome is older than Action Center toasts; rapid multiple NEW incidents in one poll may visually replace each other (each is still recorded as notified). Title ≤ 63 characters, body ≤ 255.

**Sound:** bundled `Assets/notifier.wav` (PCM WAV) via `SoundPlayer`. Volume is applied in-process by scaling PCM samples (default **30%**). Custom WAV is copied into `%LocalAppData%/CheckmkDesktopNotifier/assets/custom-notification.wav` — playback never depends on the original path. MP3/MP4 are out of V1. Mute is a separate preference (visual balloon still shown). Settings **Test notification sound** plays the selected source at the configured volume and **bypasses Mute**.

**Mute:** notifications may still appear; custom sound is disabled. Mute does not pause monitoring, mark Seen, hide the list, or call Checkmk ACK. Gear, tray, and Settings share the same `IUserPreferences`. Persisted in `%LocalAppData%/CheckmkDesktopNotifier/preferences.json` (not `settings.json`) together with volume and Default/Custom. Reset configuration does not clear these preferences.

**Startup baseline:** if local state is virgin (`GetOpenIncidents().Count == 0` and `LastSuccessfulPollUtc is null`), the first successful snapshot is the baseline — existing Checkmk problems appear in the UI with no toasts/sound. Failed polls keep the store virgin. If persisted state already exists, normal lifecycle applies (no replay of existing incidents as NEW notifications).

**De-duplication:** only `AlertDelta.Opened` notifies. The same NEW incident on later polls is not Opened. Seen / restart with persisted incidents do not notify. Recovery then recurrence Opens a new incident and may notify again.

**Menus:** dark compact WPF `ContextMenu` templates and a WinForms `ToolStripProfessionalRenderer` (subtle outer border, compact padding, thin separator, muted Exit). Gear: Connection settings, Help / About, Mute/Unmute, Hide to tray, Exit. Tray: Open, Connection settings, Help / About, Mute/Unmute, Exit.

**Problem list chrome:** `WindowChrome` with `GlassFrameThickness=0` removes the default light DWM/resize outline on `WindowStyle=None`. Dark `ScrollBar`/`ScrollViewer` templates replace the system light scrollbar (including the `ScrollViewer` corner cell). The remaining top-right light rectangle was the default Aero2 **`Button` ControlTemplate** on “Mark all new as seen” — WPF ignores `Background` unless `OverridesDefaultStyle` is set. App-level dark `Button` templates fix that; the top-right slot is now a dark ALL/NEW/CRIT/WARN/UNK filter.

**Problem list filter:** presentation-only (`ProblemListFilter` / `ProblemListFilterLogic`). Compact-bar counters **toggle**: closed → open that filter; same filter again → close; different filter → stay open and switch (no close/reopen). Filter chips in the list select without closing. Clicking the Checkmk title / non-counter bar area toggles **ALL**. Gear does not change the filter. Counters remain `Button`s excluded from drag via `IsFromButton` / `AncestorSearch`.

**Settings:** General / Connection / Notifications tabs with dark `TabControl` templates (no system tab chrome). Test connection and Test notification sound are secondary actions. Save is primary. Reset is separated. Start with Windows lives on General and applies immediately from OS autostart state.

**Compact bar width:** `SizeToContent=Width` with no `MinWidth`. The previous `MinWidth="640"` left empty dark space after the gear when content was shorter than 640px. The window now ends after the gear plus the existing 10px border padding. Status and counter text can grow and shrink the bar.

**Windows 11 manual validation: PASSED.** Phase 4C grouping/autostart is a separate phase.

### Windows 11 polish retest (Phase 4B)

No Administrator privileges. Use a self-contained win-x64 publish.

| | Test | Expected |
|---|------|----------|
| A | Gear menu spacing | Compact; no large empty area around the separator |
| B | Separator | Thin subtle line; Exit sits close underneath |
| C | Problem list border | No bright/light top line or system chrome |
| D | Problem-list scrollbar | Dark track/thumb; still scrolls; hover/drag visible |
| E | Hide to tray | Compact bar and list hide; process/monitoring/notifications continue |
| F | Tray Open | Restores the existing compact bar (not a second window) |
| G | Tray left-click | Toggles hide/restore |
| H | Custom sound | Distinct from Windows Exclamation |
| I | Mute | Visual notification still shows; custom sound does not play |
| J | Unmute | Custom sound plays again on a NEW incident |
| K | Settings → Test notification sound | Plays the custom WAV; no new incident |
| L | Real NEW incident | Exactly one visual notification + custom sound (if unmuted) |
| M | Regression | 4A/4B: Settings, About, Exit, Seen, VPN freeze, baseline suppression still work |
| N | Problem list fill | No white/light rectangle remains in ProblemListWindow |
| O | Gear separator | Separator/Exit spacing looks clean; Exit aligned with other items |
| P | Tray separator | Separator/Exit spacing looks clean; Exit aligned with other items |
| Q | Settings tabs | Connection/Notifications match the dark app style; no default light TabControl chrome |
| R | Compact bar width | Compact bar ends shortly after the gear icon with only small right padding |
| S | Compact bar trailing fill | No empty rectangle remains after gear |
| T | Compact bar resize | EN/PL and different status/counter lengths resize naturally without clipping |
| U | Filter chrome | White rectangle is gone and replaced by a dark filter control |
| V | List filters | ALL / NEW / CRIT / WARN / UNK filters work |
| W | Compact-bar counters | Clicking CRIT/WARN/UNKNOWN/NEW counters opens the corresponding filtered list |
| X | NEW + Seen | Marking one NEW Seen removes only that incident from the NEW filter |
| Y | Live polling | Filtered list updates correctly (recoveries leave, new matches appear) |

The original functional 4B checklist (baseline storm, WARN/CRIT/UNKNOWN, Seen, recurrence, restart, VPN) still applies. **Windows 11 polish retest: PASSED.**

### Windows 11 remaining 4B retest (counters + sound)

| | Test | Expected |
|---|------|----------|
| A | CRIT click | Opens CRIT view |
| B | CRIT click again | Closes ProblemListWindow |
| C | CRIT → WARN | List stays open and switches to WARN |
| D | WARN click again | Closes |
| E | NEW → NEW | Same toggle as CRIT |
| F | UNKNOWN → UNKNOWN | Same toggle as CRIT |
| G | Filter switch | No close/reopen flash |
| H | Notifications tab | Default / Custom WAV / Volume / Test / Restore default / Mute |
| I | Default sound | Bundled notifier plays |
| J | Volume 100% vs 30% | Clearly different loudness |
| K | Volume 0% | Silent |
| L | Choose Custom WAV | Import succeeds |
| M | App-owned copy | File exists under LocalAppData `assets/custom-notification.wav` |
| N | Delete original source WAV | Custom notification still works |
| O | Custom after delete | Still plays from app-owned copy |
| P | Restart | Preserves Custom + Volume |
| Q | Restore default | Bundled notifier.wav |
| R | Restart after restore | Preserves Default |
| S | Mute from gear | Real notification is silent |
| T | Tray | Same Mute state |
| U | Unmute | Sound returns |
| V | Test notification sound | Selected source + configured volume; bypasses Mute |
| W | One NEW incident | Exactly one balloon + one sound |
| X | Next poll | No duplicate |
| Y | Restart | No replay of existing incidents |
| Z | Seen single-row | Unchanged |
| AA | Mark all new as seen | Unchanged |
| AB | Filters | ALL / NEW / CRIT / WARN / UNK still correct |
| AC | Hide/restore tray | Unchanged |
| AD | VPN | Unchanged |
| AE | Settings / Credential Manager | Unchanged |
| AF | About / Exit / Seen persistence | Unchanged |

**Windows 11 remaining 4B retest: PASSED.** Sanitized confirmation: CRIT/NEW/UNKNOWN same-filter click closes the list; CRIT→WARN stays open and switches with no close/reopen flash. Notifications tab shows Default / Custom WAV / Volume / Test / Restore default / Mute. Bundled default plays; 30% is quieter than 100%; 0% is silent; custom WAV is copied to LocalAppData `assets/custom-notification.wav` and survives deleting the original source; restart preserves Custom + Volume; Restore default returns to the bundled sound; Mute/Unmute are shared between gear and tray. One NEW incident → one balloon + one sound; later polls and restart do not replay. Previously validated 4A/4B behavior (baseline, dark list/scrollbar, filters, Seen, tray, VPN, Credential Manager, compact-bar sizing) remains confirmed.

## Phase 4C — host grouping + Start with Windows (COMPLETE / Windows-tested)

Grouping is notification-only (`HostFailureNotificationGrouping`). Core/UI still list every host and service incident. Autostart is `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` value `CheckmkDesktopNotifier` (quoted current exe; no secrets; no HKLM). Settings General checkbox reflects the real OS entry.

**Windows 11 manual validation: PASSED.** Phase 4D packaging is COMPLETE / Windows-tested.

### Windows 11 checklist (Phase 4C)

No Administrator privileges. Use a self-contained win-x64 publish.

| | Test | Expected |
|---|------|----------|
| A | Host goes DOWN with multiple service failures | One host/group balloon; one sound; child incidents remain visible/NEW; no service notification storm |
| B | Next poll while host remains DOWN | No repeat grouped alert |
| C | Host recovers then fails again | New grouped alert |
| D | UNREACHABLE host | One grouped UNKNOWN-style notification (`HOST UNREACHABLE`) |
| E | Service fails on an otherwise UP host | Normal individual service notification |
| F | Mute | Grouped balloon yes; grouped sound no |
| G | Filters / Seen | ALL/NEW/CRIT/WARN/UNK and Seen remain correct; grouping does not MarkSeen |
| H | Enable Start with Windows | Checkbox on; no UAC |
| I | Per-user startup entry | `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` value `CheckmkDesktopNotifier` exists (quoted exe path) |
| J | Restart application | Checkbox remains enabled |
| K | Disable | Startup entry removed |
| L | Path with spaces | Launch from the registered command works |
| M | No Administrator prompt | Enable/disable never elevates |
| N | Settings / Credential Manager | Unchanged |
| O | Real monitoring | Still works |
| P | VPN failure/recovery | Still works |
| Q | Tray / Settings / About / Exit | Still work |
| R | Custom sound / volume / mute | Still work |
| S | Counter toggle / filter | Still work |

**Windows 11 Phase 4C validation: PASSED.** Sanitized confirmation: Settings → General → Start with Windows creates the per-user HKCU Run entry; restart preserves enabled; disable removes it; no UAC; Settings / Credential Manager unchanged. A controlled host failure produced one grouped host balloon and exactly one sound; child service incidents stayed visible/NEW; child notifications were suppressed while grouping-active; no service storm; later polls while the host stayed failed did not repeat; after recovery, a later host failure produced a new grouped notification.

## Phase 4D — per-user installer (COMPLETE / Windows-tested)

Inno Setup 6 source: `installer/CheckmkDesktopNotifier.iss`. Polish language file ships with Inno Setup 6 (`compiler:Languages\Polish.isl`).

**Windows 11 installer validation: PASSED.** Phase 5 is COMPLETE / V1 READY.

### Packaging commands

Linux publish (portable and installer input):

```bash
bash scripts/publish-win-x64.sh
# or:
dotnet publish src/CheckmkDesktopNotifier.App/CheckmkDesktopNotifier.App.csproj \
  -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o publish/win-x64
```

Windows installer (Inno Setup 6 `iscc` on PATH, or `C:\Program Files (x86)\Inno Setup 6\ISCC.exe`):

```powershell
powershell -File scripts/build-windows-package.ps1
```

Equivalent:

```text
iscc /DMyAppVersion=1.1.0 installer\CheckmkDesktopNotifier.iss
```

Output: `artifacts/CheckmkDesktopNotifier-Setup-x64.exe` (gitignored). `publish/` and `artifacts/` must not be committed.

### Windows 11 checklist (Phase 4D)

No Administrator privileges.

| | Test | Expected |
|---|------|----------|
| A | Run installer as normal user | Completes |
| B | No UAC | No admin prompt |
| C | Install path | `%LocalAppData%\Programs\CheckmkDesktopNotifier` |
| D | Start Menu shortcut | Exists, uses app icon, launches exe |
| E | Optional desktop shortcut | Created only if selected |
| F | Launch after install | Default on; launches installed exe, no elevation, no credentials on the command line |
| G | Fresh profile | Setup required / Settings |
| H | Installer Start with Windows | Same HKCU Run value `CheckmkDesktopNotifier` |
| I | App Settings | Reflects installer autostart state |
| J | Toggle in app | Still works after install |
| K | No duplicate autostart | No Startup folder / task / second Run value |
| L | Configure app | Settings + Seen + custom WAV + volume/mute |
| M | Upgrade | Newer setup over existing install |
| N | Settings survive | Yes |
| O | Credential Manager secret | Survives |
| P | Seen/open state | Survives |
| Q | Custom WAV | Survives |
| R | Volume/Mute | Survive |
| S | Autostart | Survives or repairs to installed path |
| T | Launch after upgrade | Normal |
| U | Normal uninstall | Removes binaries/shortcuts |
| V | Default uninstall | Preserves `%LocalAppData%\CheckmkDesktopNotifier` |
| W | Reinstall | Restores previous configuration |
| X | Optional data removal | Deletes user-data folder; attempts `cmdkey /delete:CheckmkDesktopNotifier` |
| Y | Upgrade while running | Mutex prompt; user Exits from tray; no corrupt state |
| Z | After uninstall | No leftover notifier process |
| AA | Real monitoring | Works from installed exe |
| AB | Host grouping | Works |
| AC | Notifications/sound/mute | Work |
| AD | Settings/About/Exit/tray | Work |
| AE | Autostart after login | Launches **installed** path |

**Windows 11 Phase 4D validation: PASSED.** Sanitized confirmation: `CheckmkDesktopNotifier-Setup-x64.exe` builds and runs as a normal user with no UAC. Install path is `%LocalAppData%\Programs\CheckmkDesktopNotifier`. The installed app launches; the Start Menu shortcut works; existing GUI settings and Credential Manager remain usable. HKCU autostart points at the installed executable and is the same mechanism as Settings. Starting the installed exe while the notifier is already running reuses/activates the existing instance (no second poller, no duplicate notifications).

## Phase 5 — COMPLETE / V1 READY

User documentation: `README.md` / `README.pl.md`. Release notes: `docs/RELEASE_NOTES_1.0.0.md`, `docs/RELEASE_NOTES_1.1.0.md`. MIT `LICENSE` at the repository root. Sanitized screenshots under `docs/images/`.

Installer SHA-256 for **1.0.0** is recorded in `SHA256SUMS.txt` (do not treat it as a 1.1.0 checksum). About on Windows 11 for this line is **1.1.0**.

Do not implement ticketing or Untake in v1.1.0. GitHub Release for 1.1.0 is a separate follow-up after tag `v1.1.0`. Phase 6A is COMPLETE / Windows-tested. Phase 6B is COMPLETE / Windows-tested. v1.1.0 is FEATURE COMPLETE / READY FOR RELEASE.

## Phase 6A — Take / shared ACK (COMPLETE / Windows-tested)

Ticketing / Zoho / Untake / `expire_on` remain out of v1.1.0. Git tag `v1.1.0` is created as part of the v1.1.0 close-out. GitHub Release is a separate follow-up.

**Semantics:** Seen is local. Take creates a sticky Checkmk ACK (`notify=false`). The write comment is a **single line** (`Taken by {name} via Checkmk Desktop Notifier cdn.v1 take name="..."`) because Checkmk RAW truncates `\n` ACK comments to the first line (live GO-S11). Do not revert to multiline. Taken-by is parsed from that comment (machine tag preferred; `Taken by {name} via Checkmk Desktop Notifier` accepted; plain `Taken by {name}` is generic ACK). Display name is in `preferences.json`. Mock does not write Checkmk. Search and TAKEN are presentation-only.

**Permissions:** read-only monitoring still works. Take needs `action.acknowledge`. Not Checkmk Administrator.

### Windows 11 checklist (Phase 6A)

No Administrator privileges. Self-contained win-x64 publish (`1.1.0`). Existing v1.0 settings migrated. Cross-client sync also validated on a second PC and on Windows 7 (additional check, not a new support claim).

| | Test | Result |
|---|------|--------|
| A | Existing v1.0 settings migrate; monitoring starts | PASS |
| B | Settings → General: Enable Take and set display name | PASS |
| C | Unacknowledged service row shows Take | PASS |
| D | Dark in-app confirmation (Take / Cancel). Enter confirms, Escape/close cancels. No “don’t show again”. Not a white Windows MessageBox | PASS |
| E | Confirm → Taking... | PASS |
| F | Checkmk receives sticky ACK (`persistent=false`, `notify=false`, no `expire_on`) | PASS |
| G | Problem remains CRIT/WARN/UNKNOWN and visible | PASS |
| H | After the next successful poll (no restart): Taken by &lt;display name&gt;. Checkmk source of truth; no optimistic Taken. Single-line CDN marker survives RAW 2.4 storage | PASS |
| I | Another notifier user / second PC sees the same Taken state after polling | PASS (also Windows 7) |
| J | Local Seen independent of Take/ACK | PASS |
| K | Manual Checkmk ACK shows ACK, not a fake TakenBy; generic ACK not in TAKEN | PASS |
| L | WARN → Take → CRIT stays Taken | **Automated tests PASS.** Manual **N/A** (not practical to reproduce safely). Not a failure. |
| M | CRIT → OK, later CRIT is a new non-Taken incident | **Automated tests PASS.** Manual **N/A** (not practical to reproduce safely). Not a failure. |
| N | Already ACKed NEW incident: no balloon/sound; still locally NEW | PASS |
| O | ACKed host DOWN: no grouped balloon/sound | PASS |
| P | Child services remain visible/NEW; not auto-ACK’d | PASS |
| Q | 403 / read-only: still monitors; Take unavailable; no false Taken | PASS |
| R | Write/network failure does not invent Taken state | PASS |
| S | Filters, Seen, custom sound, mute, tray, autostart, restart/read-back remain intact | PASS |
| T | Search: host / service / Taken-by; composes with ALL/NEW/CRIT/WARN/UNK/TAKEN | PASS |
| U | TAKEN chip and global counter: CDN Takes only | PASS |
| V | TAKEN + search composition | PASS |

**Windows 11 Phase 6A validation: PASSED** (L and M by automated tests; all other items live-tested).

## Phase 6B — Open in Checkmk + Seen/Unseen (COMPLETE / Windows-tested)

Final v1.1.0 feature work. Untake/Release remains out of scope (v1.2.0). Git tag `v1.1.0` is part of the v1.1.0 close-out. GitHub Release is a separate follow-up.

**Open in Checkmk:** `CheckmkGuiUriBuilder` + `ICheckmkProblemNavigator`. GUI URL is `{BaseUrl}/{site}/check_mk/index.py?start_url=view.py?view_name=host|service&host=...&service=...`. REST `show` links are API invoke URLs and are not used. Default browser via `IUriLauncher`. Open never mutates incident state.

**Seen / Unseen:** `IAlertStateService.MarkUnseen` flips local `IsSeen` only. Same JSON store. No `AlertDelta.Opened`, so no balloon/sound replay. Eye stays on every row (open eye = Mark seen, slashed eye = Mark unseen).

### Windows 11 checklist (Phase 6B)

No Administrator privileges. Existing Phase 6A Take behavior remains intact.

| | Test | Result |
|---|------|--------|
| A | Open icon on a service row | PASS |
| B | Service Open | PASS |
| C | Host Open | PASS |
| D | Default Windows browser | PASS |
| E | Open vs NEW/Seen | PASS |
| F | Open vs Take/ACK/Taken | PASS |
| G | NEW → Seen | PASS |
| H | Seen → Unseen | PASS |
| I | Unseen immediately in NEW | PASS |
| J | NEW counter updates immediately | PASS |
| K | NEW filter shows Unseen | PASS |
| L | Seen → Unseen: no balloon | PASS |
| M | Seen → Unseen: no sound | PASS |
| N | Repeated Seen/Unseen: no notification spam | PASS |
| O | Taken by &lt;name&gt; remains Taken | PASS |
| P | Generic ACK remains ACK | PASS |
| Q | Restart preserves Seen/Unseen | PASS |
| R | Mark all new as seen | PASS |
| S | Search + filters | PASS |
| T | TAKEN | PASS |
| U | Take flow | PASS |
| V | Dark Take confirmation | PASS |
| W | Downtime badge | PASS |

**Windows 11 Phase 6B validation: PASSED.** v1.1.0 is FEATURE COMPLETE / READY FOR RELEASE.

## Windows — self-contained win-x64 publish

No admin required. From the repository root:

```powershell
dotnet publish src/CheckmkDesktopNotifier.App/CheckmkDesktopNotifier.App.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=false `
  -o publish/win-x64
```

From Linux (cross-compile the Windows app):

```bash
dotnet publish src/CheckmkDesktopNotifier.App/CheckmkDesktopNotifier.App.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=false \
  -o publish/win-x64
```

Run `publish\win-x64\CheckmkDesktopNotifier.exe` on Windows 11.

`publish/` and `artifacts/` are gitignored. Do not commit publish or installer output.

## Tests

```bash
dotnet test CheckmkDesktopNotifier.sln
```

Core tests cover lifecycle, persistence (including isolated vs legacy alert-state fallback), identities, recurrence, the demo snapshot mix, compact-bar ancestor/pointer-origin logic (`Run` vs Visual vs Button, including counter buttons), and presentation-only problem-list filtering.

Infrastructure tests cover service JSON mapping, WARN/CRIT/UNKNOWN, SOFT/HARD, ACK/downtime, Unix timestamps, malformed JSON, HTTP non-success, auth header construction, config validation, Core independence from REST DTOs, host collection inspection/mapping, merged service+host snapshots, polling (immediate first poll, interval, no overlap, cancellation, failed poll freeze, persistence reload), GUI settings / Credential Manager / connection tester, Mock vs Real startup flags, Phase 4B notifications (Opened-only, baseline storm suppression, mute, sound preview, backend failure isolation), and Phase 4C host-failure grouping plus autostart (fake store). Core also covers hide/restore/tray-toggle visibility, the bundled notifier WAV header, grouped host alert copy, HKCU Run command formatting, central version/packaging invariants, and install vs user-data path separation.

There is no WPF UI test project yet.

## Secrets

Never commit:

- Checkmk automation secrets / passwords
- API tokens
- `.bin` DPAPI blobs
- production URLs with embedded credentials
- Event Log dumps that contain Authorization headers
- `config/checkmk.local.json` (gitignored)

Phase 3D stores the automation secret in Windows Credential Manager (this Windows user). Developer/CI may still use `config/checkmk.local.json` (gitignored) or `CHECKMK_*` / `CHECKMK_CONFIG`. Do not commit secrets.

## Architecture reminders

- Do not put incident logic in WPF code-behind.
- Do not put Checkmk REST DTOs in Core.
- Do not call Checkmk ACK from the eye button.
- Take is a separate command. It must not mark Seen.
- Read `docs/CHECKMK_API.md` before any HTTP work. Host monitoring is verified **GET** with repeated `columns=` query parameters, not an invented POST.
- Phase 3C is complete. Phase 3D is complete. Phase 4A is COMPLETE / Windows-tested. Phase 4B is COMPLETE / Windows-tested. Phase 4C is COMPLETE / Windows-tested. Phase 4D is COMPLETE / Windows-tested. Phase 5 is COMPLETE / V1 READY. Phase 6A is COMPLETE / Windows-tested. Phase 6B is COMPLETE / Windows-tested. v1.1.0 is FEATURE COMPLETE / READY FOR RELEASE. Do not implement Untake. Do not revert CDN Take comments to a multiline format.
