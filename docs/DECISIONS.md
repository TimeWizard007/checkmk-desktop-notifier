# Decisions

Record why, not only what. Change a decision here when the product changes.

## Local Seen is not Checkmk ACK

Clicking the eye means: this Windows user has seen this **local** uninterrupted incident; do not notify again for it.

It must never call Checkmk acknowledge APIs. `acknowledged` from Checkmk is read-only metadata (badge / future filter). ACK must not look like or set Seen.

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

Real mode persists open incidents, Seen, recurrence markers, and `LastSuccessfulPollUtc` to `%LocalAppData%/CheckmkDesktopNotifier/alert-state.json`. Mock mode stays in-memory.

Secrets, Authorization headers, and Checkmk URL/credentials are **not** stored in that file. They remain in `checkmk.local.json` / environment variables.

A diagnostic `last-poll.txt` in the same folder records success/failure and host/service **counts** only.

## No Windows Service in V1

Normal per-user desktop app, tray/bar later. No admin-required service.

## Open-source MIT intent

The repo should become MIT-licensed (LICENSE file is Phase 5). No hardcoded sites, users, or secrets.

## No Checkmk logos or trademarks bundled

Do not ship Checkmk brand assets. The compact bar title “Checkmk” is a plain text label for the product being monitored, not a logo.

## Technology (V1)

C# / .NET 8 / WPF / MVVM / Windows 10/11 / DI / async + `CancellationToken` / local JSON state / minimal NuGet (CommunityToolkit.Mvvm, Microsoft.Extensions.Hosting).

## REST vs Livestatus

V1 HTTP path is the verified REST status collections. Livestatus remains a possible later `ICheckmkClient` adapter, not required for V1.

## Host HTTP method

Until proven otherwise, host monitoring is **GET** `/domain-types/host/collections/all`. Do not invent a host POST.

## UI localization

`en` + `pl` from the start. No user-visible strings hardcoded in ViewModels when a resource key exists. English remains the source language.

## Phase 2 window owner

`Window.Owner` may be set only after the owner window has been shown (valid HWND). Compact bar is the main Always-on-Top window; the problem list becomes its owned window after `CompactBarWindow.Show()`.
