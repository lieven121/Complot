# Complot

Garden planner for small-scale, circular, permaculture growing: sites and beds on a map, crop planning with
rotation per botanical family and companion checks, tasks with learning time estimates, a garden log, a plant
catalog with inventory, tools and costs. The UI is Dutch (Belgium) by default, with English as a second language.

See [PLAN.md](PLAN.md) for scope and decisions and `demo/index.html` for the clickable prototype.

## Structure

| Folder | Contents |
|---|---|
| `backend/` | .NET 10: `Complot.Domain` (entities and rules, no framework), `Complot.Api` (minimal APIs, EF Core + SQLite), tests |
| `frontend/` | Vue 3 + TypeScript with Vite+ (`vp`), PrimeVue 4 (MIT), vue-i18n, TanStack Query |
| `docker/` | Dockerfile (single image: API + built frontend) and docker-compose |

The backend is the single source of truth for rules and validation. On every backend build the OpenAPI document is
written to `frontend/src/shared/api/openapi.json` and NSwag generates the typed client `client.ts` from it, so a
change to an API contract shows up as a TypeScript error in the frontend. Validation errors are sent as codes with
parameters (`notEmpty`, `inclusiveBetween {from, to}`, `category.inUse`) and translated in the frontend.

## Requirements

- .NET SDK 10 (`backend/global.json`)
- Node.js 24 LTS (`.nvmrc`; 22.18+ also works) and Vite+: `npm install --global vite-plus@1.4.0`
  (Vite+ installs the pnpm version pinned in `frontend/package.json`)

## Run locally

```bash
# API on http://localhost:5180, fills an empty database with the demo garden (Development settings)
cd backend/src/Complot.Api
dotnet run

# Frontend on http://localhost:5173, proxies /api and /auth to the API
cd frontend
vp install
vp dev
```

API reference (Development only): http://localhost:5180/scalar

## Checks

```bash
cd backend && dotnet test          # domain rules + API integration tests
cd frontend && vp run verify       # vue-tsc (templates too), format + lint, unit tests, license check
```

## Configuration

| Setting (env var) | Default | Meaning |
|---|---|---|
| `Storage__DataDir` | `app-data` (`/data` in Docker) | SQLite database and uploaded photos |
| `Seed__DemoData` | `true` in Development | Fill an empty database with the demo catalog and garden |
| `Auth__Oidc__Authority`, `Auth__Oidc__ClientId`, `Auth__Oidc__ClientSecret` | empty | OpenID Connect sign-in; empty = open (internal use) |
| `Auth__Admins` | empty | With sign-in: comma-separated e-mail addresses that are always admin |
| `Google__ClientId`, `Google__ClientSecret` | empty | Google OAuth client for linking people's Google Calendars (planner) |
| `TZ` | system | Time zone of the garden: planned times are local times there (Docker: `Europe/Brussels`) |

### Users and access

Without OIDC everybody has full access. With OIDC, the addresses in `Auth__Admins` are admins. On the **Beheer** page
admins create, archive and delete locations, invite users by e-mail and give them *view* or *edit* access per
location (the invitation is linked at the first sign-in). Anyone else who signs in sees "waiting for access" and shows
up on that page. Changing shared data (catalog, people) needs edit access to at least one location. An archived
location is hidden and read-only; only an archived location can be deleted, which removes everything on it.

### Planner and calendars

Drag tasks onto a person's week (or tap a task, then a time). Scheduled tasks can reach people's calendars in two ways:

- **iCal link** (no setup): in the planner, choose a person and *Agendalink maken*. Subscribe to that URL in Google
  Calendar (*Other calendars → From URL*) or any calendar app. Read-only, and Google refreshes it only every few hours.
- **Google Calendar** (instant, two-way free/busy): each person links their own Google account in the planner. Complot
  then writes their scheduled tasks as events (marked "free", so they do not block other appointments) and shows their
  busy times in the planner. Set it up once:
  1. In [Google Cloud Console](https://console.cloud.google.com/) create a project, enable the **Google Calendar API**,
     and configure the OAuth consent screen (*External*; scopes `calendar.events` and `calendar.freebusy`).
  2. Create an OAuth client of type **Web application** with redirect URI
     `https://<your-host>/api/planner/google/callback` (locally: `http://localhost:5180/api/planner/google/callback`).
  3. Set `Google__ClientId` and `Google__ClientSecret`.

  While the consent screen is in *Testing*, only listed test users can link, and Google expires the link after 7 days.
  Publishing the app (Google shows an "unverified app" warning for a small private app) avoids the weekly re-linking.

The encryption keys for sign-in cookies and Google tokens are kept in the data directory (`keys/`), so keep that
directory with the database in backups.

**Access links** (Beheer → Toegangslinks) give access without an account, e.g. for volunteers: choose the rights per
location and a "valid until" date, and share the link (it is shown once; only a hash is stored). Opening it signs the
browser in until that date; deleting the link or making a new one ends that access immediately.

## Docker

```bash
cp docker/.env.example docker/.env
docker compose -f docker/docker-compose.yml up -d --build   # http://localhost:8080
```
