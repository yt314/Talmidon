# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Users

- **Primary: the private tutor.** Any tutor who wants it: an independent private tutor, a tutor from the religious/Haredi community, or a school teacher giving private lessons on the side. She signs up on her own, without approval, and runs her whole practice from the app: students, parents, the lesson calendar, pedagogical notes, billing, and her public profile. Before Talmidon this was usually spread across a notebook, WhatsApp and Excel.
- **Parent.** Sees only their own children: schedule (and can request, reschedule or cancel a lesson), the notes marked visible to them, study materials, full payment status, and a message thread with the tutor.
- **Student (any age).** Sees only their own schedule, the notes marked visible to them, materials, practice games, and a message thread. Never sees payments.
- **Visitor (no login).** A family looking for a tutor in the public library: filter by subject and city, view a profile, send a contact request.
- **Admin.** A deliberately minimal maintenance role: list tutors, lock/unlock accounts, curate subject suggestions, read site feedback.

## Product Purpose

Talmidon is a multi-tenant platform for private tutors. Each tutor manages only her own students, with full data isolation between tutors. On top of that sits a public, login-free tutor library that connects families directly with tutors.

Success means a tutor stops juggling separate tools, and the parents and students around her see what concerns them without having to ask.

## Positioning

Two things together:

1. **Everything in one place.** Calendar, per-lesson billing, pedagogical notes, parents and messages in one system, instead of WhatsApp + Excel + a notebook.
2. **Transparency for parents.** A parent sees exactly what is happening with their child (lessons, notes the tutor chose to share, and what is paid or still open) without asking the tutor. Students get their own scoped view.

These rest on being built for Israel: Hebrew and right-to-left throughout, the Jewish calendar, Israeli phone formats and WhatsApp.

## Operating Context

- Hebrew UI, right-to-left everywhere; Israel only. One timezone (`Asia/Jerusalem`), Israeli phone formats, PrimeNG localized to Hebrew.
- Recurring lesson series skip festivals, chol hamoed and festival eves by default (Israeli practice), and the skip reason is shown rather than silently applied.
- Contacting a parent happens through `wa.me` WhatsApp links and prefilled `mailto:` links.
- Billing is per lesson, never a monthly subscription. The tutor marks a lesson billable when she closes it. Parents get a monthly email reminder covering their open charges, and an email before each lesson.
- Reports (monthly income, attendance) export to CSV that opens correctly in Excel with Hebrew.
- Light and dark mode follow the operating system, with a toggle in every shell.

## Capabilities and Constraints

- Roles and their visibility are strict. Tenant isolation between tutors is absolute.
- **Note visibility rule:** every pedagogical note has "visible to student" and "visible to parent" toggles. Anything visible to the student is automatically visible to the parent (server-enforced). The tutor can still share a sensitive note with the parent only.
- Students never see payment information.
- Study materials are shared by definition and have no visibility flags.
- The AI lesson planner is optional and hides itself entirely when no provider key is configured.
- The practice games use reviewed, checked-in content, not generated content.
- Stack is fixed by the existing codebase: Angular 21 + PrimeNG with a custom preset (`frontend/src/app/core/theme/talmidon-preset.ts`). Components must not hardcode colors (`frontend/src/styles.scss`).

## Brand Commitments

- Name: **תלמידון (Talmidon)**.
- The public promise in the site description: finding the right private tutor for your child by subject, city and price, directly with the tutor, **with no commission** ("ישירות מול המורה, בלי עמלות").
- A permanent "beta" badge opens a short feedback form.

## Evidence on Hand

- Stage: **personal / portfolio project.** There are no real users yet.
- Screenshots in `docs/images/` show the running app against **seeded demo data**, not real customers.
- Requirements, data model and screen specs: `docs/01-אפיון-דרישות.md`, `docs/02-מבנה-בסיס-נתונים.md`, `docs/03-מסכים-וממשק.md`.
- **Do not fabricate:** testimonials, tutor or user counts, ratings, partner logos, press, or usage numbers. None exist.

## Product Principles

1. **One place for the whole practice.** A new feature should remove a tool the tutor uses on the side, not add another one.
2. **Each role sees exactly its own slice.** Transparency for parents is a feature; leaking data across students, parents or tenants is never acceptable.
3. **Built for here.** Hebrew, right-to-left, the Jewish calendar and Israeli habits (WhatsApp, local phone numbers) are first-class, not a translation layer.
4. **The tutor's time is the scarce resource.** Common actions (closing a lesson, charging, messaging a parent) should take one or two steps.
5. **Honest about state.** Show what happened and why (a skipped festival date, an open charge, a lesson still waiting to be closed) instead of applying it silently.
