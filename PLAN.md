# Complot — plan

> A garden planner for small-scale, circular, permaculture growing: sites, plots and beds on a zoomable map with a
> time scrubber, crop planning with rotation and companion checks, tasks and a planner linked to people's calendars,
> a plant catalog with inventory, tools and materials, harvest logging, and cost tracking.

Status: **stages 1–5 built** (foundation, catalog, sites & designer, plantings, tasks & log). Next: stage 6 (planner).
The clickable prototype from stage 0 is still in `demo/index.html`.

---

## 1. Decisions (from feedback)

| # | Topic | Decision |
|---|---|---|
| D1 | Language | Whole application in **Dutch (nl-BE)**, built with **i18n from day one** (vue-i18n, ICU messages). English as second locale. User data (plant names, notes) is not translated. Dates, numbers and currency follow the locale (`23 okt 2026`, `€ 12,50`). |
| D2 | Sites | **Multiple sites** (e.g. home + allotment). Everything on the map belongs to one site; tasks, planner, costs, tools and the catalog are shared across sites and show the site where relevant. |
| D3 | Shapes | Beds and plots: **rectangle, round/oval, keyhole bed** in v1; **free polygon** (vertex editing) in a later stage. Exact dimensions are always editable as numbers (m). |
| D4 | Harvest logging | **Yes** — harvest entries (kg / pieces / bunches) via the log, with totals per bed, crop, variety and year. |
| D5 | Invoices | Split **by hand**, plus an **AI prompt + JSON import** (same pattern as the plant catalog): copy the prompt, give the AI the PDF/photo, paste the JSON back, check, save. |
| D6 | Crop rotation | Checked per **botanical family** (Solanaceae, Brassicaceae, …), rest years per family. The category gives a default family, and a variety can override it. Green manures and herbs can be exempt per category. |
| D7 | Tools | Tracked as **quantities** with a condition split (good / repair / worn). |
| D8 | Tech | **.NET 10 backend + Vue/Vite+ frontend**, client generated with **NSwag**; priority: readable, maintainable, DRY, best practices (§4). |
| D9 | Tasks | Check-off **todo list that works on mobile**; tasks are generated from plantings, trees/perennials and tool maintenance, plus manual tasks. |
| D10 | Notes | **Log per bed / tree / crop** (observation, disease, pest, harvest, soil, weather), optional photo, author, date. Disease/pest shows on the map. |
| D11 | Designer | Map editor with **exact dimensions**, plus **paths, trees/shrubs, features** (compost, water, pond, hedge, shed). Trees and shrubs have **yearly actions** that become tasks. |
| D12 | Planner | **Drag tasks onto people's (Google) calendars.** Every task has an **estimated duration**; the actual time can be entered and **estimates learn** from it. |

## 2. Remaining assumptions

| # | Assumption |
|---|---|
| A1 | Metric units, EUR, climate Belgium/Netherlands (last frost ~mid May) as default; climate settings per site. |
| A2 | A planting can take **a share of a bed** (intercropping / succession). Mixed plantings inside a bed are split along its long side. |
| A3 | Sowing/harvest windows are stored as **month ranges** (may wrap the year); concrete dates are calculated per planting and can be overridden; actual dates are recorded separately. |
| A4 | Companion planting rules (good/bad neighbours) stay on the **category**; "neighbour" = same bed or a bed within 0.6 m in the same plot. |
| A5 | Users: a small team (household + helpers + volunteers). **People ≠ users**: a person can be assigned tasks without having a login (e.g. "Vrijwilligers"). |
| A6 | No offline mode in v1 (responsive + fast on mobile). A PWA with offline task check-off is a later option. |

---

## 3. Feature scope

### 3.1 Garden map (main screen)
- Site switcher; zoomable/pannable map (wheel, pinch, drag, fit), scale bar, north arrow.
- **Timeline scrubber** across the previous, current and next years: per season, today, play.
- Beds are coloured by what is in them on the selected date and in which phase (young / growing / harvest / finishing, pre-sown indoors 🏠, planned = dashed). Warnings ❗ and disease/pest 🦠 markers.
- **Bed panel**: info (size, area, sun, soil, neighbours, harvest this year), current crops, **multi-year Gantt**, plan a planting (dates and plant count calculated from the catalog, live checks, number of tasks it creates), tasks, **log with notes/photos/harvest**, rotation status per family, plantings, tools on the bed.
- **Tree panel**: crown, planting date, age, yearly actions, tasks, log, harvest.
- **Overview panel**: beds in use, tasks for the next 14 days (check off directly), harvest now, harvested this year, checks.

