# Decisions

Record why, not only what. Change a decision here when the product changes.

## Local Seen is not Checkmk ACK

Clicking the eye means: this Windows user has seen this **local** uninterrupted incident; do not notify again for it.

Phase 6B makes the eye reversible: Mark unseen returns the same incident to local NEW. That is presentation/workflow state only. It must never write Checkmk, and it must never replay balloon/sound because Unseen is not a newly opened monitoring incident (`AlertDelta.Opened` stays empty).

It must never call Checkmk acknowledge APIs. `acknowledged` from Checkmk is read-only metadata (badge / TAKEN vs ACK). ACK must not look like or set Seen.

## HARD-only incidents in V1

`state_type`: 0 SOFT, 1 HARD.

Only HARD non-OK problems open or continue incidents. Soft blips would consume Seen before Checkmk itself would notify.

## WARN → CRIT is the same incident

Identity is the monitored object, not the severity. Escalation updates severity and compact-bar counts. It does not open a second incident and does not clear Seen.

## Recurrence markers

- Service: `last_time_ok`
- Host: `last_time_up`

Used to detect `CRIT → (app offline) → OK → CRIT` when snapshot diff cannot see the OK gap.

Do not use `last_state_change` / `last_hard_state_change` for recurrence; WARN→CRIT changes those too.

Both bound and current markers must be usable (`> Unix epoch`) and current must be greater than bound.

## Failed snapshot never recovers

A timeout, 401, or protocol error is not an empty problem list. Previous open/Seen state stays. Compact bar should keep last known counts (Phase 3+).

## Host and service identities cannot collide

`(Site, Host, SRV-WEB02)` ≠ `(Site, Service, SRV-WEB02, CPU utilization)`.

## Host DOWN grouping is presentation / notification, not identity

The engine keeps per-object incidents (host + each service). Later UI/toasts may show `SRV-SQL01 DOWN — 17 affected services`. Do not merge those objects in Core.

## Default poll interval: 60 seconds

Configurable via `PollIntervalSeconds` (file + `CHECKMK_POLL_INTERVAL_SECONDS`). Suggested presets: 10s, 30s, 60s, 2 min, 5 min. Minimum floor 10s.

Implemented in Phase 3C as a desktop `BackgroundService` (`CheckmkPollingHostedService`), not a Windows Service. First poll is immediate. Subsequent waits use `Interval - elapsed`. `SemaphoreSlim` prevents overlapping request cycles; a busy `RefreshAsync` is skipped. HTTP timeout is shorter than the interval (`max(5, interval-2)` seconds). A full cycle is two HTTP calls (service POST + host GET) and may exceed the interval; the next wait is then skipped so polls never overlap.

Failed polls continue the loop. Core still does not recover incidents from a failed snapshot.

## Alert state JSON is per-user LocalAppData (Real mode)

Real mode persists open incidents, Seen, recurrence markers, and `LastSuccessfulPollUtc` under `%LocalAppData%/CheckmkDesktopNotifier/`. Files are isolated by Checkmk connection identity (SHA-256 of normalized BaseUrl + Site) as `state/<id>/alert-state.json`. A legacy `alert-state.json` in the same folder is used only as a **read fallback** when the isolated file does not exist yet. It is not copied, moved, or deleted automatically. After the isolated file has been written, the root file is unused and may be removed manually.

Secrets, Authorization headers, and Checkmk URL/credentials are **not** stored in alert-state files.

A diagnostic `last-poll.txt` in the app-data folder records success/failure and host/service **counts** only.

## GUI settings vs developer config (Phase 3D)

Normal Windows users configure Checkmk through the Settings window. Non-secret fields are stored in `settings.json`. The automation secret is stored in **Windows Credential Manager** (generic credential, persist-local-machine, bound to this Windows user, no Administrator rights). This is an OS secret store, not application-layer encryption and not a hardcoded key. Phase 3D Windows validation confirmed this storage split.

Developer/CI overrides remain available and are documented in DEVELOPMENT.md. `CHECKMK_CONFIG` is an explicit override of GUI settings. Leftover `CHECKMK_*` environment variables do **not** override a saved GUI configuration. The Settings window edits GUI settings only; it does not display an active developer-file/env connection.

Truly unconfigured state (no usable current connection) shows **Setup required**. Historical incident files may remain visible; they must not themselves imply **Connected**.

## Compact-bar pointer sources are not always Visual

Routed mouse `OriginalSource` on compact-bar labels can be a `Run` (`FrameworkContentElement`). Ancestor walks must not call `VisualTreeHelper.GetParent` unless the object is a `Visual` or `Visual3D`. Content elements use content/logical parent APIs. Settings-gear clicks must not start a drag or toggle the problem list.

## Desktop shell commands (Phase 4A)

