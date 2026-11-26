# Changelog

Notable changes to Talmidon, newest first. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow
[Semantic Versioning](https://semver.org/spec/v2.0.0.html); before 1.0, a minor version may
change behavior.

A GitHub release takes that version's section below as its notes, so add the section
first. Then either push a tag `vX.Y.Z`, or run the Release workflow from the Actions tab
with the version, which tags the latest commit on main.

## [Unreleased]

## [0.1.0] - 2026-11-26

The first tagged version: the platform as it runs today. See the
[README](https://github.com/yt314/Talmidon#features) for the full feature list.

### Added

- **For tutors:** students and their parents, a lesson calendar with recurring weekly series
  that skip Jewish festivals, availability windows, pedagogical notes with per-note visibility,
  study materials, per-lesson billing with payment confirmations and monthly reminders,
  monthly income and attendance reports with CSV export, and one inbox for every conversation.
- **For parents and students:** portals scoped to exactly what concerns each of them. Parents
  see their children's lessons, shared notes, materials and payments, and can request or
  reschedule a lesson; students see their own schedule, notes and materials, and practice
  games, and never see payments.
- **For families looking for a tutor:** a public tutor library without a login, filtered by
  subject and city, with profile pages, a contact form and profile sharing by link, WhatsApp
  or QR code.
- An optional AI lesson planner (Gemini or Anthropic, whichever key is configured).
- Light and dark mode, Hebrew and right-to-left throughout.
- Production deployment with Docker Compose and Caddy, and weekly encrypted database backups.

### Quality

- CI runs the backend tests against PostgreSQL, the frontend unit tests, the production
  Docker images, and browser tests of the tutor's first day and of both portals.
- Every main screen of every role is checked for WCAG 2.1 AA accessibility in CI.
- Security headers and a content security policy on the site and the API; CodeQL and secret
  scanning on every pull request.
- The backend builds with warnings treated as errors, and dependency install scripts are
  denied.

[Unreleased]: https://github.com/yt314/Talmidon/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/yt314/Talmidon/releases/tag/v0.1.0