### 3.2 Designer (on the map)
- Toggle design mode → grid (1 m / 5 m), drag to move (10 cm snap), **exact X/Y/width/length** fields, dimension lines and coordinates on the selected item.
- Item types: **plot** (rect/oval, type open/greenhouse/orchard), **bed** (rect/oval/keyhole, plot, sun, soil), **path** (material: wood chips, grass, pavers, gravel), **tree/shrub** (crown diameter, cultivar, planting date, **yearly actions with month and duration → tasks**), **feature** (compost, rain water, pond, hedge, shed).
- Later: polygons, rotation, copy/paste, layers, background image (aerial photo/plan) for tracing, shade cast by trees affecting the sun setting of beds.

### 3.3 Tasks (todo)
- Sources: plantings (indoor sowing, sowing/planting out, start of harvest, clearing; green manure = mow/dig in), perennial/tree actions (yearly), tool maintenance (interval), manual tasks.
- Groups: **Overdue / Today / This week / Next week / Later**, filter per person or unassigned, show completed tasks.
- **Check off** with a big touch target → "how long did it take?" (as estimated / ±15 / custom / skip).
- Task detail: assignee, estimate (editable), learned correction, actual time, completed, calendar slot, add a note to the bed.
- **Workload per person this week.**
- **Estimates that learn**: estimate = base (per task type: fixed or per m²) × correction factor learned from completed tasks (Σ actual / Σ base). Later: per person and per variety, ignoring outliers (median).

### 3.4 Planner
- Week calendar (07:00–21:00) per person or for everyone; list of tasks still to schedule (due within the window).
- **Drag & drop** (desktop) or **tap the task, then tap the slot** (mobile); block height = estimate; snap 15 min.
- Shows the person's **busy times from Google Calendar** (free/busy) and warns about overlaps.
- Scheduled tasks are **written to the person's Google Calendar** (event with a link back to the task); moving or removing a task updates or deletes the event.
- **.ics export / subscription feed** per person for people without Google.

### 3.5 Plants (catalog, inventory)
- Categories (icon, colour, default family, good/bad neighbour categories, rotation exemption).
- Botanical families with rest years (Dutch + Latin names).
- Varieties: all metadata (depth, spacing in row / between rows, height, sun, windows for indoor sowing / outdoor sowing / planting out / harvest, weeks indoors, germination, days to harvest, harvest duration, annual/perennial, max years in bed, family override, tool needs, **yearly actions for perennials**, suppliers with price & unit incl. own seed), experiences and harvest totals from the log, where it has been grown.
- Inventory lots (qty, source, bought, best before → expired / use soon), **shopping list** for next season.
- **AI prompt template + JSON import** (validated preview: new / update / error) and full JSON export/import.

### 3.6 Tools & materials
- Quantity + condition split, material and reuse flag ♻️, purchase date & price, lifespan, assigned beds, maintenance interval + log (→ tasks).
- **Peak demand per season from the plan** vs. usable stock, maintenance due, replacement due.

### 3.7 Costs
- Invoices with attachment (PDF/photo), supplier, date, number, total, **split lines per cost category** (must add up to the total).
- **AI help**: prompt (with the allowed categories) + paste JSON → fills in the form.
- Totals per year / quarter / month (stacked chart), per category, category × year, year-to-date vs. last year, average per month, cost per m² of bed.
- Later: link invoice lines to inventory lots and tools (purchase price automatically), budgets, CSV export.

---

## 4. Architecture

```
┌──────────────────────── Docker image (single container) ─────────────────────────┐
│  ASP.NET Core 10 (minimal APIs)                                                   │
│   ├─ /api/*        REST + OpenAPI 3.0 (→ NSwag TypeScript client)                  │
│   ├─ /auth/*       OIDC (only when Auth__Oidc__* is set, otherwise open)           │
│   └─ /*            built Vue SPA (deep links fall back to index.html)              │
│  EF Core 10 + SQLite (WAL), migrations applied on startup     (/data volume)       │
└───────────────────────────────────────────────────────────────────────────────────┘
```

