# IIP UI Craft Guardrails

| Field | Value |
|---|---|
| Version | 1.0 |
| Date | 2026-09-08 |
| Status | **Binding.** Every UI change is reviewed against §8 before merge. |
| Applies to | `web/portal` — every rendered surface, plus product copy anywhere a user can see it. |
| Companion documents | `iip_design_language_v2.md`, `iip_ux_cognitive_load_and_navigation_model.md`, `iip_workflow_legibility_contract.md` |

---

## 1. Why this document exists

IIP is sold to insurance companies. An insurer evaluating it is deciding whether to route regulated claims work — and their reputation with claimants — through our software. They form a judgement about our engineering rigour from the interface, in seconds, before they read a single capability.

There is a recognisable visual signature to software that was generated rather than designed. It is now widely identified on sight and openly mocked; there are browser extensions that detect it. If our product carries that signature, an insurer will assume the code behind it was produced the same way. The aesthetic-usability effect runs both directions: a polished interface earns trust our features have to justify, and a generic one burns trust our features then have to win back.

This document is the floor. It is not about taste — it is a set of binary, checkable rules.

## 2. The tells, catalogued

The visual signature of generated UI, as documented across design-community analyses of AI-built sites (sources in §10). Every item below is prohibited in IIP.

**Colour and surface**
1. Violet-to-indigo or violet-to-pink gradients on heroes, buttons, cards, or text.
2. Glassmorphism — translucent blurred panels over a colourful background.
3. Neon accents, glow shadows, coloured ambient light.
4. Dark mode with low-contrast body text used as a style choice.
5. Gradient text, gradient borders, animated or mesh gradient backgrounds.

**Layout**
6. A centred hero with a pill badge above the H1, on a screen that is not a marketing page.
7. Three or four identical feature cards, each with an icon on top and two lines of text.
8. Numbered step sequences ("1 Upload · 2 Analyse · 3 Decide") inside an operational product.
9. A stat banner of large numbers with no source and no way to click through.
10. Everything centred in a `max-w-7xl` container regardless of content.
11. Coloured left borders on cards used decoratively.
12. Uniform oversized rounding — `rounded-2xl` and `rounded-3xl` on everything.

**Type and icon**
13. Emoji as iconography, anywhere. See §3.
14. An icon in a small coloured rounded box wherever a real asset should be.
15. Font pairings used as decoration — display serif italics for accent words, a second display family for headings.
16. All-caps section labels used ornamentally rather than as structural eyebrows.
17. Two icon libraries in one product, or icons at inconsistent stroke weights.

**Motion and interaction**
18. `hover:scale-105` and lift-on-hover across cards.
19. Entrance animations, staggered reveals, scroll-triggered fades.
20. Moving skeleton shimmer.
21. Spinners as the only loading treatment.

**Content — the most damaging category**
22. Fabricated statistics, fake dashboard numbers, invented percentages.
23. Placeholder people: "John Doe", "Jane Smith", "Acme Corp", lorem ipsum.
24. Empty states that describe the product instead of saying what will appear.
25. Buttons that do nothing, or navigate to a screen that does nothing.
26. Marketing voice inside an operational product — "Supercharge your claims workflow", "AI-powered insights".
27. Features advertised as "Coming soon" in shipped UI.

Items 22–27 are not only craft failures. They violate the honest-state invariants in `AGENTS.md` — protected runtimes never render seeded business data, fake metrics, or unconfigured services shown as working. A fabricated number in a demo screenshot is a product defect.

## 3. The emoji ban

**No emoji anywhere. No exceptions.**

**No em dashes anywhere either.** Not in button labels, empty states, toasts, code comments, commit messages or documentation, and not in model output shown to a person. Write a comma, a colon, a full stop or parentheses instead, and do not swap in a double hyphen. The em dash has become the most recognisable tell of machine-written text, and an insurer's staff should never read the product that way. Model completions are normalised once at the provider boundary by `DisplayText.WithoutEmDashes`; hand-written text has no such net, so check before merging.

This covers:

- Every rendered surface of the product — navigation, buttons, headings, body copy, empty states, error messages, toasts, notifications, chat responses, tooltips, badges, tables.
- Every string in the codebase that can reach a user, including drafted customer communications and email or WhatsApp templates.
- Agent and model output rendered in the UI. If a model returns an emoji, strip it before display.
- Commit messages, pull request titles and bodies, code comments, and log output.
- Documentation in this repository.

Why, concretely:

