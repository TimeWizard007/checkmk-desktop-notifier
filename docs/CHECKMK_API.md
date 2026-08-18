# Checkmk API

Target environment for verification (sanitized):

- Edition: Checkmk CRE / RAW
- Version: `2.4.0p34`
- Site: use the Checkmk **site name** (path segment). The live verification used a corporate site; that name is not published here.
- REST base path: `/{site}/check_mk/api/1.0/`

Do not invent endpoints. Prefer: (1) live verification, (2) the site’s OpenAPI/Swagger export, (3) general Checkmk docs last.

The interactive API is Checkmk REST-API **1.0** (OAS3). Collections return domain objects; the list is under **`value`**. Object fields belong in **`extensions`**.

---

## VERIFIED

Live-tested against the real instance above.

### Service status (read) — Phase 3A uses this

```
POST /domain-types/service/collections/all
```

- HTTP `200`
- `Accept: application/json`
- `Content-Type: application/json`
- JSON **body** with `columns` (array) and `query` (Checkmk filter DSL)
- Server-side filtering works (do not download all services)
- `OR` of `state` `1`, `2`, `3` returns WARN + CRIT + UNKNOWN
- Collection is under `value`
- Column values are under `value[].extensions`
- Time columns are mapped as Unix **seconds** (`DateTimeOffset.FromUnixTimeSeconds`); `0` means absent

Verified request shape:

```json
{
  "columns": [
    "host_name",
    "description",
    "state",
    "state_type",
    "plugin_output",
    "last_state_change",
    "last_hard_state_change",
    "last_time_ok",
    "acknowledged",
    "acknowledgement_type",
    "comments_with_extra_info",
    "scheduled_downtime_depth"
  ],
  "query": {
    "op": "or",
    "expr": [
      { "op": "=", "left": "state", "right": "1" },
      { "op": "=", "left": "state", "right": "2" },
      { "op": "=", "left": "state", "right": "3" }
    ]
  }
}
```

Verified service `state`:

| Value | Meaning |
|------:|---------|
| 0 | OK |
| 1 | WARN |
| 2 | CRIT |
| 3 | UNKNOWN |

Verified `state_type`:

| Value | Meaning |
|------:|---------|
| 0 | SOFT |
| 1 | HARD |

This POST is a **read**. It must never be used to change Checkmk. Acknowledge writes use a separate client and the endpoints below.

Phase 6A extra read columns (live-validated on RAW 2.4.0p34):

| Column | Notes |
|--------|--------|
| `acknowledged` | 0/1 |
| `acknowledgement_type` | 1 = normal, 2 = sticky |
| `comments_with_extra_info` | Array of positional tuples `[id, author, comment, entry_type, entry_time]` (not objects). `entry_type` 4 = acknowledgement. Author is the automation account (live: `checkmk-desktop-notifier`), never Taken-by. Live truncated write was `"Taken by Michał"` (generic ACK). Full CDN write is one line: `Taken by {name} via Checkmk Desktop Notifier cdn.v1 take name="..."`. |

Auth (verified for this adapter):

Scripts should use an automation user. The header scheme is `Bearer` plus username and automation secret (do not store that secret in this repository).

Verified working automation account (Phase 3A, Windows 11 over VPN):

- Role: **Normal monitoring user**
- Contact group: **Everything**
- No Administrator privileges required

A narrower read-only role was not tested.

### Phase 3A live connection test (Windows 11 + VPN)

One-shot `POST /domain-types/service/collections/all` from `CheckmkDesktopNotifier.ConnectionTest`, mapped into Core `ProblemSnapshot`. No secret, Authorization header, or plugin output recorded.

```
HTTP status: 200
Service problems: 129
WARN: 15
CRIT: 111
UNKNOWN: 3
```

### Host status collection (read) — Phase 3B complete

```
GET /domain-types/host/collections/all
```

This is **not** `host_config`. There is **no** host POST.

Live-tested from Windows 11 over VPN against CRE/RAW `2.4.0p34`:

Unfiltered GET (no query string, no body):