| Layer | Choice |
|---|---|
| Backend | .NET 10 minimal APIs, feature folders (`Catalog`, `Garden`, `Plantings`, `Work`); `Complot.Domain` holds entities and all rules, without framework dependencies |
| Validation | FluentValidation; errors go out as **codes + params** (`inclusiveBetween {from, to}`) and are translated in the frontend |
| DB | EF Core 10 + SQLite (WAL), Guid v7 keys, complex types for value objects, JSON columns for owned lists |
| API contract | OpenAPI document generated at build → `frontend/src/shared/api/openapi.json` → **NSwag** `client.ts`. A contract change shows up as a TypeScript error |
| Frontend | Vue 3 + TypeScript with **Vite+** (`vp`: dev, build, fmt, lint, test), Pinia, Vue Router, **vue-i18n** (nl-BE default, en), TanStack Query |
| UI | **PrimeVue 4.5.5, pinned** (v5 needs a commercial licence), own design tokens; mobile-first task list |
| Map / designer | Own SVG with pan/zoom; shapes `rect` / `ellipse` / `keyhole` (later `polygon`); designer works on a draft with undo/redo |
| Auth | Optional OIDC (cookie + OpenIdConnect); open mode when unconfigured |
| Quality | xUnit v3 (domain rules + API integration tests on a temp SQLite file), Vitest via Vite+, `vue-tsc`, licence check on npm packages (`vp run verify`) |
| Ops | Multi-stage Dockerfile (Node 24 + Vite+ → .NET SDK → aspnet alpine), docker-compose, healthcheck, `/data` volume |

### 4.1 Code principles
- **Rules live once, in `Complot.Domain`** (schedule, phases, rotation/neighbour/sun checks, task generation, estimates). The frontend only displays their results.
- **Generated rather than stored**: tasks are derived from plantings, trees and manual tasks and have a stable key (`planting:{id}:harvest:2026`), so status, assignee and actual time survive re-generation.
- One importer for the AI/JSON catalog exchange; preview = the real import inside a rolled-back transaction. The demo seed uses the same path.
- Feature-sliced folders on both sides; small composables and shared UI (`EditDialog` + `useEditor`, `FormField`, `NumberInput`, …).
- i18n: no hard-coded UI strings; rule output is `{ code, params }`.

---

## 5. Data model (v1)

```
site(id, name, climate_zone, last_frost_doy, timezone)
plot(id, site_id, name, type[open|greenhouse|orchard], shape_json, notes)
bed(id, site_id, plot_id, code, shape_json, sun[full|half|shade], soil, notes, active_from, active_to)
path(id, site_id, name, material, shape_json)
map_feature(id, site_id, type[compost|water|pond|hedge|shed|other], name, shape_json)
tree(id, site_id, name, cultivar, kind[tree|shrub], x, y, crown_m, planted_on, variety_id?, removed_on)
tree_action(id, tree_id, title, month, est_minutes)
  -- shape_json: {type:'rect',x,y,w,h} | {type:'ellipse',...} | {type:'keyhole',...} | {type:'polygon',points}

plant_family(id, latin, name_nl, name_en, rotation_rest_years)
plant_category(id, name, icon, color, default_family_id, rotation_exempt)
category_companion(category_id, other_category_id, relation[good|bad], note)
plant_variety(id, category_id, family_id?, name, cultivar, latin, life[annual|biennial|perennial],
              sow_depth_cm, spacing_row_cm, spacing_between_rows_cm, height_cm, sun_json,
              sow_indoor_from_m, sow_indoor_to_m, weeks_indoor, sow_outdoor_from_m, sow_outdoor_to_m,
              plant_out_from_m, plant_out_to_m, germination_min_d, germination_max_d,
              days_to_harvest, harvest_weeks, harvest_from_m, harvest_to_m, max_years_in_bed, notes)
variety_action(id, variety_id, title, month, est_minutes)            -- yearly care for perennials
variety_tool_need(variety_id, tool_id, per[plant|bed], qty)
supplier(id, name, url, notes)
variety_supplier(variety_id, supplier_id, price, unit, url)
inventory_lot(id, variety_id, supplier_id?, source, qty, unit, bought_on, best_before)

planting(id, bed_id, variety_id, share, status[planned|active|done|failed],
         sow_on, plant_out_on, harvest_from, harvest_to, clear_on,                    -- planned (calculated, editable)
         actual_sow_on, actual_plant_out_on, actual_harvest_from, actual_clear_on, notes)

note(id, site_id, bed_id?, tree_id?, planting_id?, person_id, date, tag[obs|disease|pest|harvest|soil|weather],
     text, harvest_qty?, harvest_unit?)
note_photo(id, note_id, path, width, height)

person(id, name, color, user_id?, google_calendar_id?, google_token_ref?, ical_token)
task_type(id, key, name_key, base_minutes, per[fixed|m2|plant], learned_factor, samples)
task(id, source_key UNIQUE, type_id, title, site_id, bed_id?, tree_id?, tool_id?, planting_id?,
     due_on, est_minutes_override?, assignee_id?, status[open|done|skipped], done_at, actual_minutes,
     scheduled_start, scheduled_person_id, calendar_event_id, notes)

tool(id, name, kind, icon, qty, unit, qty_good, qty_repair, qty_worn, material, is_reused,
     bought_on, price, lifespan_years, maintenance_interval_months, maintenance_minutes, notes)
tool_assignment(tool_id, bed_id, qty, from_on, to_on)
tool_log(id, tool_id, date, text, cost)

cost_category(id, name, color)
invoice(id, site_id?, date, supplier_id, number, total, attachment_path, notes)
invoice_line(id, invoice_id, description, cost_category_id, amount, inventory_lot_id?, tool_id?)

user(id, oidc_sub, name, email)       audit(id, user_id, entity, entity_id, action, at, diff_json)
```

