# Cluster Solution

A multi-module .NET solution that backs a lobby visitor-log system. A small
embedded device (clustered as `cluster-nano`) hosts a kiosk web server on the
LAN, captures each visitor's details, and forwards them as JSON to this
solution, which stores them for a CRM-style grid in an authenticated web app.

## What it does

1. A visitor fills in a form on a tablet kiosk served by the `cluster-nano`
   firmware.
2. The firmware parses the form, escapes it into JSON, and POSTs it to this
   solution at `POST /api/visits`.
3. This solution accepts, validates and persists the visit.
4. Authenticated staff browse, search and sort the visits in a CRM-style grid.

```
kiosk browser ──POST /visit──▶ cluster-nano (lwIP httpd)
                                      │  queue + retry
                                      ▼
                          POST /api/visits (JSON)
                                      │
                                      ▼
                    Cluster host (Blazor + minimal APIs)
                                      │
                                      ▼
                          Postgres (Supabase)
                                      │
                                      ▼
                   CRM-style "Visits" grid (authenticated)
```

The forwarded payload (produced by `cluster-nano`, fields are visitor-controlled
and stored as-is):

```json
{"visitor_name":"Ana Ruiz","visitor_type":"courier","unit":"B12",
 "tenant_id":"T-99","answered":true,"notes":"left at door"}
```

## Solution layout

```
src/
  Cluster.Domain/   net8.0 class library  — domain model (no framework deps)
  Cluster.Identity/ headless web app      — auth service (login/register/logout)
  Cluster.View/     Blazor Web App        — interactive UI (the planned single host)
```

| Project | Role |
| --- | --- |
| `Cluster.Domain` | Domain model. Currently `Visit`, mirroring the firmware payload. |
| `Cluster.Identity` | Headless ASP.NET Core service owning authentication: `/identity/*` HTML forms and `/api/identity/*` JSON. Issues the auth cookie. SQLite via EF Core today. |
| `Cluster.View` | Blazor Web App serving the UI. Reads the auth cookie through a shared data-protection key ring (`.keys/`). Hosts the `/visits` grid. |

## Domain model

`Cluster.Domain.Visit` is the persisted visitor log entry:

| Property | Notes |
| --- | --- |
| `Id` | `Guid`, generated client-side by the accepting service. |
| `VisitorName` | Free text from the form. |
| `VisitorType` | `guest`, `courier`, `delivery`, `contractor`, … defaults to `unknown`. |
| `Unit` | Destination unit/room. |
| `TenantId` | Tenant identifier (e.g. `T-99`). |
| `Answered` | Whether the visitor was received. |
| `Notes` | Optional free text. |
| `ReceivedAt` | `DateTimeOffset` set when the service accepted the visit. |

## Visits grid

`Cluster.View` exposes `/visits`, an interactive (server-rendered) CRM-style
grid built over `Cluster.Domain.Visit`:

- summary stats (total, answered, unique units),
- live text search across visitor, unit, tenant and notes,
- click-to-sort columns,
- type badges, an answered/no-answer pill, avatar initials and local time.

Data is currently served from a sample set in the page (`LoadVisitsAsync` with a
`TODO`) until the persistence layer is wired up.

## Planned implementation

The target is a single ASP.NET Core host (the Blazor app) with the domain and
data concerns extracted into libraries, backed by Supabase Postgres.

### Target layout

```
src/
  Cluster.Domain/   class library  — entities + interfaces. No EF.
  Cluster.Data/     class library  — ApplicationDbContext (IdentityDbContext),
                                     entity configs, Npgsql provider, migrations.
  Cluster.View/     Blazor Web App — the single host: UI, identity endpoints,
                                     and /api/visits.
```

`Cluster.Identity` dissolves: its web endpoints move into the `Cluster.View`
host, and its EF Core/identity data pieces move into the new `Cluster.Data`
library (which references `Cluster.Domain`).

### Steps

1. **`Cluster.Domain`** — keep the domain model free of framework dependencies
   (done; `Visit` exists).
2. **`Cluster.Data`** — move `ApplicationDbContext`, `ApplicationUser` and the
   migrations here; add `Npgsql.EntityFrameworkCore.PostgreSQL`; add the `Visit`
   entity set and configuration.
3. **Collapse into `Cluster.View`** — move the identity endpoints, HTML helpers
   and form-token service into the host; register Identity and
   `AddDbContext(UseNpgsql(...))`; simplify away the cross-app key-ring and
   return-URL plumbing now that there is one host.
4. **Device endpoint** — `MapPost("/api/visits")` binds the firmware payload,
   maps it to a `Visit`, persists it and returns `200`. Any non-2xx makes the
   firmware retry, so the endpoint must answer `2xx` on success. It must be
   exempt from `UseHttpsRedirection` (the firmware speaks plain HTTP and treats a
   redirect as a failure) and reachable on the LAN at the address configured in
   the firmware (`VISIT_FORWARD_HOST` / `VISIT_FORWARD_PORT`, default
   `192.168.1.50:5000`).
5. **Wire the grid to data** — replace the sample rows in `Visits.razor` with a
   `Cluster.Data`-backed source.
6. **Verify** — `dotnet build cluster-sln.sln`; apply migrations against
   Supabase; POST a firmware-shaped payload to `/api/visits` and expect `200`;
   run the firmware's host test harness (`cluster-nano/test/run.sh`).

### Database

The solution targets **Supabase (hosted PostgreSQL)** through EF Core's Npgsql
provider:

- Swap `UseSqlite(...)` for `UseNpgsql(connectionString)`.
- `IdentityDbContext` works unchanged on Postgres, but the existing SQLite
  migration (`TEXT`/`INTEGER` columns, `Sqlite:Autoincrement`) must be replaced
  with regenerated Npgsql migrations.
- Supabase requires SSL (`SSL Mode=Require`). Use the session pooler for
  migrations and the transaction pooler at runtime (with prepared statements
  disabled for Npgsql under transaction pooling).
- Keep the connection string in user-secrets or
  `appsettings.Development.json`, never in source control.

## Configuration

| Setting | Where | Meaning |
| --- | --- | --- |
| `ConnectionStrings:DefaultConnection` | user-secrets / `appsettings.Development.json` | Postgres (Supabase) connection string. |
| `ViewClient:BaseUrl` | `Cluster.Identity` | Root of `Cluster.View`, used to build return URLs. |
| `IdentityClient:BaseUrl` / `ViewBaseUrl` | `Cluster.View` | Roots of the identity and view apps. |

Shared pieces: both web hosts persist a data-protection key ring to `.keys/`
(git-ignored) under the application name `Cluster.Identity`, so the auth cookie
issued by the identity service can be read by the Blazor app. A rename of that
application name must be applied to both hosts together.

## Build and run

```sh
dotnet build cluster-sln.sln

# Blazor UI (http profile)
dotnet run --project src/Cluster.View/Cluster.View.csproj --launch-profile http

# Identity service
dotnet run --project src/Cluster.Identity/Cluster.Identity.csproj --launch-profile http
```

## Related repository

`cluster-nano` — the embedded firmware (FreeRTOS + lwIP, C++14) that serves the
kiosk form and forwards visits to `POST /api/visits`. See its README for the
firmware-side configuration and the host test harness.
