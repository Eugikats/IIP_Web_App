# IIP Academy rebuild: from the HTML design to the real product

Written 2026-10-15. Source design: `docs/iip-academy/design/iip-academy-reference.html` (one file, 1,182 lines, 50 icons,
13 routes, 4 overlays), with `iip-academy-logo.svg` and `iip-logo.svg` beside it. This plan replaces the Academy built
on 2026-10-12 (`/app/academy`) with that design as its own dashboard, opened from the IIP sidebar, with a button in its
navbar that returns to the IIP dashboard.

The rule for this build: the screens are the design, exactly. What changes is what stands behind them. The design is a
prototype with invented people, invented progress and a pretend checkout. Everything on a screen either comes from a real
source or says honestly that it is not set up.

## 1. What the design contains

### Pages (13 routes, plus not found)

| # | Design route | What is on it | New route |
|---|---|---|---|
| 1 | `dashboard` | Welcome hero with photo and quote; three stats (enrolled, overall progress ring, certificates); "My courses" carousel with arrow; community banner with Discord button; Zenith mini box | `/academy` |
| 2 | `courses/{all,mine,active,notstarted,completed,explore}` | Search, filter chips, three-column course grid, empty-after-filter message | `/academy/courses?show=` |
| 3 | `course/{id}` | Sales page (not enrolled): hero, what you'll learn, who it is for, curriculum, instructor, price card, mobile price bar. Enrolled: progress card, continue button, curriculum with ticks | `/academy/courses/[id]` |
| 4 | `learn/{id}/{n}` | Wide video player (play, time, full screen), lesson text, key takeaways, "Try it" note box, previous and next, side list of modules and lessons | `/academy/learn/[id]/[lesson]` |
| 5 | `community` | First-time welcome (join a group), composer, Latest, Popular and Questions tabs, posts with like, comment and share, Discord card, your groups, upcoming events with RSVP | `/academy/community` |
| 6 | `assistant` | Zenith full page: chat with visible working steps, sources, quiz answers with feedback, capability list, agent activity log | `/academy/zenith` |
| 7 | `notifications` | List with unread dot, mark all read | `/academy/notifications` |
| 8 | `profile` | Cover, avatar, name and role, four counts, about, recent activity, skills, certificates | `/academy/profile` |
| 9 | `progress` | Four counts, this-week bar chart, daily goal meter, per-course progress | `/academy/progress` |
| 10 | `settings` | Profile, notification switches, language, daily goal, save, sign out | `/academy/settings` |
| 11 | `certificates` | Earned cards, locked cards with progress | `/academy/certificates` |
| 12 | `certificate/{id}` | The certificate (printable), or "not earned yet" | `/academy/certificates/[id]` |
| 13 | `signin` | Signed-out card | `/academy/signed-out` |
| 14 | not found | Page not found with a way back | `/academy/not-found` |

### Chrome and overlays

Top bar (logo, four links, bell with dot, user menu with profile, progress, certificates, settings, sign out); mobile bottom
bar (under 860 px); Zenith floating button and panel (opens over any page, full screen on a phone); toast; the checkout
dialog (choose how to pay, processing, enrolled); the certificate-earned dialog with confetti; the group picker dialog.
Print styles for the certificate. Three breakpoints (1060, 860, 420 px).

Added by us, not in the design: **"Back to IIP" in the navbar** (right of the links, before the bell, with the IIP mark,
and in the user menu and the mobile bar), and a public **certificate verification page** the design only points at.

## 2. What cannot ship as it is

The design is a prototype. Each row is something on a screen that is invented; what replaces it, with no change to the layout.

