# Checkmk Desktop Notifier 1.3.0

Independent open-source Checkmk companion. Not affiliated with Checkmk GmbH.

This is the **first unified Windows + macOS release**. Product version **1.3.0** is shared by both platforms. Historical tags `v1.2.0` and `v1.3.0-beta.1` are unchanged.

Feature development for this cycle is **frozen**. The builds are handed to users for normal real-world testing.

## Windows

Windows 11 x64 (Windows 10 64-bit remains supported).

- Fresh per-user installer: `CheckmkDesktopNotifier-Setup-x64-v1.3.0.exe`
- The released **v1.2.0 feature set is preserved** (compact Always-on-Top bar, tray, filters, search, Seen/Unseen, Take / Taken by / Release, Open in Checkmk, balloon notifications, sound, Credential Manager, HKCU Run, single instance)
- Unified product version **1.3.0** (not a renamed v1.2.0 binary)

Windows code changes since v1.2.0 are the already-shipped M0 platform seams (Credential Manager, HKCU Run, URI launch, and user-data paths live in `Platform.Windows`). v1.3.0 does not redesign Windows UI or Checkmk behavior.

## macOS

First normal macOS release. Menu-bar application (`Checkmk Desktop Notifier.app`), not a clone of the Windows compact bar.

- Intel x64 (real-device validated)
- Apple Silicon arm64 (build/package validated; broader physical-device validation may continue after release)
- Live Checkmk HARD host/service polling and N/C/W/U/T counts
- Problem panel: ALL / NEW / CRIT / WARN / UNK / TAKEN filters and search
- Local Seen / Unseen
- Take / Taking… / Taken by / TAKEN / Release / Releasing… with Checkmk ACK read-back
- Generic/manual ACK is never removed by the notifier
- Open in Checkmk
- Settings: General, Connection, Notifications
- Automation secret in **Keychain**
- Native notifications (from the bundled `.app` only) and sound
- Start at Login via a per-user LaunchAgent that opens the `.app`
- Single instance

## Validation

- **Windows 11 x64:** real-machine smoke tested. FileVersion **1.3.0.0**, ProductVersion **1.3.0**.
- **macOS Intel x64:** real-machine DMG installation and application startup tested (drag to Applications, application icon, menu-bar start).
- **macOS Apple Silicon arm64:** build/package available; not yet validated on a physical Apple Silicon Mac.

## Installation

**Windows:** run `CheckmkDesktopNotifier-Setup-x64-v1.3.0.exe` as a normal user (no Administrator). Verify SHA-256 against `SHA256SUMS-v1.3.0.txt`.

**macOS:** open the architecture-matching DMG, drag `Checkmk Desktop Notifier.app` to Applications, eject the DMG, then launch from Applications.

- Intel: `CheckmkDesktopNotifier-macOS-x64-v1.3.0.dmg`
- Apple Silicon: `CheckmkDesktopNotifier-macOS-arm64-v1.3.0.dmg`

The macOS application is **unsigned and not notarized**. Gatekeeper may require a one-time **right-click → Open → Open**. Do **not** disable Gatekeeper, SIP, or other macOS security.

Windows binaries are **unsigned**. SmartScreen may show an unknown-publisher warning. Do **not** disable SmartScreen globally.

## Security

- Windows automation secret: Windows Credential Manager
- macOS automation secret: macOS Keychain
- Secrets are never stored in `settings.json`

## Known limitations

- Unsigned / not notarized
- Apple Silicon physical-device validation may continue after release
- Local Seen is per OS user, not shared between administrators
- Light-mode polish, sleep/wake, VPN reconnect, notification permission prompts, and long-running stability remain real-world follow-up (bug fixes only; no new feature phase)

## Source

`CheckmkDesktopNotifier-source-v1.3.0.zip` is produced from git tag `v1.3.0` (`git archive`), not from a dirty working tree.