Gear menu and tray must call the same `IShellCommands` implementation (`UiShell`): Open compact bar, Hide to tray, Toggle bar (tray left-click), Connection settings, Help / About, Exit. Do not duplicate hide/show or lifecycle logic. Hide to tray hides the existing compact bar and problem list; it does not exit, pause monitoring, or create a second bar. Tray **Open** always restores the existing window. Tray left-click toggles visibility.

## Tray uses WinForms NotifyIcon

No extra tray NuGet. `System.Windows.Forms.NotifyIcon` is part of the Windows desktop TFM (same license as .NET). Prefer this over Hardcodet.NotifyIcon.Wpf to avoid another UI framework. Left-click (and the Open menu item) activates the existing compact bar.

## Desktop notifications (Phase 4B)

Unpackaged self-contained exe does **not** get reliable Windows App SDK / CommunityToolkit toast delivery without an AppUserModelID plus a Start Menu shortcut (fragile, often needs extra install steps). Phase 4B therefore uses the existing tray icon's `NotifyIcon.ShowBalloonTip`.

- Windows 10/11, no Administrator privileges
- No extra NuGet
- Works from unpackaged `dotnet publish` output
- Limits: balloon UI (not Action Center toast chrome); title 63 / text 255 characters; overlapping balloons may replace each other

Sound is an original bundled WAV (`Assets/notifier.wav`), played with `System.Media.SoundPlayer` (no extra audio NuGet). It is a short synthetic three-tone motif (G5 → D6 → B5, 16-bit PCM mono 22050 Hz, ~350 ms). **V1 is WAV-only** (uncompressed PCM, 8- or 16-bit, mono or stereo, ≤ 5 seconds). MP3/MP4 would need another decoder; they are out of V1.

Volume is application-only: PCM samples are scaled in memory (default **30%**). The app does not change Windows master volume or other apps. `SoundPlayer` has no volume API.

Custom WAV is **imported** into `%LocalAppData%/CheckmkDesktopNotifier/assets/custom-notification.wav`. Preferences store Default vs Custom, volume, mute, and the display file name — not the original source path. If the imported file is missing or invalid, playback falls back to the bundled default without crashing. Restore default selects the bundled asset, does not reset mute/volume/connection settings, and deletes the imported copy when safe.

Mute persists in `preferences.json` (non-secret) and remains a separate switch from volume 0%. Mute never means pause, Seen, or Checkmk ACK. Settings **Test notification sound** plays the selected source at the configured volume without creating an incident; it bypasses mute so the asset can be heard while muted.

Notifications fire only for Core `AlertDelta.Opened` after host-failure grouping. First successful snapshot on virgin local state is a silent baseline. Host-DOWN grouping and per-user autostart are Phase 4C (**COMPLETE / Windows-tested**). Installer work is Phase 4D (**COMPLETE / Windows-tested**). V1 documentation/release is Phase 5 (**COMPLETE / V1 READY**).

## Host-failure notification grouping (Phase 4C, COMPLETE / Windows-tested)

Grouping is a **notification/presentation** decision. Core identities, ProblemListWindow rows, NEW/Seen, ACK badges, downtime, and plugin output are unchanged.

Algorithm (same successful `ProblemSnapshot`, no sleep window):

1. Grouping hosts = snapshot host problems that are HARD + Critical (DOWN) or Unknown (UNREACHABLE). Do not infer host failure from services.
2. If a grouping host is in `AlertDelta.Opened` and not Seen → one grouped balloon (`HOST DOWN` / `HOST UNREACHABLE` + hostname + `{n} affected service(s)`) and one sound.
3. NEW child services of a grouping host (same SiteId + HostName) are not notified.
4. Later polls while the host stays down do not repeat the grouped balloon. New children in those polls still open as NEW in Core/UI with no storm.
5. Recovery has no sound. A later host recurrence may group again.

**Affected-service count** uses merged snapshot service problems for that host, not REST `num_services_hard_*` (those counters are not on `MonitoredProblem`; the snapshot is what the list shows).

**ACK / downtime:** do not suppress grouped (or individual) notifications. Same as Phase 4B: ACK and downtime are Checkmk metadata; local Seen is independent; grouping suppression is a third concept. A child ACK does not suppress a host group. A host ACK/downtime does not suppress the grouped balloon in Phase 4C.

## Start with Windows (Phase 4C, COMPLETE / Windows-tested)