| In the design | Why it cannot ship | What stands there instead |
|---|---|---|
| "Jane Doe", her photo, job title, employer, email | A real person's learner is whoever is signed in | The signed-in person's name, initials avatar, job title they set, organisation from their workspace |
| Pre-filled progress, certificate `IIP-2026-0481`, activity, notifications | Invented | Nothing until they happen. Every list has an honest empty state in the design's own style |
| Streak "5 days", weekly minutes, skills chips | Invented numbers | Streak and minutes counted from lessons really completed; skills taken from the outcomes of courses really completed |
| 10 courses, hours, levels, 10 named instructors | 7 of the 10 have no lesson content, only filler the prototype generates; the people do not exist | Section 5: courses with written lessons are startable; the rest are listed as "Coming soon". The instructor card shows the course author only when one is recorded |
| Community posts by named people at named insurers, member counts, events with dates | Invented | A real community, empty to begin with (the design's welcome state), with member counts and events entered by administrators |
| Checkout with MTN MoMo, Airtel Money and card: "No real payment is taken" | Taking no money while saying "You're enrolled" is a pretend sale | Price 0: "Enroll for free". Price above 0 and payments not connected: the same dialog says so plainly and offers administrator enrolment. Real payment is phase 6 |
| Video player that counts a fake timer | There is no video | A real video player when a lesson has a recording. Without one the same area shows the lesson title and "No recording for this lesson yet. Read it below", and the lesson can still be completed |
| Zenith answers from canned rules, steps with invented timings | Pretending to search | Zenith answers from the course lessons with a model, steps show what it really did, sources are real lessons, quiz questions are the authored ones |
| `DISCORD_URL` placeholder | No link | Set in Settings. Until set, the Discord buttons are hidden and the banner shows only "Go to community" |
| Verification link `academy.iip.example/verify/CODE` | Placeholder domain | A real public page: `/academy/verify/[code]` |
| "Reset demo data" button | Demo only | Removed |
| The waving-hand emoji in the hero | No emoji anywhere | The same spot gets a drawn hand icon, same size, same wave animation |
| Language list (English, Kiswahili, Luganda, French) | The interface has one language | It sets the language Zenith answers in, using the translation service IIP already has for Luganda and Kiswahili. Languages the service cannot do are not offered |

## 3. Decisions for the owner

Each has a recommended default that the plan assumes unless told otherwise.

1. **Who learns and how enrolment works.** Recommended: people with an IIP account in an organisation. Free courses enrol
   at once; a priced course is enrolled by an administrator until a payment provider is connected. The alternative is open
   sign-up for the public with card and mobile money, which needs a payment provider chosen and credentials supplied.
2. **The catalogue.** Recommended: the nine courses with written lessons are startable (five programme courses and four
   hands-on courses), and the design's other courses that have no lessons yet appear as "Coming soon" with their outline.
   Alternatives: hide them, or write them first.
3. **Community scope.** Recommended: each organisation's community is its own. The design says "across institutions";
   one shared community means one organisation's staff and posts are visible to another's, which needs consent,
   moderation and terms, and cuts across tenant isolation.
4. **Zenith.** Recommended: model-backed, grounded in the lessons, using the workspace's AI credits like the rest of IIP.
   The alternative is rules only (recommend, plan, quiz), with no explanations.

Smaller facts needed: the Discord invite link; confirmation that you hold the rights to the hero photograph in the
design; whether lesson videos exist (and where they live).

## 4. How it is built

- **Its own area, not inside the IIP shell.** New route group `web/portal/app/academy/` with its own layout. It reuses
  IIP sign-in, the runtime context (who is signed in, which workspace), and the two-factor gate, but not the IIP sidebar
  or assistant bar. Signed-out, access-unavailable and loading states are drawn in the Academy's look.
- **The design's CSS, scoped.** `scripts/build-academy-css.mjs` reads the reference file's `<style>`, prefixes every
  selector with `.iipa` and writes `components/academy/academy.css`, so nothing leaks into the IIP screens and nothing
  from Tailwind's reset leaks in. Regenerable from the reference, which stays in the repo.
- **The design's fonts and icons.** Plus Jakarta Sans self-hosted through `next/font` (no runtime call to Google). The 50
  icon paths copied as they are into `components/academy/Icon.tsx`, so glyphs match.
- **One component per screen**, with the design's markup and class names kept, so the reference and the build can be
  compared pixel for pixel.
- **Old Academy retired at cut-over.** `/app/academy` and `/app/academy/[courseId]` redirect into the new area; the sidebar
  link "IIP Academy" points to `/academy`; old components and the browser-stored completions are removed after a one-time
  import (section 6).

## 5. Content: what the design needs and what exists

| Design needs | Exists today | Plan |
|---|---|---|
| Course: category, title, description, outcomes, who it is for, hours, level | Course has title, short, audience, duration, aim, outcomes, category | Add `level`, `whoFor`, optional `author`; durations stay "proposed" wording where the source says so |
| Modules with lessons (3 each), each with minutes | Modules, each one page: goal, outcomes, flow, screen, worked example, try it, takeaways | **Module stays a module; each module becomes lessons by section:** the idea (goal, outcomes, flow), in IIP (the real screen with callouts), the worked example, try it and takeaways. Minutes are reading time computed from the words (shown as "about 4 min read"), not invented |
| Key takeaways, a "Try it" note box | Takeaways and `tryIt` text exist | Used as they are |
| Lesson video | None | Optional `videoUrl` per lesson, from blob storage; absent shows the honest empty player (section 2) |
| Course check and certificate | Quiz of questions with explanations per course; certificate of completion | The quiz becomes the last lesson of the course ("Check your understanding"), in the design's quiz styling; passing it plus every lesson issues the certificate |
| 10 courses | 9 courses, 3 of them close to the design's ai-sales, ai-claims-fraud and claims-comm | Map by id; the 7 design courses without lessons are outline-only entries (existing `outline` field), listed "Coming soon", not startable |

All existing honesty rules stay: only features IIP really has are described as features, screens IIP does not have are
labelled, the certificate is a certificate of completion and says it is not an accredited qualification.

## 6. Data and server

New tables (one migration), all scoped to tenant and workspace, learner data also to the person:

| Table | Holds |
|---|---|
| `AcademyEnrolments` | person, course, enrolled by (self or administrator), when |
| `AcademyProgress` | one document per person and course: completed lessons, notes by lesson, last lesson, quiz result, minutes |
| `AcademyCertificates` | code (unguessable), person, name as printed, course, issued date; the public verify page reads name, course and date only |
| `AcademyNotifications` | person, text, link, read, created |
| `AcademyProfiles` | job title, about, daily goal, language, notification switches, display name for certificates |
| `AcademyPosts` | post, tag, author, comments and likes inside the document; soft-deleted by moderators |
| `AcademyGroups`, `AcademyMemberships`, `AcademyEvents`, `AcademyRsvps` | groups and events an administrator creates; who joined and who is going |

Endpoints under `/api/v1/academy/`: catalogue with the person's status, enrol, progress (complete a lesson, save a note,
record quiz), certificates (list, one, public verify), notifications (list, mark read), profile and settings, community
(posts, comment, like, delete by moderator), groups (list, join), events (list, RSVP; create by administrators), and
`zenith` (ask). Course prices stay where they are (Settings, IIP Academy, UGX 500,000 to begin with).