1. Emoji are the single most recognisable marker of generated interfaces. One in a nav item undoes a great deal of careful work.
2. They render differently on every platform and inconsistently in enterprise Windows environments, where our users work.
3. Screen readers announce them with verbose, often absurd names in the middle of a sentence.
4. They carry cultural connotations that vary by region — an unacceptable risk in regulated communication with claimants.
5. They cannot express any of the states this product needs. A dot and a word can.

Where the reference implementation uses emoji — the Appearance settings and the suggestion chips in `reference/bevaya-ui-replica.html` — that is explicitly not copied. Use a `lucide-react` glyph at the specified stroke weight and size, or nothing at all.

Enforcement: the check specified in §9 fails the build on any emoji in `web/portal`.

## 4. The violet problem, and why our restrictions are load-bearing

Our brand colour is `#5B2AE8`. Violet-and-indigo is also the most-cited signature of AI-generated design. This is a real tension and it is resolved by *how* the colour is used, not by abandoning it.

| Generated look | IIP |
|---|---|
| Violet as atmosphere — gradient backgrounds, glowing cards, tinted regions | Violet as **action** — the button you should press, the link, the thing that is currently active |
| Gradients everywhere | Flat fills. Gradients in exactly two named places (design language §4.5) |
| Violet plus five other accent colours | One violet, plus four semantic colours that appear only as 6px dots and single figures |
| Violet covering large areas | Violet covering perhaps 2% of any screen |
| Dark violet-tinted surfaces | White surfaces, hairline borders, a near-white page ground |

Stated as a rule: **violet marks what to do next; it never decorates.** A screen where you cannot immediately point at the one violet element has failed this rule.

## 5. What separates designed from generated

The tells above are what to avoid. These are what to actually do — they are what make an interface read as built by someone who cared.

1. **One spacing rhythm, held.** Every gap comes from the permitted set. Inconsistent spacing is the fastest way to look unfinished, and it is invisible to the person who created it.
2. **Optical alignment over mathematical.** Icon and text baselines aligned by eye; a `14px` icon beside `14px` text usually needs a `0.5px` nudge. Nothing on the screen looks *nearly* aligned.
3. **One radius per element class, always.** A `12px` card next to a `16px` card is noticed before anything else on the screen.
4. **Density that respects the work.** Insurance operations staff compare rows. They want more rows visible, not more whitespace. Whitespace separates meaning; it does not decorate emptiness.
5. **Real content at design time.** Design with real Ugandan names, real claim references, real policy numbers, realistic document titles, and the longest string the field can actually hold. Layouts that only work with short tidy strings break on contact with production.
6. **Empty states that say what will appear here**, not what the product is. "Claims assigned to you will appear here." Not "Welcome to your claims workspace!"
7. **Tables before charts.** A chart is ambient; the table is the truth. Every chart has a table under it.
8. **Loading states shaped like the content that is coming.** Skeletons match the real layout's geometry, so nothing jumps when data lands.
9. **Nothing renders that does nothing.** No decorative counters, no unlinked figures, no buttons pending implementation. Ship the screen without the control rather than with a dead one.
10. **The last 5%.** Focus rings, hover states, disabled states, keyboard paths, the `768px` breakpoint, the 60-character claimant name. This is the entire difference between a demo and a product, and it is precisely what gets skipped.

## 6. Copy rules

- Sentence case everywhere. No Title Case On Buttons.
- Second person, present tense, active voice.
- Verbs on buttons, and specific ones: `Request missing documents`, not `Submit`. `Approve claim`, not `OK`.
- No exclamation marks. No "Oops". No "Uh oh". No apologising to the user for a system state.
- No superlatives, no marketing adjectives, no "powerful", "seamless", "intelligent", "supercharge", "effortless".
- Never say "AI" where a specific verb is available. "Extracted from the police abstract", not "AI-powered extraction".
- Numbers carry units and denominators: `5 of 7 documents`, `48h SLA · 6h remaining`.
- Error messages state what happened, what it means, and what to do — in three short sentences at most, with no error code as the primary content.
- Never blame the user, and never imply the machine decided something a human decided.

## 7. Iconography discipline

`lucide-react` only, stroke `1.8`, sizes from design language §7. No emoji. No filled icons. No second library. No decorative icon that merely repeats its adjacent label. Consequential actions always carry a text label.

## 8. Pre-merge review checklist

Every UI change is reviewed against these. Each is a yes/no question with no interpretation required. A single "no" blocks the merge.