Mechanism: per-user `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, value name `CheckmkDesktopNotifier`, command = quoted `Environment.ProcessPath` only. No arguments, no secrets, no HKLM, no scheduled task, no elevation.

If the publish/install path changes, re-enable or opening Settings / startup `RepairIfRegistered` rewrites the command to the current exe. Settings checkbox reads the **actual** Run value, not `preferences.json`.

**Phase 4D installer:** use this same HKCU Run value as the single source of truth. Do not add a parallel Startup-folder shortcut for “Start with Windows”. The installer checkbox should write/delete this value; the app General tab should keep showing OS state. After install, if the value already exists (portable path), rewrite it to the quoted installed exe. App `RepairIfRegistered` does the same when the installed process starts.

## Per-user installer (Phase 4D, COMPLETE / Windows-tested)

Inno Setup 6, `PrivilegesRequired=lowest`, `AppId={{B7C4E91A-5D2F-4A38-9E16-8F0C3B2A1D47}`. License: compiling an installer with Inno Setup does not relicense this project; we commit only `.iss` source, not Inno binaries. Generated setup EXE is an artifact, not source.

Upgrade replaces `{app}` only. User data and Credential Manager survive. Uninstall removes binaries/shortcuts/Run value; optional Yes (default No) deletes `%LocalAppData%\CheckmkDesktopNotifier` and runs `cmdkey /delete:CheckmkDesktopNotifier`. If `cmdkey` fails, the user can delete Generic credential `CheckmkDesktopNotifier` in Windows Credential Manager.

The compact bar cancels `Closing`, so Restart Manager close is unreliable. Setup `AppMutex` matches `SingleInstanceIdentity.MutexName` and asks the user to Exit. No silent `taskkill`.

Unsigned builds: Windows SmartScreen / “unknown publisher” is expected until a real Authenticode certificate is used. Do not check in self-signed certs.

## Single-instance (Phase 4D, COMPLETE / Windows-tested)

Installed + Start Menu + autostart + desktop would otherwise start multiple pollers and duplicate balloons. A per-user `Local\` mutex is required. Second launch sets `ActivateEventName`; the first instance calls `ShowBar`. Portable and installed share the mutex for this logon. Not `Global\` (no admin). Not a machine-wide service.

## Central version (Phase 4D, COMPLETE / Windows-tested)

`Directory.Build.props` defines `Version` / `AssemblyVersion` / `FileVersion` / `InformationalVersion`. About reads informational version. Inno uses `/DMyAppVersion` with the same value (fallback `#define` in the `.iss` must match; PackagingTests checks). Do not hardcode the version in the About UI. Product version is **1.2.0**.

## Dark compact menus (Phase 4B)

Gear uses a custom WPF `ContextMenu`/`MenuItem` template (dark `#252A33`, subtle `#3A4150` border, compact padding, 1px low-contrast separator, muted Exit with the same item padding). Tray uses a WinForms `ToolStripProfessionalRenderer` with the same palette, a 1px separator, and `ShowImageMargin=false`.

## Problem list light rectangle (Phase 4B)

The bright rectangle at the top-right of `ProblemListWindow` was **not** the scrollbar. Default WPF `Button` (Aero2) paints system chrome and ignores `Background`/`BorderThickness` unless `OverridesDefaultStyle` is true. The header “Mark all new as seen” button was that fill. App-wide dark `Button` templates remove default light chrome (including per-row eye buttons). The `ScrollViewer` template also omits the system `Corner` rectangle. That header slot now hosts the dark filter chips.

## Problem list filter (Phase 4B)

Compact-bar counters call `ToggleCounter` (same filter closes; different filter switches without collapsing). Filter chips call `OpenFilter`. The gear menu does not. Opening the list from the bar background (non-counter area) uses **ALL**.

## Compact bar width (Phase 4B)

`CompactBarWindow` uses `SizeToContent=Width` and a horizontal `StackPanel`. A `MinWidth="640"` was forcing unused dark space after the gear whenever the content (EN status, small counters) was narrower than 640px. There is no `MinWidth` now; first-run placement uses the laid-out `ActualWidth` instead of a hardcoded 720px assumption. Hide/restore keeps the saved position; opening the problem list does not set bar width.

## Shutdown is explicit

`ShutdownMode=OnExplicitShutdown`. Closing Settings, About, or the problem list does not exit. Exit cancels polling (`ResetPollingAsync`), closes dialogs, disposes the tray, then `Application.Shutdown()`. Do not use `Environment.Exit` for the normal path.

## About version and icon

About reads version from assembly informational/assembly metadata. Do not hardcode `1.0.0` in the UI. The GitHub URI is `ProductInfo.Repository`. The app icon is an original placeholder `Assets/app.ico` (dark monitor + heartbeat, no Checkmk logo), easy to replace later.

## Startup status is session-based

Until initialization completes, the compact bar shows **Initializing...**. Historical incidents may be listed, but **Connected** is only shown from this session's poller status, not from a persisted last-successful-poll timestamp.

## No Windows Service in V1

Normal per-user desktop app, tray/bar later. No admin-required service.

## Open-source MIT

The repository is MIT-licensed (`LICENSE`, copyright TimeWizard007, 2026). No hardcoded sites, users, or secrets. No Checkmk logos. Inno Setup binaries are not committed.

## No Checkmk logos or trademarks bundled

Do not ship Checkmk brand assets. The compact bar title “Checkmk” is a plain text label for the product being monitored, not a logo.

## Technology (V1)

C# / .NET 8 / WPF / MVVM / Windows 10/11 / DI / async + `CancellationToken` / local JSON state / minimal NuGet (CommunityToolkit.Mvvm, Microsoft.Extensions.Hosting).

## REST vs Livestatus

V1 HTTP path is the verified REST status collections. Livestatus remains a possible later `ICheckmkClient` adapter, not required for V1.

## Take vs Seen vs ACK (Phase 6A, COMPLETE / Windows-tested)

Keep these concepts separate:

- **Seen** is local per Windows user. Eye toggles Seen/Unseen. Mark all new as seen. Never writes Checkmk. Unseen does not replay notifications.
- **Take** is a shared team action. It creates a Checkmk sticky ACK (`notify=false`). It does not hide the row, change severity, mark Seen, ACK children, create a ticket, or use Checkmk `notify`.
- **ACK** is shared Checkmk state (notifier, GUI, or another tool).
- **Taken by** is shown only when an acknowledgement comment is a CDN Take. Prefer `cdn.v1 take name="..."`. Checkmk RAW 2.4 stores ACK comments as a single line (`\n` is truncated; live GO-S11). Writes therefore put human text, via-phrase, and machine tag on one line. Flattened `Taken by {name} via Checkmk Desktop Notifier` still counts as Taken on read. Never invent identity from the Checkmk author (live: `checkmk-desktop-notifier`). A generic “Taken by …” ACK without the CDN via-phrase stays **ACK**.

TAKEN filter/counter (added during live Windows 6A testing): presentation-only view of open incidents with `IsTakenByNotifier`. Not a local ownership database. Generic Checkmk ACK is excluded. Search composes with TAKEN and severity filters.

Display name lives in `preferences.json` with mute/volume (non-secret per-user prefs). Not Credential Manager. Not `settings.json` (connection). Take is disabled by default so existing 1.0 installs stay read-only until opted in.

`ICheckmkClient` stays read-only. Writes use `ICheckmkAcknowledgementClient`. Same automation secret. Take requires `action.acknowledge` and remains optional; a read-only account continues to monitor.

No Untake in v1.1: ACK ends on OK/UP; manual removal is Checkmk UI. Safe Release/Untake is **v1.2.0 / Phase 7A COMPLETE / Windows-tested**. Live-validated `POST /domain-types/acknowledge/actions/delete/invoke` removes CDN Takes only. Generic/manual ACK is never deleted. `Taken by <name>` is the Release action. Any admin may release any CDN Take. Successful Take/Release waits on Checkmk read-back (`Taking...` / `Releasing...`) and does not show a native Windows MessageBox. No `expire_on` (HTTP 400 on validated RAW 2.4.0p34). No custom backend. Concurrent Takes may leave multiple ACK comments; UI shows the newest valid CDN Take. ACK/Taken fields must refresh on every successful snapshot.

Sticky is intentional so WARN → Take → CRIT stays Taken (same local incident).

## Open in Checkmk uses GUI URLs, not REST show links

REST collection items may include `rel: urn:com.checkmk:rels/show` hrefs under `/api/1.0/objects/.../actions/show_service/invoke`. Those are API invoke endpoints, not the interactive GUI. Phase 6B builds `{BaseUrl}/{site}/check_mk/index.py?start_url=view.py?...` from the configured origin, site, and host/service identity. No credentials or automation secret in the URL. Default browser via `IUriLauncher` (Windows: shell execute).

## Phase M0 — shared ports, Windows host frozen (COMPLETE / Windows-tested)

Keep the released WPF app. Extract Windows-only I/O (Credential Manager, HKCU Run, shell URI, LocalAppData roots) behind Core ports so a later Avalonia macOS host can supply Keychain, login items, NSWorkspace, and Application Support paths. Do not change Take/Release/Seen/notification semantics. Phase M1 is planned, not started.

## Host HTTP method

Until proven otherwise, host monitoring is **GET** `/domain-types/host/collections/all`. Do not invent a host POST.

## UI localization

`en` + `pl` from the start. No user-visible strings hardcoded in ViewModels when a resource key exists. English remains the source language.

## Phase 2 window owner

`Window.Owner` may be set only after the owner window has been shown (valid HWND). Compact bar is the main Always-on-Top window; the problem list becomes its owned window after `CompactBarWindow.Show()`.