New permissions: every signed-in member may use the Academy; administrators manage it (enrol people, create groups and
events, moderate posts, set the Discord link) under the existing manage-settings permission.

Notifications are generated by real events only: enrolled, certificate earned, someone replied to you, an event you
RSVPed to is tomorrow. The "weekly summary" and "email notifications" switches are stored but stay inactive until the
email channel is connected, and say so beside the switch.

One-time import: the old browser-stored completions (`iip-academy:{user}`) become server certificates on first visit
to the new Academy, then the browser copy is cleared.

Zenith: `POST /api/v1/academy/zenith` retrieves the lessons of the person's courses (and course outlines), builds a
grounded prompt, calls the workspace chat provider through the existing AI credits, and returns the answer with the
lessons it used as sources and the steps it actually performed. Quiz requests use the authored questions, never invented
ones. It answers only from the courses: if the lessons do not say, it says so. Recommend-next and plan-my-week are
computed from real progress, then worded by the model or shown plainly if the model is unavailable. It does not give
insurance decisions, and is labelled as an assistant.

## 7. Build order

Each phase ends with its gate, a commit, and a deploy to the dev environment with live verification.

| Phase | Build | Gate |
|---|---|---|
| 0 Foundation | Reference and logos in the repo (done), hero image extracted to a file, CSS scoping script, icon file, fonts, route group, layout, top bar with Back to IIP, bottom bar, toast, modal host, sign-in and access states | The empty shell matches the reference's chrome at 1280, 768 and 390 px |
| 1 Read-only screens on real catalogue | Dashboard, courses, course page (both states), lesson page with the lesson mapping, certificates and certificate page, not found, signed-out; content adapter | Every one of these screens matches the reference in the same state, pixel-compared at three widths |
| 2 Learner data | Migration, enrolments, progress, notes, quiz, certificates with public verify page, notifications, profile, progress page, settings; browser import; free enrolment and the honest paid state; the checkout and certificate-earned dialogs | Complete a course end to end in the browser and get a verifiable certificate; tenant A cannot see tenant B's learner data |
| 3 Community | Posts, comments, likes, tabs, groups, events, group picker, moderation, Discord link setting | Post, comment, like and RSVP work; empty state is the design's welcome; another organisation sees nothing |
| 4 Zenith | Floating panel, full page, grounded answers with sources and real steps, authored quizzes, recommend and plan, credit metering | Answers carry real lesson sources; an unanswerable question says so; credits are used and shown |
| 5 Cut-over | Sidebar link, redirects from the old routes, remove old Academy components and tests, docs, live deploy | Click IIP Academy in the sidebar, land on the new dashboard, click Back to IIP, land on the IIP dashboard; old links redirect |
| 6 Later | A payment provider (mobile money and card), lesson videos, cross-organisation community if chosen, lessons for the "Coming soon" courses | Per item |