**Rules in `Complot.Domain`**: planting dates from metadata · phase at a date · rotation per family (rest years, exemptions) · companions (same bed and adjacent beds) · sun mismatch · max stay for perennials · sowing window check · plant count from area and spacing · tool demand (peak per month) · task generation · estimate = base × learned factor.

---

## 6. Google Calendar integration

1. Each person can **link a Google account** (OAuth consent, offline access; tokens encrypted at rest). This is separate from the OIDC login, so volunteers without a login can still be linked by an admin.
2. **Read**: `freebusy.query` for the visible week → grey "busy" blocks in the planner (only busy/free, no details).
3. **Write**: scheduling a task → `events.insert` in the person's calendar (title, location = bed/site, description with a link to the task, duration = estimate). Moving it → `events.patch`; unscheduling → `events.delete`. Store `calendar_event_id`.
4. **Fallback**: a per-person secret **iCal feed URL** (`/ical/:token`) that any calendar app can subscribe to (read-only, refreshes with a delay).
5. Later: a webhook (push notifications) to detect when the person moves or deletes the event in Google.

---

## 7. Roadmap

| Stage | Content | Status |
|---|---|---|
| **0** | Plan + demo (this) — approve UX & scope | done |
| **1 Foundation** | Solution layout, Docker, SQLite + migrations, domain project, i18n setup (nl-BE/en), auth switch (OIDC/none), app shell (desktop sidebar / mobile bottom bar), seed data, CI | done, except CI |
| **2 Catalog** | Families, categories, varieties (all metadata + actions), suppliers, inventory, shopping list, AI prompt/import, JSON export/import | done, except the shopping list |
| **3 Sites & designer** | Sites, plots, beds (rect/oval/keyhole), paths, trees + actions, features; designer with exact dimensions; map + timeline | done |
| **4 Plantings** | Plan plantings, date calculation, Gantt, phases on the map, rotation/companion/sun/stay checks, actual dates | done |
| **5 Tasks & log** | Task generation + manual tasks, mobile todo with check-off & actual time, learning estimates, notes with photos & harvest quantities | done |
| **5b Admin** | Users (OIDC + invitations), view/edit access per location, archive/delete locations, admin page | done |
| **6 Planner** | Week planner (DnD + tap), people, Google OAuth, free/busy, event sync, iCal feed; access links for volunteers | done |
| **7 Tools & costs** | Tools + maintenance tasks + demand; invoices with attachments, manual split + AI import, reports | done |
| **8 Polish** | Polygons, backups UI, full export/import, accessibility, Playwright e2e (mobile), PWA option | next |
| Later | Weather/frost alerts, per-person/variety estimate learning, tree shade on beds, yield statistics, seed-saving tracking, budgets |  |

---

## 8. Open questions (smaller, can be answered during stage 1)

1. Are the people who do the work on **personal Gmail** or **Google Workspace** accounts? Workspace allows domain-wide setup and makes OAuth simpler.
2. Should volunteers be able to **check off tasks without logging in** (e.g. a personal link per person), or does everyone get an account?
3. **Notifications**: do you want reminders (e-mail / push) for overdue tasks or frost warnings in v1, or later?
4. Should **photos** be resized/compressed on upload? Assumed yes: max 2000 px, originals not kept.
5. Do **costs** belong to a site (to compare the home garden with the allotment), or are they always global? Assumed: optional site per invoice.
