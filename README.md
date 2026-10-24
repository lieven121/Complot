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

### Users and access

Without OIDC everybody has full access. With OIDC, the addresses in `Auth__Admins` are admins. On the **Beheer** page
admins create, archive and delete locations, invite users by e-mail and give them *view* or *edit* access per
location (the invitation is linked at the first sign-in). Anyone else who signs in sees "waiting for access" and shows
up on that page. Changing shared data (catalog, people) needs edit access to at least one location. An archived
location is hidden and read-only; only an archived location can be deleted, which removes everything on it.

**Access links** (Beheer → Toegangslinks) give access without an account, e.g. for volunteers: choose the rights per
location and a "valid until" date, and share the link (it is shown once; only a hash is stored). Opening it signs the
browser in until that date; deleting the link or making a new one ends that access immediately.

## Docker

```bash
cp docker/.env.example docker/.env
docker compose -f docker/docker-compose.yml up -d --build   # http://localhost:8080
```
