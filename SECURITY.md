# Security Policy

Talmidon holds personal data about minors — names, schedules, pedagogical notes and payment
records — and separates every tutor's data from every other tutor's. Security reports are
therefore welcome, including for the deployment side and not only the code.

## Reporting a vulnerability

Please do **not** open a public issue for a security problem.

- Preferred: GitHub's private vulnerability reporting, from the **Security** tab of this
  repository ("Report a vulnerability").
- Alternative: email the address on [the owner's GitHub profile](https://github.com/yt314).

Please include what you did, what happened, and what you expected — and, where it matters, the
role you were authenticated as (visitor, teacher, parent, student, admin).

You will get an acknowledgement within a few days. This is a personal project without a paid
security programme, so there is no bounty, but credit is given gladly if you want it.

## What is most worth looking at

- **Tenant isolation** — any path that lets one teacher read or change another teacher's
  students, lessons, notes or payments. Isolation is enforced in three layers (an EF Core global
  query filter, `TenantId` enforcement in `SaveChanges`, and composite `(Id, TenantId)` foreign
  keys); a gap in all three is the highest-severity bug in this project.
- **Role and object-level access** — a parent or student reaching data belonging to another
  family under the same teacher, or reaching a teacher-only endpoint.
- **Authentication** — refresh-token rotation and reuse detection, email confirmation, lockout,
  and the rate limits on `/api/auth/*`.
- **Note visibility** — a note reaching a student or parent it was not marked visible to.

## Out of scope

- Findings that require an attacker to already control the server or the database.
- Missing hardening headers with no demonstrated impact, and automated-scanner output without a
  working proof of concept.
- Denial of service by volume alone.

## Supported versions

The `main` branch is the only supported version. There are no tagged releases yet.
