# Talmidon 🎓

**Talmidon** is a multi-tenant SaaS platform for private tutors — each tutor independently
manages her own students, lesson schedule, pedagogical notes, and payments, with a public,
login-free directory sitting on top so prospective students can discover tutors and reach out.

![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)
![Angular](https://img.shields.io/badge/Angular-21-DD0031?logo=angular&logoColor=white)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-17-4169E1?logo=postgresql&logoColor=white)
![PrimeNG](https://img.shields.io/badge/PrimeNG-Aura-06B6D4)
![License](https://img.shields.io/badge/license-private-lightgrey)

---

## Overview

A tutor signs up on her own, adds her students and their parents, and gets full,
tenant-isolated control over her practice: scheduling, per-lesson billing, and
pedagogical tracking. Parents and students each get a scoped, read-mostly view of
exactly what concerns them — nothing more.

| Role | Access |
|---|---|
| **Visitor** (no login) | Public tutor directory — browse tutors, subjects, and contact info |
| **Teacher** | Full control of her own students, calendar, notes, payments, and public profile |
| **Parent** | Their children's schedule (with lesson request / reschedule / cancel), visible notes, and full payment status |
| **Student** | Their own schedule and the notes explicitly shared with them — no payment visibility |

## Features

- **Student management** — student cards, linked parents, login provisioning
- **Pedagogical notes** — per-note visibility toggles (student / parent), with a server-enforced
  rule that anything visible to the student is automatically visible to the parent
- **Lesson calendar** — create / reschedule / delete lessons, mark a lesson complete with
  billing and homework, and approve or decline parent-submitted requests
- **Per-lesson billing** — no monthly subscriptions; a tutor marks a lesson billable on
  completion, batches open charges by parent into a payment, and the system emails a
  confirmation automatically
- **Teacher profile** — price per lesson, cancellation policy, contact info, and subject
  list, all optionally published to the public directory
- **Public directory** — a login-free page listing every opted-in tutor, filterable by subject

## Tech Stack

| Layer | Technology |
|---|---|
| Backend | ASP.NET Core Web API (.NET 10, C#) |
| ORM | Entity Framework Core |
| Auth | ASP.NET Core Identity + JWT access tokens + rotating refresh tokens |
| Frontend | Angular 21 + PrimeNG (Aura theme), RTL Hebrew UI |
| Database | PostgreSQL |
| Local dev infra | Docker Compose — PostgreSQL + Mailpit (SMTP sandbox with a web inbox) |

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
- **Client-side validation mirrors every server-side rule** (`DataAnnotations` on the API
  DTOs ↔ Angular `Validators` in `core/forms/`) — the same password policy, max lengths,
  and cross-field checks (e.g. end time after start time) are enforced on both sides.

## Project Structure

```
Talmidon/
├── backend/
│   ├── Talmidon.Domain/          # Entities, enums — no external dependencies
│   ├── Talmidon.Infrastructure/  # EF Core DbContext, Identity, tokens, email, tenant isolation
│   ├── Talmidon.Api/             # Web API controllers, JWT auth, request/response contracts
│   └── Talmidon.Tests/           # xUnit integration tests — tenant isolation, auth, IDOR
├── frontend/
│   └── src/app/
│       ├── core/                 # Auth, HTTP interceptors, shared form-validation helpers
│       └── features/
│           ├── auth/             # Login, registration
│           ├── public/           # Login-free tutor directory
│           ├── teacher/          # Teacher app shell + profile settings
│           ├── students/         # Student list, student detail, parent linking
│           ├── notes/            # Pedagogical notes
│           ├── lessons/          # Lesson calendar, requests, change requests
│           ├── payments/         # Open charges, payment history
│           ├── parent-portal/    # Parent-facing schedule / notes / payments
│           └── student-portal/   # Student-facing schedule / notes (read-only)
└── docs/                         # Requirements, database schema, screen designs
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

### 2. Run the backend

```bash
# Apply database migrations (first run, or after a schema change):
dotnet ef database update --project backend/Talmidon.Infrastructure --startup-project backend/Talmidon.Api

# Start the API:
dotnet run --project backend/Talmidon.Api
```

The API listens on **http://localhost:5208**.

### 3. Run the frontend

```bash
cd frontend
npm install
npm start
```

The app is served at **http://localhost:4200**.

### 4. Sign up

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
schema are created automatically on first run.

Coverage is aimed at the highest-blast-radius failure modes for a multi-tenant app, plus the
core domain rules that are easy to silently break in a refactor:

- **Isolation & authorization:** one tutor can never read, list, or modify another tutor's data
  (`TenantIsolationTests`); a parent or student can't reach a teacher-only endpoint
  (`RoleAuthorizationTests`); a parent can't act on a lesson belonging to a different parent's
  child under the same tutor (`ParentIdorTests`).
- **Auth lifecycle & tokens:** register → confirm → login → forgot-password → change-password
  end to end (`AuthFlowTests`); refresh-token rotation, and reuse of an already-rotated token
  revoking the entire token family (`RefreshTokenReuseTests`).
- **Lesson state machine:** every status-transition guard — updating/deleting/completing a
  lesson in the wrong state, approving/declining a request twice, a duplicate pending
  change-request — plus the actual effect of an approved cancel/reschedule on the lesson
  (`LessonStatusTransitionTests`).
- **Note visibility:** the server-enforced rule that a note visible to the student is always
  visible to the parent too, on both create and update, and that each portal's endpoint only
  ever returns notes actually marked visible to it (`NoteVisibilityTests`).
- **Payments/billing:** the guards on marking lessons paid (already paid, not billable, or
  belonging to a different parent's child) and the full mark-paid → appears on the payment →
  disappears from open charges → delete reopens it cycle (`PaymentsTests`).
- **Rate limiting:** the per-IP limit on `/api/auth/*` actually returns 429 once exceeded
  (`RateLimitingTests`) — this needs its own `WebApplicationFactory` with a small permit limit,
  since the shared test fixture intentionally inflates the limit for every other test.
- **Student IDOR:** two students under the same tutor never see each other's lessons or notes
  through their own-schedule/own-notes endpoints, and a student can't reach a teacher-only
  by-id endpoint at all, even for their own record (`StudentIdorTests`).

## Configuration

Local development reads connection details from `backend/Talmidon.Api/appsettings.Development.json`
(see `appsettings.Development.example.json` for the expected shape). For any non-local
deployment, supply these via environment variables instead of committing secrets:

| Variable | Purpose |
|---|---|
| `ConnectionStrings__Default` | PostgreSQL connection string |
| `Jwt__SecretKey` | JWT signing key — **32+ bytes**, high-entropy |
| `App__ApiBaseUrl` | Public base URL of the API (used in generated email links) |
| `App__ClientUrl` | Public base URL of the Angular app |
| `Email__FromAddress`, `Email__FromName` | Sender identity on outgoing email — `FromAddress` must be on a domain verified with your email provider |
| `SendGrid__ApiKey` | SendGrid API key. When set, email sends via the [SendGrid](https://sendgrid.com) Web API instead of SMTP/Mailpit — see below |
| `Email__*` (Host/Port/UseSsl/Username/Password) | Only used as a fallback when `SendGrid__ApiKey` is unset — SMTP host, port, and credentials |

### Production email (SendGrid)

By default the app sends every email — confirmation, password reset, parent/student
invitations, payment reminders and receipts — over SMTP to Mailpit, which only works
locally. To send real email in production:

1. Create a [SendGrid](https://sendgrid.com) account and verify a sender: either a single
   sender address or, better, an entire domain (Settings → Sender Authentication). Emails
   sent from an unverified `Email__FromAddress` will be rejected.
2. Create an API key with **Mail Send** permission (Settings → API Keys).
3. Set `SendGrid__ApiKey` to that key and `Email__FromAddress` to the verified sender in your
   production environment's variables — never commit the key to a config file.

With `SendGrid__ApiKey` set, `AddInfrastructure` (`backend/Talmidon.Infrastructure/DependencyInjection.cs`)
registers `SendGridEmailSender` instead of the Mailpit-facing `SmtpEmailSender`; every call site
uses the same `IEmailSender` interface, so no other code changes when you switch providers.

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
            # SENDGRID_API_KEY, EMAIL_FROM_ADDRESS (see the Configuration/SendGrid sections above)

docker compose -f docker-compose.prod.yml up -d --build
```

The API applies pending EF Core migrations automatically on startup (`MigrateDatabaseAsync` in
`Program.cs`), so there's no separate migration step. First boot: Caddy requests a certificate
for `DOMAIN` (needs the DNS record from step 1 to already resolve), Postgres initializes, and the
API seeds roles and — if `ADMIN_EMAIL`/`ADMIN_PASSWORD` are set — the one platform-admin account.

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
