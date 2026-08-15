# Checkmk API

Target environment for verification:

- Edition: Checkmk CRE / RAW
- Version: `2.4.0p34`
- Site: `itssrv`
- REST base path: `/itssrv/check_mk/api/1.0/`

Do not invent endpoints. Prefer: (1) live verification, (2) the site’s OpenAPI/Swagger export, (3) general Checkmk docs last.

The interactive API is Checkmk REST-API **1.0** (OAS3). Collections return domain objects; the list is under **`value`**. Object fields belong in **`extensions`** per the documented envelope (confirm with a sanitized live payload before coding the mapper).

---

## VERIFIED

Live-tested against the real instance above.

### Service status (read)

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

This POST is a **read**. It must never be used to change Checkmk. Do not call acknowledge APIs.

### Host status collection exists (read)

```
GET /domain-types/host/collections/all
```

Returns a **monitoring** host collection under `value`.

This is **not** `host_config`.

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

### `state_type`

| Value | Meaning |
|------:|---------|
| 0 | SOFT |
| 1 | HARD |

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
| `scheduled_downtime_depth` | In downtime if `> 0` |
| `num_services_hard_crit` | Count for later grouping copy |

### Services table (unprefixed; matches verified POST names)

Also documents `last_time_warning` / `last_time_critical` / `last_time_unknown` and adjacent `host_*` columns such as `host_state`. Adjacent host columns on a **service** query can help grouping but do not replace a host collection (a DOWN host with no non-OK services would be missed).

### Auth (handbook / API intro)

Scripts should use an automation user:

`Authorization: Bearer <username> <automation_secret>`

Do not store that secret in this repository.

### Envelope

Domain object: `domainType`, `title`, `links`, `extensions`, …  
Collection adds `value: [ ... ]`.

---

## UNVERIFIED

Do **not** implement Phase 3 HTTP until these are confirmed on the real site.

- Whether **host GET** accepts `columns`
- Whether **host GET** accepts `query` (including server-side DOWN/UNREACH filter)
- Exact host GET item JSON (flattened vs `extensions`)
- Exact service POST item JSON path for each column (`value[].extensions.*` vs other)
- Unix seconds vs milliseconds (documented as Unix timestamps; still confirm on a payload)
- `POST /domain-types/host/collections/all` — **do not claim this exists** until verified. Phase 3 must use verified **GET** host collection unless a POST is later proven.
- Server-side `state_type = 1` on the service POST body
- Putting `host_state` in the verified service POST `columns` list
- Pagination / size limits
- Distributed-monitoring `site` column
- Least-privilege automation role that can **only** read these collections

---

## Application rules for any future client

- Read-only: GET host collection + POST service collection as verified.
- No acknowledge, downtime, comment, or config write endpoints on `ICheckmkClient`.
- Map into Core DTOs in the adapter, not in Core.
- Filter WARN/CRIT/UNKNOWN **server-side** for services.
- V1 engine uses HARD states only (filter in adapter or engine; server-side `state_type` not verified).
