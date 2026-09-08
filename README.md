# Talmidon 🎓

**Talmidon** is a multi-tenant SaaS platform for private tutors — each tutor independently
manages her own students, lesson schedule, pedagogical notes, and payments, with a public,
login-free directory sitting on top so prospective students can discover tutors and reach out.

![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)
![Angular](https://img.shields.io/badge/Angular-21-DD0031?logo=angular&logoColor=white)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-17-4169E1?logo=postgresql&logoColor=white)
![PrimeNG](https://img.shields.io/badge/PrimeNG-custom%20preset-06B6D4)
![License](https://img.shields.io/badge/license-private-lightgrey)

---

## Overview

A tutor signs up on her own, adds her students and their parents, and gets full,
tenant-isolated control over her practice: scheduling, per-lesson billing, and
pedagogical tracking. Parents and students each get a scoped, read-mostly view of
exactly what concerns them — nothing more.

| Role | Access |
|---|---|
| **Visitor** (no login) | Public tutor library — browse tutors, filter by subject and city, view a profile, send a contact request |
| **Teacher** | Full control of her own students, calendar, notes, payments, reports, messages, and public profile |
| **Parent** | Their children's schedule (with lesson request / reschedule / cancel), visible notes, study materials, full payment status, and a message thread with the tutor |
| **Student** | Their own schedule, the notes explicitly shared with them, study materials, practice games, and a message thread — no payment visibility |
| **Admin** | A deliberately minimal platform-maintenance role: list tutors across tenants, lock/unlock an account, curate the subject suggestions, read site feedback |

## Features

### Teaching practice

- **Student management** — student cards, linked parents, login provisioning, profile photos
  (cropped and resized in the browser before upload), and a keyboard-driven quick search
  (`Ctrl/⌘+K`) over students and screens
- **Pedagogical notes** — per-note visibility toggles (student / parent), with a server-enforced
  rule that anything visible to the student is automatically visible to the parent
- **Lesson calendar** — a FullCalendar-based schedule: create / reschedule / delete lessons, mark
  a lesson complete with billing and homework, record a no-show, and approve or decline
  parent- and student-submitted requests
- **Recurring lessons** — a weekly series ("every Tuesday 16:00–17:00") that ends after N lessons,
  on a date, or never. The series is only a rule: it generates ordinary, independent lessons over
  a rolling 8-week horizon (a daily background job rolls it forward), so editing or cancelling one
  week never touches the rest
- **Jewish-calendar awareness** — series generation skips festivals, chol hamoed and festival eves
  by default (Israeli practice — one day of yom tov), using .NET's `HebrewCalendar` so leap years
  and Adar I/II are handled correctly. The skip reason is surfaced rather than silently applied
- **Personal calendar events** — non-lesson entries (a meeting, a blocked day, an all-day event)
  kept as their own entity so they never leak into billing, reports, or a parent's portal
- **Availability windows** — a weekly working-hours grid that highlights the calendar and warns
  when a lesson is scheduled outside it
- **Study materials** — a tutor attaches links (a practice sheet, an explainer video, a Drive
  folder) to a student; the student and their parents each get a materials screen of their own.
  Unlike a pedagogical note, a material is shared by definition and carries no visibility flags

### Money and reporting

- **Per-lesson billing** — no monthly subscriptions; a tutor marks a lesson billable on
  completion, batches open charges by parent into a payment, and the system emails a
  confirmation automatically
- **Payment reminders** — a monthly background job emails every parent with open charges,
  grouped by child; the same job is behind the tutor's manual "send now" button, scoped to her
  own tenant only
- **Lesson reminders** — an hourly job emails parents about lessons starting within 24 hours,
  marking each lesson so a reminder is never sent twice
- **Reports** — monthly income (lessons held, charged / paid / outstanding, broken down per
  student) and attendance (completed, cancelled, no-shows, hours, missed percentage), each
  exportable to CSV with a UTF-8 BOM so Excel opens Hebrew correctly

### Communication

- **Messages** — one threaded inbox for the tutor covering everything that used to be three
  separate things: a student reaching out, a reply to a note, and a message the tutor initiates.
  Students and parents each see only their own thread; unread state is tracked in both directions
- **Notification center** — lesson requests, change requests, contact requests and new messages,
  each with a deep link into the relevant screen
- **Contact requests** — enquiries arriving from the public library, with a handling status
  (new / handled / closed) and a pending-count badge
- **WhatsApp and email shortcuts** — `wa.me` links built from Israeli phone numbers, and prefilled
  `mailto:` links, so contacting a parent doesn't mean copying digits by hand

### Public surface

- **Tutor library** — a login-free page listing every opted-in tutor, filterable by subject and
  city, with a per-tutor profile page and a rate-limited contact form
- **Profile sharing** — a share dialog with a copyable link, a WhatsApp share, and a QR code
  (generated lazily, only when the dialog opens)
- **Site feedback** — a permanent "beta" badge opening a short idea/bug form, open to anonymous
  visitors, rate-limited, stored in the database and emailed to the admin

### AI and extras

- **AI lesson planner** *(optional)* — builds a lesson plan from subject, topic, duration, grade
  level and free-text notes. The call is made server-side so the API key never reaches the
  browser. Gemini or Anthropic, chosen by whichever key is configured; the whole feature hides
  itself when neither is. For Gemini the model name isn't hardcoded — the app asks the provider
  which models the key may actually use and picks one, because Gemini model names churn and
  free-tier access differs per key
- **Practice games** — three self-contained games in the student portal (quick arithmetic,
  English vocabulary matching, reading comprehension). The content is checked-in data, not
  model-generated: instant, free, offline-capable, and reviewed in advance
- **Light and dark mode** — follows the operating system by default, with a toggle in every
  shell; the choice is remembered and applied before first paint, so there is no white flash

## Tech Stack

| Layer | Technology |
|---|---|
| Backend | ASP.NET Core Web API (.NET 10, C#) |
| ORM | Entity Framework Core + Npgsql |
| Auth | ASP.NET Core Identity + JWT access tokens + rotating refresh tokens |
| Background jobs | Hangfire (PostgreSQL storage) — lesson reminders, payment reminders, series generation |
| Email | Brevo or SendGrid Web API in production; MailKit/SMTP → Mailpit locally |
| LLM *(optional)* | Gemini or Anthropic, server-side only |
| Frontend | Angular 21 + PrimeNG (custom preset over Aura, light/dark), FullCalendar, RTL Hebrew UI |
| Database | PostgreSQL |
| Local dev infra | Docker Compose — PostgreSQL + Mailpit (SMTP sandbox with a web inbox) |

The product is Hebrew and Israel-only by design: one timezone (`Asia/Jerusalem`, with UTC stored
in the database and local wall-clock time preserved across DST for recurring lessons), one
calendar, Israeli phone formats, and PrimeNG localized to Hebrew.

## Design System

The UI is not stock Aura. `frontend/src/app/core/theme/talmidon-preset.ts` defines the product's
whole visual language through PrimeNG's `definePreset`, and the rule in `styles.scss` is that no
component may hardcode a color:

- **Palette** — teal primary; warm `stone` surfaces in light mode (a "paper" feel that suits a
  learning product and sets off the cool teal), and a hand-tuned deep blue-charcoal ramp in dark
  mode rather than an automatic inversion.
- **Component tokens** — card, button, menubar, tag, dialog, datatable, toast, popover, menu and
  tooltip are tuned at the token level, so screens rarely need local CSS to override PrimeNG.
- **Custom tokens** — elevation, hero gradient, glass, and state tints are declared under the
  preset's `extend` block, which publishes them as `--p-talmidon-*` CSS variables that flip with
  the color scheme. This is what lets one stylesheet serve both themes.
- **Dark mode** — `ThemeService` toggles `.app-dark` on `<html>`, the same selector passed to
  `providePrimeNG`. A small inline script in `index.html` applies the stored choice before Angular
  boots, which is the only way to avoid a flash of light theme on load.
- **Shared primitives** — `shared/ui/` holds the pieces every screen reuses: `app-page-header`,
  `app-empty-state`, `app-stat-card`, `app-auth-layout`, the theme toggle, the user menu, the
  quick-search palette, the subject picker, the share-profile dialog and the beta-feedback badge —
  so every screen opens with the same hierarchy and every empty list says something useful
  instead of each screen inventing its own phrasing.
- **Motion** — staggered card entrances, scroll-reveal (`appReveal`), animated counters
  (`appCountUp`), a pointer-tracking spotlight (`appSpotlight`) and shimmer skeletons. Each is a
  small directive over `IntersectionObserver` / `requestAnimationFrame` rather than a dependency,
  and every one of them is inert under `prefers-reduced-motion`. Route changes use Angular's
  `withViewTransitions()`.
- **Public surface** — the login-free pages (library, tutor profile) and the four auth screens
  share one visual language: an animated aurora gradient over a dot grid, and, on auth, a
  branded split panel (`app-auth-layout`) that collapses to the form alone on small screens.

## Multi-Tenancy & Security

- **Tenant isolation, defense in depth:** an EF Core global query filter on every
  tenant-owned entity, `TenantId` enforcement inside `SaveChanges`, and composite
  `(Id, TenantId)` foreign keys at the database level — three independent layers, so a
  single missed filter can't leak data across tutors.
- **Auth:** short-lived JWT access tokens (15 min) + refresh tokens with rotation and
  **reuse detection** (a replayed refresh token revokes the entire token family).
- Mandatory email confirmation, account lockout after repeated failures, rate limiting,
  registration responses that don't leak whether an email already exists, and a
  fail-safe authorization default (`RequireAuthenticatedUser`) so a forgotten
  `[Authorize]` attribute fails closed, not open.
- **Deliberate exceptions, each explicit:** background jobs and the admin role have no tenant
  context, so they call `IgnoreQueryFilters()` on purpose and in named places; the anonymous
  contact form writes an explicit `TenantId`, which is exactly the case `EnforceTenantOnSave`
  permits.
- **Client-side validation mirrors every server-side rule** (`DataAnnotations` on the API
  DTOs ↔ Angular `Validators` in `core/forms/`) — the same password policy, max lengths,
  and cross-field checks (e.g. end time after start time) are enforced on both sides.

## Project Structure

```
Talmidon/
├── backend/
│   ├── Talmidon.Domain/          # Entities, enums, Jewish calendar, app timezone — no external deps
│   ├── Talmidon.Infrastructure/
│   │   ├── Data/                 # EF Core DbContext, migrations, tenant query filters
│   │   ├── Identity/             # ApplicationUser, roles
│   │   ├── Auth/                 # JWT + refresh token issuing and rotation
│   │   ├── Multitenancy/         # ICurrentTenant, tenant resolution and SaveChanges enforcement
│   │   ├── Email/                # IEmailSender: Brevo / SendGrid / SMTP, plus templates
│   │   ├── Scheduling/           # LessonSeriesGenerator — recurring-lesson expansion
│   │   ├── BackgroundJobs/       # Lesson reminders, payment reminders, series generation
│   │   └── Ai/                   # ILessonPlanner: Gemini / Anthropic + provider selection
│   ├── Talmidon.Api/             # Controllers, request/response contracts, Program.cs wiring
│   └── Talmidon.Tests/           # xUnit integration tests — tenant isolation, auth, IDOR, domain rules
├── frontend/
│   └── src/app/
│       ├── core/                 # Auth + guards, HTTP interceptors, forms, models, i18n, theme preset
│       ├── shared/               # UI primitives, calendar wrapper, avatars, CSV export, WhatsApp/mailto
│       └── features/
│           ├── auth/             # Login, registration, forgot/set password
│           ├── public/           # Login-free tutor library + public profile
│           ├── teacher/          # Teacher shell, profile, first-run setup, account settings
│           ├── dashboard/        # Teacher home
│           ├── students/         # Student list, student detail, parent linking
│           ├── parents/          # Parent models + service
│           ├── notes/            # Pedagogical notes
│           ├── lessons/          # Lesson calendar, series, requests, change requests
│           ├── payments/         # Open charges, payment history
│           ├── reports/          # Income and attendance reports + CSV export
│           ├── resources/        # Study materials — service + shared list, used by all three roles
│           ├── messages/         # Threaded messaging (teacher inbox + portal view)
│           ├── notifications/    # Notification center and bell
│           ├── contact-requests/ # Enquiries from the public library + the public contact form
│           ├── ai/               # Lesson planner screen
│           ├── admin/            # Platform admin: tutors, subject suggestions, site feedback
│           ├── parent-portal/    # Parent-facing schedule / notes / payments / materials / messages
│           └── student-portal/   # Student-facing schedule / notes / materials / messages / games
└── docs/                          # Requirements, database schema, screen designs (Hebrew)
```

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org/) 20+ and npm
- [Docker](https://www.docker.com/) (for PostgreSQL and the local mail sandbox)

### 1. Start local infrastructure

```bash
docker compose up -d
```

Brings up PostgreSQL on `5432` and [Mailpit](https://github.com/axllent/mailpit) — a local
SMTP server with a web inbox at **http://localhost:8025**, so you can read confirmation and
invitation emails without a real mail provider.

### 2. Configure the API

```bash
cp backend/Talmidon.Api/appsettings.Development.example.json \
   backend/Talmidon.Api/appsettings.Development.json
```

Fill in `Jwt:SecretKey` with 32+ random bytes (`openssl rand -base64 48`). The real
`appsettings.Development.json` is gitignored.

### 3. Run the backend

```bash
# Apply database migrations (first run, or after a schema change):
dotnet ef database update --project backend/Talmidon.Infrastructure --startup-project backend/Talmidon.Api

# Start the API:
dotnet run --project backend/Talmidon.Api
```

The API listens on **http://localhost:5208**. In development only, the Hangfire dashboard is
exposed at **http://localhost:5208/hangfire** — it authenticates via cookie rather than the JWT,
so it is never mapped outside development.

### 4. Run the frontend

```bash
cd frontend
npm install
npm start
```

The app is served at **http://localhost:4200**.

### 5. Sign up

Open http://localhost:4200, register as a teacher, and confirm the account via the link
in the confirmation email — check Mailpit at http://localhost:8025 instead of a real
inbox.

## Testing

```bash
docker compose up -d   # the tests need a real Postgres, same as local dev
dotnet test backend/Talmidon.Tests
```

The suite runs the real API in-process (`WebApplicationFactory`) against its own `talmidon_test`
database on the same PostgreSQL server — not mocks, not an in-memory provider — so the actual
EF Core global query filters and Npgsql behavior are what's under test. The database and its
schema are created automatically on first run. CI (`.github/workflows/ci.yml`) runs the same
suite against a Postgres service container, plus a production build of the frontend.

Coverage is aimed at the highest-blast-radius failure modes for a multi-tenant app, plus the
core domain rules that are easy to silently break in a refactor:

- **Isolation & authorization:** one tutor can never read, list, or modify another tutor's data
  (`TenantIsolationTests`); a parent or student can't reach a teacher-only endpoint
  (`RoleAuthorizationTests`); a parent can't act on a lesson belonging to a different parent's
  child under the same tutor (`ParentIdorTests`); two students under the same tutor never see
  each other's lessons or notes, and a student can't reach a teacher-only by-id endpoint at all,
  even for their own record (`StudentIdorTests`).
- **Auth lifecycle & tokens:** register → confirm → login → forgot-password → change-password
  end to end (`AuthFlowTests`); refresh-token rotation, and reuse of an already-rotated token
  revoking the entire token family (`RefreshTokenReuseTests`).
- **Lesson state machine:** every status-transition guard — updating/deleting/completing a
  lesson in the wrong state, approving/declining a request twice, a duplicate pending
  change-request — plus the actual effect of an approved cancel/reschedule on the lesson
  (`LessonStatusTransitionTests`); and a student's own lesson request arriving as `Requested`
  and waiting for approval rather than booking itself (`StudentLessonRequestTests`).
- **Recurring lessons:** creating a series produces exactly the occurrences its end condition
  implies, each an independent lesson; re-running the generator over the same horizon adds
  nothing (the bookmark holds); cancelling a series never touches lessons already completed and
  honours the "delete future occurrences" flag either way; and an occurrence keeps its local
  time of day across a DST change rather than drifting an hour (`LessonSeriesTests`). The
  Hebrew-calendar rules themselves, leap years and Adar II included, are tested separately
  (`JewishCalendarTests`), as is the DST-safe local↔UTC conversion they depend on
  (`AppTimeZoneTests`).
- **Note visibility:** the server-enforced rule that a note visible to the student is always
  visible to the parent too, on both create and update, and that each portal's endpoint only
  ever returns notes actually marked visible to it (`NoteVisibilityTests`).
- **Payments/billing:** the guards on marking lessons paid (already paid, not billable, or
  belonging to a different parent's child) and the full mark-paid → appears on the payment →
  disappears from open charges → delete reopens it cycle (`PaymentsTests`); the attendance
  report's counting and missed-percentage math (`AttendanceReportTests`).
- **Messaging:** a message reaches its thread and nobody else's, and unread state is correct in
  both directions — the badge that tells a tutor something is waiting depends on it
  (`MessagingTests`).
- **Calendar events:** full CRUD for the tutor, isolation between tutors, no exposure to a
  parent or student, rejection of an end before its start, and a multi-day event that began
  before the requested window still showing inside it — otherwise a week's holiday would vanish
  from the calendar on its second day (`CalendarEventTests`).
- **Admin & feedback:** the cross-tenant tutor list, lock/unlock through Identity's lockout, and
  the role being unreachable for teachers and anonymous callers (`AdminTests`); site feedback being open
  to anonymous senders and readable by the admin alone — the two properties that *are* the
  feature (`SiteFeedbackTests`).
- **Study materials:** a material reaches the student it was written for and that student's
  parents, and nobody else — not a second student of the same tutor, not a second parent under
  that tutor, and not another tutor at all (`StudentResourcesTests`). Creation is restricted to
  `http`/`https`, since the link is rendered as a clickable anchor inside someone else's portal.
- **AI provider selection:** which planner gets registered for a given combination of keys and
  explicit configuration — free tier before paid, so a paid key left configured "to try it"
  can't start billing quietly (`LessonPlannerSelectionTests`); Gemini model picking and request
  shaping (`GeminiModelChoiceTests`, `GeminiRequestTests`). These are pure unit tests — no
  network, no key needed.
- **Rate limiting:** the per-IP limit on `/api/auth/*` actually returns 429 once exceeded
  (`RateLimitingTests`) — this needs its own `WebApplicationFactory` with a small permit limit,
  since the shared test fixture intentionally inflates the limit for every other test.

## Configuration

Local development reads connection details from `backend/Talmidon.Api/appsettings.Development.json`
(see `appsettings.Development.example.json` for the expected shape). For any non-local
deployment, supply these via environment variables instead of committing secrets:

| Variable | Purpose |
|---|---|
| `ConnectionStrings__Default` | PostgreSQL connection string (also used for Hangfire's job storage) |
| `Jwt__SecretKey` | JWT signing key — **32+ bytes**, high-entropy |
| `App__ApiBaseUrl` | Public base URL of the API (used in generated email links) |
| `App__ClientUrl` | Public base URL of the Angular app (CORS + email links) |
| `Email__FromAddress`, `Email__FromName` | Sender identity on outgoing email — `FromAddress` must be on a domain verified with your email provider |
| `Brevo__ApiKey` | [Brevo](https://www.brevo.com) API key. Takes precedence when set |
| `SendGrid__ApiKey` | [SendGrid](https://sendgrid.com) API key, used when `Brevo__ApiKey` is empty |
| `Email__*` (Host/Port/UseSsl/Username/Password) | Only used as a fallback when neither API key is set — SMTP host, port, and credentials |
| `Admin__Email`, `Admin__Password` | Optional. When both are set, a single platform-admin account is seeded on startup |
| `Ai__Gemini__ApiKey` / `GEMINI_API_KEY` | Optional. Enables the AI lesson planner via Gemini |
| `Anthropic__ApiKey` / `ANTHROPIC_API_KEY` | Optional. Enables the AI lesson planner via Anthropic |
| `Ai__Provider` / `AI_PROVIDER` | Optional. `gemini`, `anthropic`, or `none` — an explicit choice overrides key-based detection |

### Production email (Brevo or SendGrid)

By default the app sends every email — confirmation, password reset, parent/student
invitations, lesson and payment reminders, and receipts — over SMTP to Mailpit, which only works
locally. To send real email in production, configure one provider:

1. Create a [Brevo](https://www.brevo.com) or [SendGrid](https://sendgrid.com) account and verify
   a sender: either a single sender address or, better, an entire domain. Email sent from an
   unverified `Email__FromAddress` will be rejected.
2. Create an API key with send permission.
3. Set `Brevo__ApiKey` **or** `SendGrid__ApiKey` to that key, and `Email__FromAddress` to the
   verified sender, in your production environment's variables — never commit the key to a
   config file.

`AddInfrastructure` (`backend/Talmidon.Infrastructure/DependencyInjection.cs`) registers
`BrevoEmailSender` when `Brevo__ApiKey` is set, otherwise `SendGridEmailSender` when
`SendGrid__ApiKey` is set, otherwise the Mailpit-facing `SmtpEmailSender`. Every call site uses
the same `IEmailSender` interface, so no other code changes when you switch providers.

### AI lesson planner (optional)

Set `Ai__Gemini__ApiKey` (Google AI Studio) or `Anthropic__ApiKey` and the "lesson plan" screen
appears for teachers; leave both empty and the frontend hides the feature entirely after a single
availability check. With both set, Gemini wins unless `Ai__Provider` says otherwise — the free
tier is the safer default for a key that might have been left configured by accident. The key is
only ever read server-side, so it never reaches the browser and a teacher's quota can't be burned
from a devtools console.

## Deploying to production (single VPS + Docker)

`docker-compose.prod.yml` runs the whole stack on one server: Postgres, the API, and Caddy
(serving the built Angular app and reverse-proxying `/api/*` to the API, with fully automatic
HTTPS via Let's Encrypt — no certbot, no manual certificates). This is a one-domain deployment;
tenants (teachers) are separated by a JWT claim, not by subdomain, so no per-tenant DNS is needed.

### 1. Buy a domain and point it at the server

Register a domain with any registrar (Cloudflare Registrar and Namecheap are both simple).
Once you have a server (next step) and its public IP, create an **A record** at your domain's
DNS pointing your chosen hostname (e.g. `app.example.co.il`) at that IP. DNS propagation can take
anywhere from a few minutes to a few hours.

### 2. Provision the server

Any VPS with Docker works. A cheap option: [Hetzner Cloud](https://www.hetzner.com/cloud) or
[DigitalOcean](https://www.digitalocean.com), Ubuntu 24.04 LTS, 2 GB RAM minimum (4 GB is more
comfortable). Then, over SSH:

```bash
# Install Docker + the Compose plugin (Ubuntu/Debian)
curl -fsSL https://get.docker.com | sh

# Open HTTP/HTTPS to the outside world (Caddy needs both — it issues certificates over 80/443)
ufw allow 80/tcp && ufw allow 443/tcp && ufw allow 22/tcp && ufw enable
```

### 3. Deploy the app

```bash
git clone <this repo's URL> talmidon && cd talmidon
cp .env.example .env
nano .env   # fill in DOMAIN, CLIENT_URL, API_BASE_URL, POSTGRES_PASSWORD, JWT_SECRET_KEY,
            # SENDGRID_API_KEY, EMAIL_FROM_ADDRESS (see the Configuration/email sections above)

docker compose -f docker-compose.prod.yml up -d --build
```

The API applies pending EF Core migrations automatically on startup (`MigrateDatabaseAsync` in
`Program.cs`), so there's no separate migration step. First boot: Caddy requests a certificate
for `DOMAIN` (needs the DNS record from step 1 to already resolve), Postgres initializes, and the
API seeds roles and — if `ADMIN_EMAIL`/`ADMIN_PASSWORD` are set — the one platform-admin account.
The recurring background jobs (hourly lesson reminders, daily series generation, monthly payment
reminders) register themselves with Hangfire on startup.

Check it came up clean:

```bash
docker compose -f docker-compose.prod.yml ps       # all three services healthy/running
docker compose -f docker-compose.prod.yml logs -f api   # watch for startup errors
```

Then visit `https://<DOMAIN>` in a browser.

### 4. Redeploying after changes

```bash
git pull
docker compose -f docker-compose.prod.yml up -d --build
```

This rebuilds only the images whose source changed and restarts those containers; Postgres data
persists in the `talmidon_pgdata` named volume regardless.

## Documentation

See [docs/](docs/) for the original requirements specification, database schema design,
and screen/wireframe planning (in Hebrew).

---

Created by **Yehudit Pollock**
