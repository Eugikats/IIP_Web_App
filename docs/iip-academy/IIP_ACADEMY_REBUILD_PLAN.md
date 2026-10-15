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
| Community posts by named people at named insurers, member counts, events with dates | Invented | A real community shared across organisations, empty to begin with (the design's welcome state), with consent, reporting and moderation (section 3), real member counts, and events entered by the Academy operator |
| Checkout with MTN MoMo, Airtel Money and card: "No real payment is taken" | Taking no money while saying "You're enrolled" is a pretend sale | Price 0: "Enroll for free". Price above 0 and payments not connected: the same dialog says so plainly and offers administrator enrolment. Real payment is phase 6 |
| Video player that counts a fake timer | There is no video | A real video player when a lesson has a recording. Without one the same area shows the lesson title and "No recording for this lesson yet. Read it below", and the lesson can still be completed |
| Zenith answers from canned rules, steps with invented timings | Pretending to search | Zenith answers from the course lessons with a model, steps show what it really did, sources are real lessons, quiz questions are the authored ones |
| `DISCORD_URL` placeholder | No link | Set in Settings. Until set, the Discord buttons are hidden and the banner shows only "Go to community" |
| Verification link `academy.iip.example/verify/CODE` | Placeholder domain | A real public page: `/academy/verify/[code]` |
| "Reset demo data" button | Demo only | Removed |
| The waving-hand emoji in the hero | No emoji anywhere | The same spot gets a drawn hand icon, same size, same wave animation |
| Language list (English, Kiswahili, Luganda, French) | The interface has one language | It sets the language Zenith answers in, using the translation service IIP already has for Luganda and Kiswahili. Languages the service cannot do are not offered |

## 3. Decisions (recorded 2026-10-15) and what they require

1. **Who learns: both.** The Academy is open through the IIP website (a public front door) and through the IIP dashboard
   (sidebar link). People from insurers and people with no organisation at all both take courses.
2. **Catalogue: startable courses, the rest "Coming soon".** The nine courses with written lessons can be started; the
   design's other courses are listed with their outline and cannot be started.
3. **Community: one shared community across organisations.**
4. **Zenith: model-backed, grounded in the lessons.**

What these four decisions force, which the plan now assumes:

| Because of | The build must |
|---|---|
| Public learners have no organisation | Run the Academy as its **own platform scope**, separate from every insurer's workspace. A learner is identified by their sign-in alone (the same way the API already knows a signed-in person who belongs to no organisation, as `GET /api/v1/me/organisations` does). Learner data, posts, certificates and prices live in that scope. No insurer's customer or claims data is reachable from it, and no learner data is written into an insurer's workspace |
| Public front door | Signed-out visitors can read **All courses**, each **course page** and the **certificate verification page**; everything else asks them to sign in. The navbar shows "Sign in" in place of the bell and user menu (drawn in the design's style). The IIP website's navigation and landing page gain an Academy link. Signed-in people from insurers land on the same pages |
| People outside insurers must be able to sign in | Today only Microsoft work or school accounts can sign in. **Google and email-code sign-in, which public learners need, need the Entra External ID setup that is not done yet** (it is on your registrations list). Until it is, the Academy opens to Microsoft work or school accounts only, and the sign-in page says so |
| Public learners pay | Paid courses need a **payment provider** (mobile money and card). None is chosen. Until one is connected, a priced course shows the checkout dialog in the design's look with "Payments are not connected yet" in place of the Pay button, and an Academy administrator can enrol a person. Free courses enrol at once. Which provider (for example a Ugandan mobile-money aggregator, or MTN and Airtel directly) is a decision still to make |
| Prices and administration | Prices, groups, events, the Discord link and moderation are managed for the whole Academy by the **Academy operator**: a designated workspace (set by configuration; yours) whose administrators manage it. The per-workspace "IIP Academy prices" screen in Settings is replaced by a single Academy price list in the operator's workspace |
| Shared community | A person's **name, job title and organisation are visible to every Academy learner** once they post or comment, so the first time they open Community they are shown exactly that and must agree. Report-a-post and remove-a-post for moderators, a posting rate limit, a block-list of words for the worst content, and the Academy terms and privacy notice updated to cover it (Uganda's Data Protection and Privacy Act: purpose, consent, the right to delete). No customer data may be posted; the composer says so. Delete my posts is in Settings |
| Zenith uses a model | The platform pays for Zenith: a **per-learner daily limit** (default 30 questions) and a **monthly platform budget** with a visible "Zenith is resting" state when the budget is spent, so public use cannot run up cost unseen. Answers come from the course lessons only and cite them. No personal or claims data is sent to the model |
| Multi-factor | Insurers' two-factor policies apply to their workspaces, not to the Academy. Academy learners are not asked for a second step |

Smaller facts needed: the Discord invite link; confirmation that you hold the rights to the hero photograph in the
design; whether lesson videos exist, and where they live; the payment provider.

## 4. How it is built

- **Its own area, not inside the IIP shell, and not requiring an organisation.** New route group
  `web/portal/app/academy/` with its own layout. It reuses IIP sign-in (returning to the page the person started on,
  as fixed on 15 October) but does not use the IIP runtime context, sidebar or assistant bar, because those assume an
  organisation. A small Academy identity context asks `GET /api/v1/academy/me` (name, email, whether they also belong to
  an organisation, whether they operate the Academy). Public pages render without sign-in. Signed-out, loading and
  error states are drawn in the Academy's look.
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

New tables (one migration), all in the Academy platform scope (a fixed scope reserved for it), keyed by the learner's sign-in identity, never by an insurer's workspace:

| Table | Holds |
|---|---|
| `AcademyEnrolments` | person, course, enrolled by (self, an Academy administrator, or the person's organisation), when |
| `AcademyProgress` | one document per person and course: completed lessons, notes by lesson, last lesson, quiz result, minutes |
| `AcademyCertificates` | code (unguessable), person, name as printed, course, issued date; the public verify page reads name, course and date only |
| `AcademyNotifications` | person, text, link, read, created |
| `AcademyProfiles` | job title, about, daily goal, language, notification switches, display name for certificates |
| `AcademyPosts` | post, tag, author, comments and likes inside the document; reports; soft-deleted by moderators |
| `AcademyConsents` | who agreed to the community notice, which version, when |
| `AcademyPrices`, `AcademySettings` | the price list, the Discord link, Zenith limits, the operator workspace |
| `AcademyGroups`, `AcademyMemberships`, `AcademyEvents`, `AcademyRsvps` | groups and events an administrator creates; who joined and who is going |

Endpoints under `/api/v1/academy/`: catalogue with the person's status, enrol, progress (complete a lesson, save a note,
record quiz), certificates (list, one, public verify), notifications (list, mark read), profile and settings, community
(posts, comment, like, delete by moderator), groups (list, join), events (list, RSVP; create by administrators), and
`zenith` (ask). Course prices stay where they are (Settings, IIP Academy, UGX 500,000 to begin with).

Who may do what: anyone may read the public pages; any signed-in person is a learner; the Academy operator's
administrators manage it (prices, enrol people, groups, events, moderation, the Discord link) under the existing
manage-settings permission in the operator's workspace. An insurer's administrator cannot manage the Academy from
their own workspace.

Notifications are generated by real events only: enrolled, certificate earned, someone replied to you, an event you
RSVPed to is tomorrow. The "weekly summary" and "email notifications" switches are stored but stay inactive until the
email channel is connected, and say so beside the switch.

One-time import: the old browser-stored completions (`iip-academy:{user}`) become server certificates on first visit
to the new Academy, then the browser copy is cleared. A person who already holds an IIP workspace account and takes a
course is the same learner (one sign-in identity); their workspace is shown as their organisation in the community, if they agree.

Zenith: `POST /api/v1/academy/zenith` retrieves the lessons of the person's courses (and course outlines), builds a
grounded prompt, calls the chat provider against the Academy's own monthly budget and the learner's daily limit, and returns the answer with the
lessons it used as sources and the steps it actually performed. Quiz requests use the authored questions, never invented
ones. It answers only from the courses: if the lessons do not say, it says so. Recommend-next and plan-my-week are
computed from real progress, then worded by the model or shown plainly if the model is unavailable. It does not give
insurance decisions, and is labelled as an assistant.

## 7. Build order

Each phase ends with its gate, a commit, and a deploy to the dev environment with live verification.

| Phase | Build | Gate |
|---|---|---|
| 0 Foundation | Reference and logos in the repo (done), hero image extracted to a file, CSS scoping script, icon file, fonts, route group, layout, Academy identity (`/api/v1/academy/me`), top bar with Back to IIP and a signed-out variant, bottom bar, toast, modal host, public and signed-in states | The empty shell matches the reference's chrome at 1280, 768 and 390 px; a signed-out visitor sees the public chrome; a signed-in person with no organisation is recognised |
| 1 Read-only screens on real catalogue | Dashboard, courses, course page (public, enrolled and coming-soon states), lesson page with the lesson mapping, certificates and certificate page, not found, signed-out; content adapter; the website link to the Academy | Every one of these screens matches the reference in the same state, pixel-compared at three widths |
| 2 Learner data | Migration, enrolments, progress, notes, quiz, certificates with public verify page, notifications, profile, progress page, settings; browser import; free enrolment and the honest paid state; the checkout and certificate-earned dialogs | Complete a course end to end in the browser and get a verifiable certificate; tenant A cannot see tenant B's learner data |
| 3 Community | Consent notice, posts, comments, likes, tabs, groups, events, group picker, report and moderation, rate limits, Discord link setting, terms and privacy updated | Post, comment, like and RSVP work across two different organisations' people; a reported post can be removed by the operator; a person who has not agreed cannot post; delete-my-posts works |
| 4 Zenith | Floating panel, full page, grounded answers with sources and real steps, authored quizzes, recommend and plan, daily limit and monthly budget with a visible resting state | Answers carry real lesson sources; an unanswerable question says so; the daily limit and the budget stop further questions and say why |
| 5 Cut-over | Sidebar link, redirects from the old routes, remove old Academy components and tests, docs, live deploy | Click IIP Academy in the sidebar, land on the new dashboard, click Back to IIP, land on the IIP dashboard; old links redirect |
| 6 Later, in this order | A payment provider (mobile money and card) so public learners can buy; Google and email-code sign-in for public learners (External ID); lesson videos; lessons for the "Coming soon" courses | Per item. Payment and public sign-in are what turn "open to the public" from visible to usable |

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
