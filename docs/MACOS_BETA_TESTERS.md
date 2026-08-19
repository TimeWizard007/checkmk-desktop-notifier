# macOS beta tester instructions (v1.3.0-beta.1)

This is a **pre-release**. It is not a stable macOS product. Windows v1.2.0 is unchanged.

## Which ZIP

| Mac | Download |
| --- | --- |
| Intel (x86_64) | `CheckmkDesktopNotifier-macOS-x64-v1.3.0-beta.1.zip` |
| Apple Silicon (arm64) | `CheckmkDesktopNotifier-macOS-arm64-v1.3.0-beta.1.zip` |

Verify SHA-256 against `SHA256SUMS-macOS-v1.3.0-beta.1.txt` before unzipping.

## Install / first launch

1. Unzip the archive. You should get `Checkmk Desktop Notifier.app` only.
2. Optional: move the `.app` to `/Applications`.
3. Open it. If Gatekeeper blocks it: **right-click → Open**, then confirm. Do **not** disable Gatekeeper, SIP, or other system security.
4. Configure Checkmk under Settings → Connection (`https://…` origin, site, automation user, secret, poll interval).
5. Test connection, then Save. The secret is stored in Keychain, not in JSON.

The app is a **menu-bar** utility. Closing the problem panel or Settings does not quit. Use **Quit**.

## Logs / crash reports

Per-user data (no secrets in the error log):

`~/Library/Application Support/CheckmkDesktopNotifier/`

- `settings.json` — connection fields without the automation secret
- `status-item-error.txt` — last-resort native/UI errors
- `state/` — local Seen / incident state
- `last-poll.txt`

If the app crashes, note macOS version, Intel vs Apple Silicon, whether the `.app` was launched (not a raw binary), and attach `status-item-error.txt` plus a sanitized crash report. Never send automation secrets, Keychain exports, or internal hostnames.

## Checklist

- [ ] Startup without crash; no extra Settings window when already configured
- [ ] Native menu-bar item; live NEW / CRIT / WARN / UNK / TAKEN counts
- [ ] Problem panel; filters ALL / NEW / CRIT / WARN / UNK / TAKEN; search
- [ ] Seen / Unseen stays local (does not change Checkmk)
- [ ] Open in Checkmk opens the matching host/service
- [ ] Take confirm → Taking… → Taken by after Checkmk read-back
- [ ] Generic ACK is not releasable; CDN Taken by can Release
- [ ] Settings General / Connection / Notifications
- [ ] Restart keeps Keychain secret (blank secret field on Save)
- [ ] Notifications (allow permission); no storm at startup
- [ ] Sound, mute, volume, test sound
- [ ] Start at Login enable/disable
- [ ] Second launch activates the existing instance (no second icon)
- [ ] Polling continues while windows are hidden; Quit exits
- [ ] Sleep / wake
- [ ] VPN disconnect / reconnect
- [ ] Leave running for several hours