## 8. How "exactly the design" is proved

- **Parity test.** The reference file is opened in the browser with a fixed learner state; the new page is opened with the
  same data from a stub of the API. Screenshots of each screen at 1280, 768 and 390 px are compared, with the learner's
  name, photo and dates masked. A difference above a small tolerance fails the build.
- **Six states** on every page: loading, empty, empty after a filter, error, not configured, no permission. The design
  already draws some; the rest are drawn in its style, and tested.
- **Behaviour tests** for each action in the design (the 33 `data-act` actions: play, complete, save note, like, comment,
  post, RSVP, join groups, enrol, pay or not, print, share, ask Zenith, answer a quiz, and the rest).
- Backend tests for every endpoint, tenant isolation on every table, and the existing rules: no emoji, no em dash,
  human-review boundary untouched (the Academy teaches; it decides nothing).

## 9. What it replaces and what carries over

- Replaced: `web/portal/app/app/academy/*`, `components/academy/*` (catalogue, category browser, overview, player),
  `lib/academy/progress.ts`, the PDF certificate generator (the certificate page prints as designed), `tests/workspace/academy.spec.ts`.
- Carried over: all course and lesson content (`lib/academy/courses.ts`, `programmeCourses.ts`, `lessons/*`, the screen
  pictures in `public/academy/`), course prices and the Settings price screen, certificate wording, the honesty rules.

## 10. Left from the earlier build (unchanged by this plan)

Not built: web chat widget (W6), Microsoft Teams bot (W5), email from a stranger becoming a lead (W4 step two), data
connectors (W10), security self-audit (S6), SharePoint library as an export destination. Waiting on the owner: Google and
Microsoft registrations and Search Console verification (so connected folders and file picking can run for real), the
key ring move to Key Vault, a passkey trial on a real phone, and the decisions on which data connectors to build and on
recovery for a locked-out sole administrator. See `docs/01-strategy-requirements-roadmap/IIP_CIP_CHANNELS_AND_INTEGRATIONS_BUILD_PLAN.md`.
