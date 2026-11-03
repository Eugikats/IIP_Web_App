# IIP Bancassurance: everything in one place

Prepared 13 Oct 2026 for Eugene (Otic Group, leads IIP). Covers the whole body of work: Pearl Bank pricing and proposal, cost models, dashboards, tokenization research, per-seat pricing, and the HFB meeting prep.

**How to read this file:** bullets and tables first; detail in the linked files inside this zip. Items marked **[check]** could not be re-verified when this was assembled.

---

## 0. Folder map

```
IIP_Everything/
├── 00_MASTER_IIP_Bancassurance_Project.md   ← this file
├── 01_Meeting_prep/
│   ├── HFB_Meeting_Intro_and_10_Questions.md      (12 Oct, HFB)
│   ├── Pearl_Bank_Prep_Handoff.md                 (demo flow, Q&A, scoring, gaps)
│   ├── Pearl_Bank_Meeting_Playbook.pdf
│   └── IIP_Prospect_Scoring_Explained.pdf
├── 02_Pricing_documents/
│   ├── IIP_Pricing_Strategy_Summary.pdf           (4 Oct, UGX plans)
│   ├── IIP_Service_Costs_and_Pricing_v1 (.docx .pdf .md)   (9 Oct, CIP method)
│   ├── CIP_Service_Costs_and_Pricing_reference_text.md     (CIP v1 text, truncated at 60,000 characters)
│   ├── IIP_Pricing_v5_80pct_after_all_costs.md    (11 Oct)
│   └── IIP_Pricing_2026_Core_Growth_Enterprise.md (11 Oct)
├── 03_Dashboards_and_calculators/   (open .html in a browser)
│   ├── iip-unit-economics.html
│   ├── iip-bancassurance-pricing.html
│   ├── iip-price-card.html
│   └── iip-base-costs.html
├── 04_Product_and_concept/
│   ├── IIP_Concept_Deck.pdf
│   └── IIP_Consolidated_Stakeholder_Overview.pdf
└── 05_Source_scripts/   (build.js, calc.js, dashboard_check.js)
```

**Not in the zip [check]:** `CIP_Service_Costs_and_Pricing_v4.docx`, `attachment.txt` (CIP & IIP Enterprise Commercial Model) and `Bancassurance_Desk_Proposal.pdf` were uploaded earlier but are no longer in the workspace. Re-attach them if you want them bundled. The Pearl Bank slide images and email drafts were chat outputs and were not saved as files.

**Published artifacts in your account (live links):**
- IIP Unit Economics: https://claude.ai/artifact/F8UnkWSC5eabsPa5rf1frS
- IIP Bancassurance Pricing: https://claude.ai/artifact/Rkg77DzGhn1dYwsDfTckRF
- IIP Base Costs: https://claude.ai/artifact/8Qpyhyqu5k1ycrUtmn2oc8
- IIP Pricing 2026 (doc): https://claude.ai/artifact/GACWmrgYvAUZdcQCQ1Hno7
- IIP Pricing v5: 80% After All Costs (doc): https://claude.ai/artifact/E6wpsp5CkjbLwHqTWeU2HW
- IIP Price Card: https://claude.ai/artifact/6jLbZVFpV32nraydLqMxYB

---

## 1. What IIP is

- **IIP = Insurance Intelligence Platform**, by Otic Group. Azure-native, tenant-isolated, human-reviewed insurance operations layer for African insurers, brokers, TPAs, HMOs, bancassurance partners and microinsurers.
- **Principle:** AI prepares insurance work; people make regulated decisions (claim approval, payment, binding, underwriting, fraud determinations).
- **Two surfaces:** staff workspace (Microsoft Entra sign-in) and a customer portal reached by a single-use secure link (no account, no password).
- **Kato** = WhatsApp assistant. English, Luganda, Kiswahili, text and voice notes. **Dev only, unreviewed machine translation** (Sunbird AI, AfriSLM fallback).
- **IIP is not:** a core-system replacement, a payment engine, an autonomous claims/fraud decider, a place to price or sell cover.

### Bancassurance pipeline (the acquisition-led offer)

```
Customer list ─► Scoring ─► Drafting ─► Named person approves ─► WhatsApp (single / bulk)
 (file import)   (rules)    (agent)                                     │
                                                                        ▼
        Quiet prospects + policies about to end ─► Follow-up agent ─► back into the list
 Stranger texts in (branch QR, prefilled message) ─► appears as a scored prospect
 Policy wordings ─► uploaded, classified, approved by a person ─► Kato answers with citations
```

