# Checkmk Desktop Notifier

**[English](README.md)** | **[Polski](README.pl.md)**

A lightweight Windows desktop monitor and notifier for Checkmk.

This is an **independent open-source project**. It is **not affiliated with Checkmk GmbH**, and is not endorsed by or a product of Checkmk GmbH. “Checkmk” is used only to name the monitoring system this companion talks to.

Current release line: **1.0.0**.

## Overview

Checkmk Desktop Notifier is a per-user Windows 10/11 companion. It polls Checkmk over the REST API, shows current HARD host and service problems in a compact Always-on-Top bar, and raises desktop notifications when new local incidents open.

It is **not** a replacement for the Checkmk web UI. It does not write acknowledgements, downtimes, or configuration back to Checkmk. Local **Seen** state lives on this Windows user only.

## Screenshots

Host names and internal URLs are omitted or replaced with examples.

![Compact Always-on-Top bar](docs/images/compact-bar.png)

![Problem list and severity filters](docs/images/problem-list.png)

![Settings — Connection](docs/images/settings-connection.png)

![Settings — Notifications](docs/images/settings-notifications.png)

![System tray menu](docs/images/tray-menu.png)

![About (version 1.0.0)](docs/images/about.png)

## Features

- Compact Always-on-Top bar with NEW / CRIT / WARN / UNKNOWN counts
- Expandable problem list (hosts and services, plugin output, EN/PL UI)
- Clickable counters and filter chips (presentation-only; incidents are not merged)
- Local NEW / Seen (per Windows user, persisted)
- Read-only Checkmk ACK and scheduled-downtime badges
- Background polling of Checkmk service and host REST collections
- Windows balloon notifications and alert sound
- Bundled WAV, optional custom WAV, per-app volume, mute
- HOST DOWN / UNREACHABLE notification grouping (notifications only)
- System tray (show / hide / Settings / About / Mute / Exit)
- Start with Windows (per-user HKCU Run)
- GUI Settings; automation secret in Windows Credential Manager
- Per-user installer (no Administrator rights)
- Single-instance: a second launch activates the existing notifier
- Self-contained portable win-x64 publish remains supported

## Requirements

**Windows**

- Windows 10 or Windows 11 (64-bit)
- No Administrator rights for install or normal use
- Network path to the Checkmk server (for example a VPN, if that is how you reach the site)

**Checkmk**

