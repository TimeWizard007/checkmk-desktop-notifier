# Checkmk Desktop Notifier 1.0.0

Independent open-source Windows companion for Checkmk. Not affiliated with Checkmk GmbH.

Windows 11 validated (Phases 4A–4D and the 1.0.0 installer/About dialog).

## Highlights

- Checkmk REST monitoring of **service** and **host** problems (HARD states)
- Background **polling** with persisted open incidents
- Local **NEW / Seen** (per Windows user; not shared; not a Checkmk ACK)
- Compact Always-on-Top bar, expandable list, clickable counters, severity filters
- System **tray**, hide-to-tray, English / Polish UI
- Balloon **notifications**, bundled WAV, optional custom WAV, volume, mute
- **HOST DOWN / UNREACHABLE** notification grouping (the list still shows every incident)
- GUI Settings; automation secret in **Windows Credential Manager**
- **Start with Windows** (per-user HKCU Run, shared with the installer)
- Per-user **Inno Setup** installer (no Administrator rights)
- **Single-instance** (a second launch activates the existing process)

## Install

`CheckmkDesktopNotifier-Setup-x64.exe` → `%LocalAppData%\Programs\CheckmkDesktopNotifier`

SHA-256 (see also `SHA256SUMS.txt`):

```
71C5A97C461B513DF2B977F4FEC39C2E739E5817779EF9BA205C44EDEF847B2E
```

The installer is **unsigned**. SmartScreen may warn; verify the hash. Do not disable SmartScreen globally.

## Known limitations

- Windows only
- Local Seen is not shared between administrators
- Checkmk ACK is read-only in V1
- No ticket integration
- Custom alert sound is WAV-only
- Unsigned release may trigger SmartScreen