- HTTP `200`
- `domainType`: `"host"`
- Collection under `value[]`
- Identity: `value[].extensions.name`
- Host objects: **263**
- Only `name` present; monitoring columns are **absent**

GET with documented repeated `columns=` query-string parameters (no JSON body):

- HTTP `200`
- Host objects: **263**
- UP: **262**, DOWN: **1**, UNREACHABLE: **0**
- All expected monitoring columns present under `extensions`
- Identity: `extensions.name`

Application host monitoring uses the **`columns=` GET**, not the name-only GET.

Verified host `state`:

| Value | Meaning | Core mapping |
|------:|---------|--------------|
| 0 | UP | not a problem |
| 1 | DOWN | Critical |
| 2 | UNREACHABLE | Unknown |

Verified `state_type`: 0 SOFT, 1 HARD. V1 incidents use HARD only.

Host recurrence marker: `last_time_up`.

ACK (`acknowledged`) and downtime (`scheduled_downtime_depth`) are read-only metadata.

Do **not** implement `POST /domain-types/host/collections/all`. Do **not** use `host_config`.

### Not monitoring

`/domain-types/host_config/collections/all` is configuration. **Never** use it as live UP/DOWN/UNREACH status.

---

## DOCUMENTED

From the site REST-API 1.0 export (Swagger UI / table definitions), not all of it live-tested.

### Query DSL (Monitoring)

Binary node: `{ "op": <livestatus operator>, "left": <column>, "right": <value> }`  
Negation: `{ "op": "not", "expr": <filter> }`  
Combination: `{ "op": "and"|"or", "expr": [ ... ] }`

Time columns are Unix timestamps.

### Hosts table (unprefixed columns)

| Column | Notes |
|--------|--------|
| `name` | Host name (not `host_name`) |
| `state` | 0 UP, 1 DOWN, 2 UNREACHABLE |
| `state_type` | 0 SOFT, 1 HARD |
| `plugin_output` | Last check output |
| `last_state_change` | Unix time |
| `last_hard_state_change` | Unix time |
| `last_time_up` | Recurrence marker analog for hosts |
| `last_time_down` | Unix time |
| `last_time_unreachable` | Unix time |
| `acknowledged` | 0/1 |
| `acknowledgement_type` | 1 = normal, 2 = sticky (Phase 6A) |
| `comments_with_extra_info` | Same tuple format as services (Phase 6A) |
| `scheduled_downtime_depth` | In downtime if `> 0` |
| `num_services_hard_crit` | REST count; **not** mapped onto Core `MonitoredProblem`. V1 grouping uses merged-snapshot service problems |
| `num_services_hard_warn` | same |
| `num_services_hard_unknown` | same |

### Services table (unprefixed; matches verified POST names)

Also documents `last_time_warning` / `last_time_critical` / `last_time_unknown` and adjacent `host_*` columns such as `host_state`. Adjacent host columns on a **service** query can help grouping but do not replace a host collection (a DOWN host with no non-OK services would be missed).

### Envelope

Domain object: `domainType`, `title`, `links`, `extensions`, …  
Collection adds `value: [ ... ]`.

---

## VERIFIED — acknowledge writes (Phase 6A)

Live-tested against Checkmk RAW 2.4.0p34. Service ACK:

```
POST /domain-types/acknowledge/collections/service
```

Validated payload (HTTP **204** No Content). The problem stayed CRITICAL and visible. ACK was visible in the Checkmk GUI. Account permission: `action.acknowledge`.

Checkmk RAW 2.4.0p34 stores acknowledgement comments as **one line**. A `\n` comment is truncated to the first line (live GO-S11: REST returned only `"Taken by Michał"`). The write comment is therefore a single line that still contains the human text, the via-phrase, and `cdn.v1 take name="..."`.

```json
{
  "sticky": true,
  "persistent": false,
  "notify": false,
  "comment": "Taken by <DisplayName> via Checkmk Desktop Notifier cdn.v1 take name=\"<DisplayName>\"",
  "acknowledge_type": "service",
  "host_name": "HOST",
  "service_description": "SERVICE"
}
```

