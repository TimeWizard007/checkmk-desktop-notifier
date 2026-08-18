# Checkmk Desktop Notifier 1.2.0

Independent open-source Windows companion for Checkmk. Not affiliated with Checkmk GmbH.

Windows 10/11 64-bit supported. Windows 11 validated (Phase 6A Take / shared ACK, Phase 6B Open in Checkmk + Seen/Unseen, and Phase 7A Release / Untake). Verified against Checkmk CRE / RAW 2.4.0p34.

v1.2.0 is the **consolidated public-release candidate**. It contains the completed team workflow and UX improvements. Tag `v1.1.0` exists and was **not** published as a GitHub Release.

## Highlights

On top of 1.0.0 monitoring, tray, sound, autostart, and the per-user installer, and the 1.1.0 Take / Open in Checkmk / Seen-Unseen work:

- Optional **Team Take** — sticky Checkmk acknowledgement (`sticky=true`, `persistent=false`, `notify=false`)
- **Taken by &lt;display name&gt;** for notifier-created CDN Takes; generic/manual Checkmk ACK stays **ACK**
- Checkmk is the **source of truth**; Taken state synchronizes across notifier instances after polling
- **TAKEN** counter and filter; live **search** by host, service, and Taken-by name (composes with filters)
- **Release / Untake** of CDN Takes via `POST /domain-types/acknowledge/actions/delete/invoke`
- Release is offered only for CDN Takes. Any administrator using the notifier may release a CDN Take. Generic ACK never exposes Release
- Before delete, current Checkmk state is refreshed/validated. No optimistic local Released state; the UI waits for Checkmk read-back
- Release does **not** resolve the Checkmk problem. Severity stays CRIT/WARN/UNKNOWN until Checkmk reports recovery. Checkmk notifications may resume
- Local **Seen / Unseen** remains per Windows user and never writes Checkmk. NEW remains local
- **Open in Checkmk** — default browser, corresponding host or service GUI view (not a REST API resource)
- Dark in-app Take and Release confirmation
- Successful Take/Release uses row waiting states (`Taking...` / `Releasing...`) instead of native white Windows MessageBoxes
- Take and Release do not emit a notifier balloon or sound by themselves

CDN Take comments remain **single-line** because Checkmk RAW 2.4 stores ACK comments as one line.

## Install

Build the Windows installer from 1.2.0 source (see `scripts/build-windows-package.ps1`). Record the 1.2.0 installer SHA-256 after that Windows build; do not reuse the 1.0.0 checksum in `SHA256SUMS.txt`.

The installer is **unsigned**. SmartScreen may warn.

## Known limitations

- Windows only (Windows 10/11 64-bit)
- Seen / Unseen is local per Windows user and is not shared
- Generic/manual Checkmk ACK cannot be released from the notifier
- Release does not resolve the Checkmk problem itself
- No ticketing / Zoho integration
- No custom shared backend/database
- CDN Take comments must remain single-line on Checkmk RAW 2.4
- Unsigned installer may trigger SmartScreen
