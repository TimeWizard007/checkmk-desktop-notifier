# Checkmk Desktop Notifier 1.3.0-beta.1

Independent open-source Checkmk companion. Not affiliated with Checkmk GmbH.

This is a **macOS beta / pre-release**. It is **not** a stable macOS product release.

Windows **v1.2.0** remains the released Windows build. Tag `v1.2.0` is unchanged.

## Highlights

First public macOS tester build. The macOS host reuses the same Core and Infrastructure as Windows v1.2.0:

- Native **menu-bar** application (`NSStatusItem`), not a Windows compact-bar clone
- Live Checkmk HARD host/service **polling**
- Problem panel with ALL / NEW / CRIT / WARN / UNK / TAKEN filters and search
- Local **Seen / Unseen** (per macOS user)
- **Open in Checkmk** in the default browser
- Optional **Take / Taken by / Release** through Checkmk acknowledgement (same REST contract as Windows)
- Settings: General, Connection, Notifications
- Automation secret in **macOS Keychain** (never in `settings.json`)
- **Start at Login** via a per-user LaunchAgent that opens the `.app`
- Native macOS **notifications** (from the bundled `.app` only)
- Alert **sound** (bundled WAV, optional custom WAV, volume, mute)
- **Intel x64** (real-device validated) and **Apple Silicon arm64** (cross-published; not yet real-device validated)

## Install

Use the architecture-matching ZIP:

- Intel Mac: `CheckmkDesktopNotifier-macOS-x64-v1.3.0-beta.1.zip`
- Apple Silicon: `CheckmkDesktopNotifier-macOS-arm64-v1.3.0-beta.1.zip`

Unzip and run `Checkmk Desktop Notifier.app`. Verify SHA-256 against [SHA256SUMS-macOS-v1.3.0-beta.1.txt](../SHA256SUMS-macOS-v1.3.0-beta.1.txt).

Tester steps: [docs/MACOS_BETA_TESTERS.md](MACOS_BETA_TESTERS.md).

The binaries are **unsigned and not notarized**. Gatekeeper may require a one-time Open. Do not disable macOS security globally.

## Known limitations

- Beta / pre-release — not a final macOS release
- Unsigned / not notarized
- Gatekeeper may require manual approval
- Native notification delivery and permission prompts still need broader real-world coverage
- Apple Silicon is published but not yet validated on a real device
- No final DMG/PKG installer
- Sleep/wake, VPN reconnect, and long-running stability still under test
- Light-mode polish may vary across Macs
- Seen remains local to the current macOS user
- SMAppService login items are not used; Start at Login is a LaunchAgent

## Windows

Windows users should continue to use **v1.2.0**. This beta does not replace the Windows installer.
