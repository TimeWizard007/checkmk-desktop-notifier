# Checkmk Desktop Notifier 1.1.0

Independent open-source Windows companion for Checkmk. Not affiliated with Checkmk GmbH.

Windows 11 validated (Phase 6A Take / shared ACK and Phase 6B Open in Checkmk + Seen/Unseen). Feature freeze: no further v1.1.0 functionality.

## Highlights

On top of 1.0.0 monitoring, tray, sound, autostart, and the per-user installer:

- Optional **Team Take** — sticky Checkmk ACK (`sticky=true`, `persistent=false`, `notify=false`)
- **Taken by &lt;display name&gt;** for CDN Takes; generic/manual Checkmk ACK stays **ACK**
- Checkmk is the **source of truth**; Taken state synchronizes across notifier instances
- **TAKEN** counter and filter; live **search** by host, service, and Taken-by name
- Dark in-app Take confirmation
- Already-acknowledged NEW incidents stay locally NEW but raise **no balloon and no sound**
- ACK-aware HOST DOWN / UNREACHABLE notification grouping
- Optional Take; read-only / 403 accounts keep monitoring
- **Open in Checkmk** — default browser, corresponding host or service GUI view
- Reversible local **Seen / Unseen** (Unseen returns the incident to NEW; no notification replay)

CDN Take comments are **single-line** because Checkmk RAW 2.4 stores ACK comments as one line.

## Install

Build the Windows installer from tagged v1.1.0 source (see `scripts/build-windows-package.ps1`). The 1.1.0 installer SHA-256 is recorded after that Windows build; do not reuse the 1.0.0 checksum.

The installer is **unsigned**. SmartScreen may warn.

## Known limitations

- Windows only
- Seen / Unseen is local per Windows user and is not shared
- No Untake / Release in 1.1.0; generic/manual ACK cannot be removed by the notifier
- No ticketing / Zoho integration
- No custom shared backend/database
- CDN Take comments must remain single-line on Checkmk RAW 2.4
- Unsigned installer may trigger SmartScreen