Host ACK (same options; host only, no child services):

```
POST /domain-types/acknowledge/collections/host
```

```json
{
  "sticky": true,
  "persistent": false,
  "notify": false,
  "comment": "...cdn.v1 take name=\"...\"",
  "acknowledge_type": "host",
  "host_name": "HOST"
}
```

Do **not** send `expire_on` (HTTP 400 on this RAW instance). Taken-by identity is the `cdn.v1 take name="..."` machine line. Never use the Checkmk comment author.

### Acknowledge delete / Release (Phase 7A) — live-validated

Live-tested against Checkmk RAW 2.4.0p34.

```
POST /domain-types/acknowledge/actions/delete/invoke
```

Validated service payload (HTTP **204** No Content). After delete, the Checkmk GUI no longer showed the acknowledgement.

```json
{
  "acknowledge_type": "service",
  "host_name": "HOST",
  "service_description": "SERVICE"
}
```

Host (same endpoint; no `service_description`):

```json
{
  "acknowledge_type": "host",
  "host_name": "HOST"
}
```

No comment id. No credentials/secrets in the payload. The notifier Releases **CDN Takes only**; it must never send this delete for a generic/manual ACK.

Read-back: a later successful poll with `acknowledged = 0` and `acknowledgement_type = 0` (comments empty or leftover) must clear `IsTakenByNotifier` / `TakenByDisplayName`. Checkmk remains source of truth.

### Open in Checkmk (Phase 6B) — GUI, not REST show

REST `urn:com.checkmk:rels/show` hrefs such as `/api/1.0/objects/host/{host}/actions/show_service/invoke?service_description=...` are API invoke endpoints. They are **not** browser GUI URLs and are not used.

The notifier opens:

```
{BaseUrl}/{site}/check_mk/index.py?start_url=view.py%3Fview_name%3Dhost%26host%3D...
{BaseUrl}/{site}/check_mk/index.py?start_url=view.py%3Fview_name%3Dservice%26host%3D...%26service%3D...
```

Host/service names are URL-encoded. BaseUrl is the configured origin only. No credentials, automation secret, or Authorization in the URL.

---

## UNVERIFIED

- Whether **host GET** accepts a `query` filter (including server-side DOWN/UNREACH). Not sent; the client currently maps HARD non-UP hosts after fetching the `columns=` collection.
- Server-side `state_type = 1` on the service POST body (V1 still filters HARD in Core)
- Putting `host_state` in the verified service POST `columns` list
- Pagination / size limits
- Distributed-monitoring `site` column
- Least-privilege automation role that can **only** read these collections (Normal monitoring user + Everything contact group is verified to work; a narrower role is untested)
- `POST /domain-types/host/collections/all` — **does not exist in this project’s verified contract**. Do not implement it.

---

## Application rules for the client

- Phase 3A (complete): POST service collection, as verified.
- Phase 3B (complete): GET host collection with repeated `columns=` query parameters; map HARD DOWN/UNREACHABLE into Core; do not guess a host POST.
- Phase 6A (COMPLETE / Windows-tested): optional Take writes via `ICheckmkAcknowledgementClient`, not `ICheckmkClient`. Sticky ACK, single-line CDN comment (RAW 2.4 truncates `\n`), no `expire_on`.
- Phase 6B (COMPLETE / Windows-tested): Open in Checkmk uses the GUI `index.py?start_url=view.py` builder, not REST `show` links.
- Phase 7A (COMPLETE / Windows-tested): optional Release of CDN Takes via `POST /domain-types/acknowledge/actions/delete/invoke`. Never delete generic/manual ACK. ACK fields refresh on every successful snapshot.
- Map into Core DTOs in Infrastructure, not in Core. Persist only normalized ACK fields, never raw comments/REST JSON.
- Filter WARN/CRIT/UNKNOWN **server-side** for services.
- V1 engine uses HARD states only (filter in the engine; host adapter also supplies HARD-only host problems).
- Local Seen remains completely separate from Checkmk ACK.