- **Scoring is fixed rules, not a model:** Need 35, Cover fit 25, Capacity to pay 15, Relationship 15, Can be contacted 10 (total 100).
- **Bands:** 80+ High priority, 65 Qualified, 45 Warm, 25 Early interest, below 25 Cold.
- **Caps:** only ever said "not now" → 20; declined or withdrew contact permission → 44.
- **Confidence:** High with 5+ facts or 6+ messages; Medium with 3+; Low otherwise.
- **Never inputs:** age, gender, religion, tribe, health (never recorded).
- **Kato rules:** names the insurer behind each cover, records only what the customer said, records consent with exact wording, human can take over (Kato goes silent, resumes after two idle hours or on handback), kill switch, audit trail, cited insights.
- **Meta limit:** outside the 24-hour window a message needs a Meta-approved template (Meta's decision).

### Honest status (say these plainly)
- Dev environment, invented customers, no live customer data.
- No live-traffic proof for WhatsApp, SMS or email. No staging or production.
- Data-processor registration under Uganda's Data Protection and Privacy Act not confirmed.
- Not connected to core banking. First connection would be a read-only file import. Claim-status API is a later conversation.
- Not tracking claims across insurers. Not issuing cover for real customers.
- List import / upload, drafting, single and bulk send, templates and follow-up agent were **not built** as of the Pearl handoff (7 Oct); you later said uploads were being worked on. **[check current state before demos]**

---

## 2. Pearl Bank (meeting 8 Oct 2026)

- **Audience:** Pearl Bank bancassurance team ("Frank's team"; Frank's exact role not confirmed; head of bancassurance per public profile: Kalinzi, to confirm).
- **Decision:** lead with **acquisition and relationship management on WhatsApp**; do **not** lead with claims tracking across insurers (would need every insurer's system).
- **End in mind:** a pilot discussion, not a sale. Minimum: second meeting with bancassurance and branch-sales people. Stretch: 60 to 90 day pilot on one branch group.
- **Demo:** QR → WhatsApp → Kato. Pearl person chats as a customer, then appears scored in the dashboard. Human takeover moment. Say "invented customer" and "demo products" each time.
- **Public facts (hypotheses):** about Shs22bn premiums through the bank in 2025 (up from 16.2bn), about Shs9bn loan-linked claims, about 10 partner insurers.
- **Say / do not say:**

| Say | Do not say |
|---|---|
| "AI prepares, people decide and send." | "Compliant" (IRA / Data Protection Act) |
| "Dev environment with invented customers." | The 60%, 45%, 35%, 92% figures (no baselines) |
| "Kato explains and recommends. A licensed person follows up." | "Multilingual" without caveat |
| "We start with a file import, read-only." | "Connected to your core system", "field-tested", "tracks your claims" |

- **Hard questions:** see `01_Meeting_prep/Pearl_Bank_Prep_Handoff.md` section 12 (core banking, claims, AI deciding, compliance, selling licence, data location, WhatsApp templates, pilot cost → "do not quote").
- **Training idea (secondary):** IIP Academy topics for the bank, courses priced per participant (UGX 500,000 per participant per course in the CIP-method doc).

---

## 3. Pricing: how the numbers evolved

### 3.1 Pearl Bank offers

| Version | Setup | Subscription | Total (year one) | Notes |
|---|---|---|---|---|
| Early | 7M | monthly (5.5M offered once) | 70M | You decided setup and monthly must be paid in full; no partial or batch payments |
| Revised | 12M | per-user model | 80M | You shared this model for feedback |
| Final proposal | 12M | 81M a year | **93M** | Sits below the Core list price of 136.8M a year (CIP method) |

- **Pearl vs insurer bundle (earlier comparison):**

| | Pearl Bank (70.0M) | Insurer bundle (50.0M) |
|---|---|---|
| Buyer | Bank, for acquisition | Insurer, for claims plus Kato |
| Monthly volume | 3,000 active prospects | 500 claims + 1,000 Kato customers |
| Seats | 25 | 10 |
| Running cost a month | 2.0M | 1.5M |
| Running cost a year | 24.0M | 17.4M |
| Setup | 7.0M | 10.0M |
| Margin excl. setup (annual) | 61.9% | 56.5% |
| Margin incl. setup (annual) | 65.7% | 65.2% |

- **Pearl at 93M re-costed with CIP's method (internal):** full-use cost about $1,225.62 a month (about UGX 58.8M a year). Margin on the 81M subscription 27.4% as the only client; 38.0% with Meta reply fees passed through; 42.6% on a shared environment; 53.3% with both. Profit incl. 12M setup: UGX 34.2M → 55.2M.
- **Actions from that analysis:** add Meta's reply fees to the contract (Kato's service replies are charged since 1 Oct 2026); move Pearl to a shared environment once a second and third client sign; full use is the worst case.
- **Corrections made earlier:** 137.1M is a *target* price, not a floor; 40/12 ≈ 3.33M; flagged proposal inconsistencies (meeting date, data-processor claim, allowances, score bands).

### 3.2 Price anchor and structure (IIP Pricing Strategy Summary, 4 Oct, USD 1 = UGX 3,981)

- Anchor: about 0.2% of an insurer's yearly premiums (assumed tech budget about 3% of premiums). Flat shilling fees, never a share of premium.
- **Staff platform:** Pay as you go (UGX 7,500 per claim, 3 seats) · Starter 1.5M/month (300 claims, setup 3M) · Growth 4.5M (1,200 claims, setup 8M) · Enterprise 12M (4,000 claims, setup 20M).
- **Kato:** PAYG 2,000 per active customer · Kato 1,500 at 2.5M · Kato 5,000 at 6.5M · Kato 15,000 at 15M.
- **Discounts:** pilot (3 months no monthly fee, pay setup, give baseline data and case study); first three insurers 30% off year one; pay a year upfront: two months free; two-year contract: setup waived; partners (e.g. Smart) keep 30% of fees for clients they bring.

### 3.3 CIP-method costing (v1 doc, 9 Oct) — IIP priced about 25% above CIP

| | Core | Standard | Enterprise |
|---|---|---|---|
| Monthly fee | $2,850 (UGX 11.4M) | $7,500 (UGX 30M) | $22,500 (UGX 90M) |
| Annual value | $34,200 | $90,000 | $270,000 |
| Onboarding | $3,000 | $6,000 | $12,000 |
| AI credits a month | 45,000 | 160,000 | 550,000 |
| Credit price over allowance | $0.020 | $0.018 | $0.016 |
| Support | business hours, next day | 8 business hours | 24/7 critical, 2 hours |

- **IIP credit:** 1 credit = about 4,000 tokens; price $0.02; about 79% margin; whole-journey costs shown (prospect conversation ≈ 19 credits; claim ≈ 70 credits).
- **Tokenization / ISV via Microsoft marketplace (research):** Microsoft keeps 3%; metered dimensions; price changes need at least 90 days' notice; payout about month 2 to 3; whether Ugandan buyers can transact through the marketplace **[unconfirmed]**.
- **Alternative cheap-entry strategy "IIP Start":** $490 a month (UGX 2.0M), small included allowance, earns on usage; Start vs Core compared on the same bank in the doc.
- **Contract terms that protect margin:** provider price pass-through with 30 days' notice; shilling review if UGX leaves 3,700–4,300 per USD; prices exclude 18% VAT; withholding tax does not reduce price; upgrade any time, downgrade at quarter end; every invoice paid in full.

### 3.4 Cost base (IIP Base Costs artifact)

- Fixed shared platform about **$400–850 a month** (App Service 150–250, SQL 75–150, AI Search about 75, local-language container 50–300, other 40–80).
- Variable: about **$0.26–0.40 per motor claim**; about **$0.03 per bancassurance lead**; a 10,000-customer campaign about **$270**.
- Not included: staff time, VAT, mobile-money fees, FX movement, human review during pilots.
- WhatsApp marketing messages grow fastest, so meter them separately from AI credits.

### 3.5 Later packaging docs (11–12 Oct, from your account) — **two different packagings; decide which is current**

**(a) "IIP Pricing 2026" / "v5: 80% After All Costs" (UGX, per company, unlimited users)**

| | Core | Growth | Enterprise |
|---|---|---|---|
| Price a month | UGX 2.0M ($500) | UGX 4.0M ($1,000) | UGX 6.0M ($1,500) |
| Annual upfront | 21.6M (10% off) in 2026 doc; v5 uses "1 month free" (22M / 44M / 66M) | 43.2M | 64.8M |
| Weekly allowance (2026 doc) | 750 credits | 2,000 | 5,000 |
| v5 monthly allowance | 2,000 credits | 4,500 | 6,500 |
| Credits after allowance | UGX 100 / 90 / 80 (2026) · UGX 130 / 120 / 110 (v5) | | |
| Worst-case margin | 82.3 / 78.9 / 75.5% (2026) · 80.7 / 80.8 / 80.5% (v5) | | |

- v5 answer: **80% pure profit after every cost is achievable at 2M / 4M / 6M**; ceiling about 83%; 90% would need the prices doubled; exchange-rate clause if UGX passes 4,200 to the dollar.
- v5 year-one model: 9 clients → about UGX 189M revenue, about 36% from credits; Pearl on Core with a UGX 4M credit budget lands near UGX 70M year one.

**(b) "IIP Price Card" (USD, aligned to CIP)**

| | Core | Growth | Enterprise |
|---|---|---|---|
| Price a month | $529 | $799 | $1,759 |
| Annual (10% off) | $476 | $719 | $1,583 |
| Credits a month | 2,500 | 6,000 | 9,000 |
| Extra credits | $0.030 | $0.028 | $0.026 |
| Profit as % of cost (annual, worst case) | 134% | 126% | 144% |

- Credit rules on the card: AI action or WhatsApp message = 1 credit; document page or SMS = 2; campaign message = 5; typical motor claim ≈ 70 credits. No setup fee; prices locked 12 months; training billed separately.

### 3.6 Latest per-seat model (most recent pricing discussion)

You asked: first package at $20, "$20 per seat", and a company could pay $100 for 5 seats, capturing data and moving to enterprise work.

| | Core | Plus | Enterprise |
|---|---|---|---|
| Price | $20 per seat | $45 per seat | $60 per seat + $2,500 platform |
| Minimum seats | 5 ($100 / UGX 400k a month) | 10 | 50 |
| Pooled credits | 750 per seat | 2,000 per seat | 2,500 per seat |

- Top-ups, e.g. 2,000 credits for UGX 280k.
- **Break-even about 13 organisations** against a UGX 10M monthly team overhead (**placeholder**).
- The unit-economics dashboard has **not** been updated to this per-seat model (offered, not done). The Word doc has not been updated with these packages either.

---

## 4. Dashboards and models (what each file does)

| File | Purpose |
|---|---|
| `iip-unit-economics.html` | Power BI-style dashboard: cost per client as clients scale, entry tier, adjustable token cut, base price and token budgets that force a tier upgrade or credit spend; Start tier section; CEO Q&A. Chart.js 4.4.1 from cdnjs (needs internet). |
| `iip-bancassurance-pricing.html` | Calculator comparing the 70M offer vs the 50M offer for the CEO (setup, months, uplift, cost sheet, margins). |
| `iip-price-card.html` | USD price card (Core/Growth/Enterprise) plus an internal "why these prices" tab. |
| `iip-base-costs.html` | Fixed and variable cost tables with unit prices and sources. |
| `IIP_Service_Costs_and_Pricing_v1.docx` | Word pricing document built with docx-js (11 pages) from the CIP v4 structure. |

---

## 5. Open items and risks

- **Unknowns:** Sunbird (translation/voice) price; real Azure bill; token counts per Kato message (unmeasured); whether Ugandan buyers can transact via Microsoft marketplace; support/team overhead (placeholders).
- **Assumptions to confirm:** 3% tech-budget assumption; claim and customer volumes; AI Search sizing; GPT-4.1 mini token estimates; Meta rates for Uganda; hosting region and data-residency (South Africa North would add about 10–25%).
- **Compliance:** data-processor registration; whether a per-customer Kato fee counts as a service fee (not commission) with the IRA sandbox; Kato sales-conduct rules with the insurer.
- **Version clash to resolve:** three live packagings exist (UGX 2/4/6M; USD 529/799/1,759; per-seat $20/45/60). Pick one for the next proposal.
- **Dashboard state:** several artifacts were edited after last review; treat the files in this zip as the current copies.

---

## 6. HFB meeting (12 Oct 2026, Housing Finance Bank, Uganda — confirm)

Full script in `01_Meeting_prep/HFB_Meeting_Intro_and_10_Questions.md`. Summary:

- **Open with the name:** "I'm Eugene, from Otic Group. We build IIP, the Insurance Intelligence Platform."
- **Frame the gap as an opportunity:** bancassurance already works; the steps between "might need cover" and "has cover" (who first, follow-up, consistent product answers) are manual. IIP makes them easier. AI prepares, people decide.
- **Platform walkthrough:** acquisition (list import, scoring, branch QR, inbound) → follow-up (quiet prospects, policies about to end, named-person approval) → policy-wording ingestion (approved sources, citations).
- **Ten questions prepared:** core banking, data location, who controls messages, licensing, scoring, wrong policy answers, consent and WhatsApp, languages, claims across insurers, who else uses it and what a pilot looks like.
- **Do not say:** compliant, field-tested, connected to your core system, registered data processor, any price.
- Public research suggested HFB may be moving core banking (Flexcube, with an ESB). Don't raise unprompted.

---

## 7. Preferences noted

- You prefer bullet points and diagrams over prose and skim documents; keep them concise.
- Round figures to 1 d.p. where asked; setup and monthly fees are paid in full (no partial payments).

---

## 8. Gaps in this archive

- The chat history before 12 Oct was compacted, so exact earlier wording (emails to Frank, slide text for the 80M and 93M decks, Claude Design prompt, the 10 hot AI-in-insurance topics worth $100, Academy course pricing, abroad benchmarks, valuation vs build) is **not reproduced here**. The numbers above come from files that still exist plus the saved session summary.
- If you want any of those rebuilt, tell me which and I'll regenerate it as a file.