- Verified against **Checkmk CRE / RAW 2.4.0p34** REST API 1.0
- Other editions/versions that expose the same service POST and host GET collections may work; they are not claimed as tested
- An automation user that can **read** the hosts and services you care about (see [Checkmk automation user](#checkmk-automation-user))

## Installation

Recommended for normal use: the per-user installer `CheckmkDesktopNotifier-Setup-x64.exe` from a GitHub Release.

- Runs as a **normal Windows user**. No Administrator / UAC elevation.
- Installs to `%LocalAppData%\Programs\CheckmkDesktopNotifier`
- Always creates a **Start Menu** shortcut
- Optional **desktop shortcut** (off by default)
- Optional **Start with Windows** (same HKCU Run value as Settings → General)
- Does **not** require `checkmk.local.json`, `CHECKMK_CONFIG`, or environment variables

The installer and the application share one autostart mechanism:

`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`  
value name: `CheckmkDesktopNotifier`  
command: quoted path to `CheckmkDesktopNotifier.exe`

There is no Startup-folder shortcut, scheduled task, or HKLM entry for this option.

Verify the installer hash against [SHA256SUMS.txt](SHA256SUMS.txt) before running it.

### Unsigned installer / SmartScreen

V1 binaries are **unsigned**. Windows SmartScreen may show an “unknown publisher” warning. That warning means the file is not Authenticode-signed; it does **not** by itself mean the file is malicious. Download only from this repository’s official source, and verify the SHA-256 in [SHA256SUMS.txt](SHA256SUMS.txt). Do **not** disable SmartScreen globally.

## First run / configuration

1. Start **Checkmk Desktop Notifier** from the Start Menu.
2. Open **Settings** (gear on the compact bar, or tray → Connection settings).
3. On the **Connection** tab, enter:

   | Field | Meaning |
   |-------|---------|
   | Checkmk server URL | Origin only, for example `https://checkmk.example.com` — no site path, no credentials |
   | Site | Checkmk site name, for example `mysite` |
   | Username | Automation user name |
   | Automation secret | Automation secret (stored in Credential Manager, not in `settings.json`) |
   | Polling interval | Seconds between polls (default 60, minimum 10) |

4. Click **Test connection**. The app checks that both the service and host REST collections are reachable.
5. Click **Save**. Monitoring starts with an immediate first poll.

The installed application does **not** need `config/checkmk.local.json`, `CHECKMK_CONFIG`, or `CHECKMK_*` environment variables. Those remain **developer / CI overrides** only.

On a **first successful poll** with empty local incident state, current problems are loaded silently (no notification storm). Later polls notify only newly opened local incidents.

## Checkmk automation user

V1 is **read-only** toward Checkmk. Create an **automation user** with an **automation secret** (not an interactive password login).

Working V1 model:

- Role: **Normal monitoring user** is sufficient for the monitored scope (verified)
- Contact-group membership must include the hosts and services you want to see
- Checkmk Administrator rights are **not** required
- A narrower “read these two collections only” role was **not** tested

Do not put a real username, secret, URL, or internal group name in this repository.

The notifier displays Checkmk **ACK** as metadata. That is not the same as local **Seen**.

## Notifications

A Windows balloon (via the tray icon) and, unless muted, one alert sound are emitted when a local incident **opens** (`AlertDelta.Opened`) after host-failure grouping.

- Later polls of the same uninterrupted incident do not repeat the balloon/sound
- Restart does not replay already-open incidents
- A failed poll never looks like “everything recovered” and does not emit recovery noise
- Mute turns **sound** off; balloons still appear
- Checkmk ACK / downtime do **not** suppress notifications in V1

## NEW and Seen

**NEW** means: this Windows user’s notifier has not marked this local incident as Seen.

**Seen** is **local, per Windows user**:

- The eye button marks **that** incident Seen
- **Mark all new as seen** marks every currently NEW incident
- Seen is stored on disk and **survives restart**
- Seen is **not** sent to Checkmk
- Seen is **not** shared with other administrators or other Windows users

If two people run the notifier, each has their own NEW/Seen state. Shared / team workflow is a **post-V1** roadmap item (evaluate Checkmk ACK and ticket-system integration first; this project will not start from a custom shared backend).

## Checkmk ACK and downtime

- **ACK** from Checkmk is **read-only** metadata (badge). V1 does not create or write acknowledgements.
- **Scheduled downtime** is **read-only** metadata. V1 does not schedule or remove downtime.
- ACK is **not** Seen. Marking Seen does **not** ACK in Checkmk.

## Host DOWN / UNREACHABLE grouping

Grouping affects **notifications only**. Incidents are **not** merged in the engine or in the problem list.

If a HARD **DOWN** (Critical) or **UNREACHABLE** (Unknown) host has affected child services in the same snapshot:

- One grouped host balloon and one sound are emitted (`HOST DOWN` / `HOST UNREACHABLE` plus an affected-service count)
- Child service incidents stay fully visible and keep their own NEW/Seen
- Child service balloons/sounds are suppressed while that host grouping is active
- Later polls while the host stays failed do not repeat the grouped balloon
- This avoids a service-notification storm when a host fails

ACK or downtime on the host or on children does **not** suppress the grouped balloon.

## Tray behavior

The app keeps a system tray icon.

- Closing the compact bar **hides** it (does not exit)
- Tray **Open** or left-click restores/toggles the existing bar
- Tray / gear share Settings, About, Mute, Hide, and **Exit**
- **Exit** is the only normal way to stop polling

A second start of the installed or portable exe **activates** the existing instance. It does not start a second poller.

## Start with Windows

Settings → **General** → **Start with Windows**, or the installer checkbox, write the same HKCU Run value described under [Installation](#installation). The checkbox shows the **real** OS entry, not a preference file. No elevation.

## Notification sound / custom WAV / volume / mute

- Bundled original WAV (`notifier.wav`, short synthetic motif)
- Settings → **Notifications**: Default vs **Custom WAV**, volume **0–100%** (default 30%), Mute, **Test notification sound**, Restore default
- V1 accepts **WAV only** (uncompressed PCM). MP3/MP4 are intentionally unsupported
- A custom file is **copied** into `%LocalAppData%\CheckmkDesktopNotifier\assets\custom-notification.wav`. Deleting the original source file does not break playback
- Mute disables audio only; visual balloons still appear
- Test plays the selected sound at the configured volume and **bypasses Mute** so you can preview while muted
- Volume is in-process PCM scaling. The app does not change Windows master volume

## Security / Credential Manager

| Data | Location |
|------|----------|
| Non-secret GUI settings (URL, site, username, poll interval) | `%LocalAppData%\CheckmkDesktopNotifier\settings.json` |
| Automation secret | Windows Credential Manager, Generic Credential **`CheckmkDesktopNotifier`** (this Windows user) |
| Incidents / Seen | `%LocalAppData%\CheckmkDesktopNotifier\state\<connection-hash>\alert-state.json` |
| Mute / volume / Default vs Custom | `%LocalAppData%\CheckmkDesktopNotifier\preferences.json` |
| Imported custom WAV | `%LocalAppData%\CheckmkDesktopNotifier\assets\custom-notification.wav` |

- The secret is **not** stored in `settings.json` or `alert-state.json`
- Authorization headers are **not** persisted
- No Administrator rights are required
- Credential Manager is the OS secret store for this Windows user. It is **not** application-layer encryption and is not a substitute for a hardware token or enterprise secret vault

Reset configuration removes GUI settings and the stored secret. It does **not** delete incident/Seen files or notification preferences.

## Upgrade

Run a newer `CheckmkDesktopNotifier-Setup-x64.exe` over the existing per-user install.

- Replaces program files under `%LocalAppData%\Programs\CheckmkDesktopNotifier`
- Keeps user data under `%LocalAppData%\CheckmkDesktopNotifier`
- Does **not** remove the Credential Manager secret
- If the app is running, Setup asks you to **Exit** from the tray (no silent kill)

## Uninstall

Use Windows **Apps** / installed-app uninstall, or the uninstaller from the install folder.

- Removes binaries, Start Menu / desktop shortcuts, and the HKCU Run value
- **By default keeps** user data (settings, Seen, preferences, custom WAV)
- Optional prompt (default **No**) can delete the LocalAppData app folder and attempt `cmdkey /delete:CheckmkDesktopNotifier`

## Portable mode

Self-contained **win-x64** publish remains supported (`publish/win-x64/CheckmkDesktopNotifier.exe`).

| | Installed | Portable |
|---|-----------|----------|
| Typical use | Normal deployment | Testing, development, manual copy |
| Location | `%LocalAppData%\Programs\CheckmkDesktopNotifier` | Folder you publish to |
| Settings / Seen / secret | Same LocalAppData + Credential Manager | Same LocalAppData + Credential Manager |

Portable and installed builds share the per-user single-instance mutex and the same user-data directory for this Windows user.

## Building from source

Prerequisites: **.NET 8 SDK**.

The WPF app targets `net8.0-windows`. Linux CI can **compile** it (`EnableWindowsTargeting`); you still need Windows to **run** the UI.

```bash
dotnet build CheckmkDesktopNotifier.sln
dotnet test CheckmkDesktopNotifier.sln
```

Portable publish:

```bash
dotnet publish src/CheckmkDesktopNotifier.App/CheckmkDesktopNotifier.App.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=false \
  -o publish/win-x64
```

`publish/` is gitignored. Do not commit publish output.

## Building the installer

On **Windows**, with [Inno Setup 6](https://jrsoftware.org/isinfo.php) installed:

```powershell
powershell -File scripts/build-windows-package.ps1
```

Output (gitignored):

```text
artifacts\CheckmkDesktopNotifier-Setup-x64.exe
```

The script reads version **1.0.0** from `Directory.Build.props` and passes `/DMyAppVersion=1.0.0` to `iscc`. Equivalent:

```text
iscc /DMyAppVersion=1.0.0 installer\CheckmkDesktopNotifier.iss
```

SHA-256 of a built installer (do not invent a hash before the file exists):

```powershell
powershell -File scripts/hash-windows-installer.ps1
```

or:

```powershell
Get-FileHash .\artifacts\CheckmkDesktopNotifier-Setup-x64.exe -Algorithm SHA256
```

Inno Setup itself is **not** committed. Only `installer/CheckmkDesktopNotifier.iss` is source.

## Current limitations

These are **intentional V1 boundaries**, not accidental omissions:

- Windows only
- Checkmk-specific (REST collections as documented in `docs/CHECKMK_API.md`)
- Local Seen is **not** shared between administrators
- Checkmk ACK and downtime are **read-only**
- No ticketing / Zoho integration
- Custom alert sounds are **WAV-only**
- Notifications use tray **balloons**, not packaged Windows App SDK toasts
- Builds are **unsigned**; SmartScreen may warn
- Compact-bar position is not persisted across restart
- No in-app language switcher (UI follows Windows UI culture: English default, Polish when the UI culture is Polish)

## Roadmap

Not in V1. Do not expect these in 1.0.0:

**Team workflow / shared coordination**

- Take / ACK **in Checkmk**
- Show who acknowledged or took an incident
- Shared operational ownership
- Optional Checkmk acknowledgement comments

**Ticket workflow**

- Create / open ticket action
- Zoho Desk (or similar) API integration
- Shared ticket number / status

The first evaluation for shared work should be **Checkmk ACK + an existing ticket system**, not a custom shared database built into this notifier.

**Possible later improvements**

- Modern Windows toast notifications if the packaging / identity model changes
- Additional notification audio formats if there is a clear need

## License / attribution

- License: [MIT](LICENSE) — Copyright © 2026 TimeWizard007
- Application icon: original project placeholder (dark monitor + heartbeat). **No Checkmk logo** is bundled
- Default notification sound: original / generated WAV in-tree
- NuGet: CommunityToolkit.Mvvm and Microsoft.Extensions.* (MIT)
- Installer compiled with Inno Setup 6 (compiler not shipped in this repo)

Checkmk® is a trademark of Checkmk GmbH. This project is independent and uses the name only to describe compatibility.
