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
│   └── Talmidon.Api/             # Web API controllers, JWT auth, request/response contracts
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
| `Email__*` | SMTP host, port, credentials, and sender identity |

## Documentation

See [docs/](docs/) for the original requirements specification, database schema design,
and screen/wireframe planning (in Hebrew).

---

Created by **Yehudit Pollock**