**Tokens and craft**
1. Zero literal hex values, `rgb()`, or arbitrary pixel values in the component?
2. Every gap from the permitted spacing set?
3. Every radius from the six defined tokens, with nothing above `16px`?
4. `box-shadow: none` on every static card, panel, and tile?
5. Zero gradients outside the two permitted uses?
6. Zero emoji in any file touched by this change?
7. Exactly one violet primary action per screen region?
8. All status shown as dot plus text label, never colour alone?

**Content honesty**
9. Zero fabricated numbers, placeholder names, or lorem ipsum?
10. Every displayed figure traceable to a real API response?
11. Every rendered control actually functional?
12. Unavailable capability shown as `Not configured`, never as zero and never as working?
13. Zero references to unshipped features?

**States**
14. All six states from design language §8.24 implemented and visually verified?
15. Empty-with-no-data distinct from empty-after-filtering?
16. Errors carry a plain-language sentence, a retry, and a correlation id — with no raw provider error?

**Interaction**
17. Full keyboard path, visible focus ring, `Escape` closes overlays, focus returns to trigger?
18. Screen-reader names on every icon-only control; status changes announced?
19. No colour-only meaning anywhere?
20. Reflows at 320 CSS px and 400% zoom without horizontal scroll?

**Workflow**
21. Status labels match the vocabulary table exactly?
22. Owner shown and never blank?
23. AI output visually separated from human decision surfaces?
24. Every actionable AI assertion carries a citation?
25. Every consequential action has an explicit verb and a consequence preview?

**Verification**
26. `pnpm lint`, `pnpm build`, `pnpm test` run and pass?
27. The screen was actually opened in a browser and looked at, at 1280px and 768px?

## 9. Automated enforcement

The following belongs in `web/portal/scripts/check-ui-craft.mjs`, wired into the existing `pnpm test` chain alongside `check-ui-release-gates.mjs`. It fails the build — it does not warn.

| Check | Rejects | Scope |
|---|---|---|
| Emoji | Any codepoint in the Unicode Emoji ranges (`\p{Extended_Pictographic}`), plus variation selectors and ZWJ sequences | `app/**`, `components/**`, `lovable-pages/**`, `lib/**`, `context/**` |
| Gradients | `gradient` in any className or style, allowlisting only the orb and chart-bar components by exact file path | as above |
| Literal colour | `#[0-9a-fA-F]{3,8}` and `rgb(`/`hsl(` literals in `.tsx`, allowlisting only `app/globals.css` | as above |
| Oversized radius | `rounded-2xl`, `rounded-3xl`, `rounded-full` on non-pill elements, `border-radius` above `16px` | as above |
| Decorative shadow | `shadow-lg`, `shadow-xl`, `shadow-2xl`, `drop-shadow` | as above |
| Hover transforms | `hover:scale`, `hover:-translate`, `hover:shadow` | as above |
| Placeholder content | `lorem ipsum`, `John Doe`, `Jane Smith`, `Acme`, `example.com`, `foo@bar` | non-test files |
| Fake figures | Numeric literals inside stat-tile and metric components | `components/MetricCard.tsx` and stat-tile consumers |
| Marketing copy | `Coming soon`, `Powered by AI`, `Supercharge`, `Seamless`, `Effortless` | user-visible strings |
| Second icon library | Any icon import that is not `lucide-react` | as above |

Test files and Storybook stories are exempt from the placeholder-content check only. They are **not** exempt from the emoji check.

## 10. Sources

- [Signs of vibe coded UI — yuwen lu](https://x.com/yuwen_lu_/article/2041187936738447565)
- [vibecoded-design-tells — Reddit-mined ranking of visual tells of AI-built sites](https://github.com/JCarterJohnson/vibecoded-design-tells)
- [AI Design Slop: 16 Patterns That Out Your App as Vibe-Coded — Developers Digest](https://www.developersdigest.tech/blog/ai-design-slop-and-how-to-spot-it)
- [7 Signs a UI Has Been Vibe Coded (And How to Avoid Them) — The Fountain Institute](https://www.thefountaininstitute.com/blog/signs-vibe-coded-ui)
- [Why Your Vibe-Coded App Looks Like Every Other AI App — The Crit](https://thecrit.co/resources/vibe-coding-design-guide)
- [AI's Purple Problem: Make Your UI Unmistakable — DEV Community](https://dev.to/jaainil/ai-purple-problem-make-your-ui-unmistakable-3ono)
- [Vibe — detector for AI-generated websites](https://tryvibe.vercel.app/)
