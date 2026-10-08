# IIP pricing and AI credits plan

Written 2026-10-09. How IIP SaaS charges: a licence per organisation (seats included), plus AI credits that are spent as the platform does AI work, with a free start and a paywall when the free credits run out. It replicates CIP's metering design inside IIP and fits Microsoft Marketplace's billing rules. Prices below are proposals for the owner to set; everything else is the build.

## 1. The short answers

**ISV or ISP?** Otic is an **ISV** (independent software vendor): it builds IIP and sells it, including through the Microsoft Marketplace as a transactable SaaS offer. "ISP" is not a Microsoft partner category; the related term is **CSP** (Cloud Solution Provider), which is a reseller of Microsoft's own products. The two can be combined later (see section 6), but IIP itself is sold as an ISV.

**Can Otic earn a percentage of the tokens a customer burns?** Not as a commission from Microsoft. Microsoft does not pay an ISV a share of Azure OpenAI consumption. Otic earns on tokens the way CIP does: Otic pays Azure for the tokens at cost, and sells them to the customer as credits at a higher price. The difference is the margin, and it is set by Otic. On the Marketplace, Microsoft keeps a 3% store fee on what the customer pays ([Marketplace FAQ](https://learn.microsoft.com/en-us/partner-center/marketplace-offers/marketplace-faq-publisher-guide)), so the effective margin is (credit price x 0.97) minus token cost.

**Free first, then pay?** Yes. Every new organisation gets a trial allowance of credits. When it runs out, AI work pauses (the platform keeps working without AI, as Kato already does today with its rule-based replies), and the owner sees a clear prompt to choose a plan.

## 2. What Microsoft's Marketplace allows (the rules the design must fit)

| Rule | Consequence for IIP | Source |
|---|---|---|
| Metered billing only works on **flat-rate** plans, not per-user plans | Seats cannot be a per-user plan if credits are also metered. Each plan is a flat monthly price that **includes** a number of seats and a number of credits. | [SaaS metered billing](https://learn.microsoft.com/en-gb/partner-center/marketplace/partner-center-portal/saas-metered-billing) |
| A flat-rate plan can include a quantity and bill overage per unit via a custom dimension | "Credits included per month, extra credits billed per 1,000" is exactly the supported pattern. | same |
| Up to 30 metered dimensions per offer | Two are needed: `ai_credits_1000` (extra credits) and `extra_seat` (seats beyond the plan). | same |
| Usage must be reported within an hour, from the backend | A background job sends hourly totals per organisation to the Marketplace metering API. The browser never calls it. | [Metering service APIs](https://learn.microsoft.com/hi-in/partner-center/marketplace/marketplace-metering-service-apis) |
| Transactable offers priced above $0 can count towards a customer's Azure commitment (MACC) when the offer is Azure IP co-sell eligible | A strong selling point to large insurers and banks that already have Azure commitments. Requires co-sell readiness work in Partner Center. | [Transacting on the marketplace](https://learn.microsoft.com/en-za/partner-center/marketplace-offers/transacting-commercial-marketplace) |
| Store fee 3% | Built into the margin calculation above. | [Marketplace FAQ](https://learn.microsoft.com/en-us/partner-center/marketplace-offers/marketplace-faq-publisher-guide) |

Customers who sign up directly at `app.insuranceintelligenceplatform.com` (not through the Marketplace) are billed by Otic directly on the same plans, so the same meter feeds both routes.

## 3. What a credit is

A credit is IIP's unit of AI work. Customers see credits; Otic tracks tokens and cost underneath.

- **1 credit = US$0.01 of list value.** Easy to read: 1,000 credits is $10.
- Every AI call is recorded with its real token counts and the model's real price, then converted to credits at a fixed multiplier: `credits = ceil(token_cost_usd x markup / 0.01)`.
- Proposed markup: **3x** token cost. On today's model (Azure OpenAI `gpt-5.4`, Global Standard: $2.50 per million input tokens, $15 per million output tokens, [Azure OpenAI pricing](https://azure.microsoft.com/pricing/details/azure-openai/)) a typical Kato reply of about 3,000 input and 300 output tokens costs about $0.012, so it spends **4 credits** ($0.04). A claim summary of 12,000 input and 1,000 output tokens costs about $0.045 and spends **14 credits**.
- Embeddings (document indexing) are cheap enough to fold into the licence and are not charged in credits.
- Speech (voice notes) and translation calls are converted the same way from their own provider prices.

Showing both numbers matters: the meter shows "credits spent" and, for administrators, "cost to you", as CIP does.

## 4. Proposed plans (owner to confirm prices)

| Plan | Price | Seats included | Credits per month | Extra credits | Notes |
|---|---|---|---|---|---|
| Free trial | $0 | 3 | 2,000 one time (about 500 Kato replies) | none: AI pauses | 30 days, no card. Ends with a choose-a-plan screen. |
| Starter | $490 / month | 10 | 25,000 | $12 per 1,000 | Single product (Insurance or Bancassurance). |
| Growth | $1,490 / month | 30 | 100,000 | $10 per 1,000 | Both products, WhatsApp and voice. |
| Enterprise | private offer | custom | custom | custom | Private plan via Marketplace private offer; Fabric option; counts towards MACC. |
| Extra seat | $39 / seat / month | | | | Metered dimension `extra_seat`. |

Unused monthly credits do not roll over (keeps the meter predictable). Credits bought as a top-up last 12 months.

## 5. How it is built in IIP (replicating CIP)

CIP's design, already proven in production, maps onto IIP like this.

| CIP piece | What it does | IIP equivalent to build |
|---|---|---|
| `AiCostGovernanceService.CheckBudgetAsync` | Before every AI call: checks organisation, role and user budgets; returns allowed, warn, downgrade or block; audits the decision. | `IAiCreditGate.CheckAsync(scope, task)` in Application. Replaces today's `IUsageMeter.TryConsumeAsync(AssistantTurn)` count with a credit balance check. Block falls back to rule-based behaviour, never a broken screen. |
| `RecordUsageAsync` writing `AiCostUsageRecord` | After every call: user, role, task, model, prompt/completion/total tokens, estimated and actual cost, workload kind (internal or customer agent), agent, conversation. | `AiUsageRecord` table (relational, per tenant): same fields plus `Credits`. Written from one place: the AI provider adapters (`IToolCallingChatProvider`, chat, speech), so no call can skip it. |
| `OfficerUsageSummaryService` | 30-day spend, daily series, budget cap from tenant settings. | `AiUsageSummaryService`: credits and cost this period, by day, by feature (Kato, claim summaries, Command Center, documents), by user. |
| `OfficerBillingService` (billing profile, invoices) | Plan, billing contact, invoices. | Extend the existing `Billing` module (`EntitlementPackage`, `UsageMeter`) with the plan catalogue above, the credit balance, top-ups and invoice history. |
| `V2UsageView`, `V2BillingView` in the settings modal | Usage and billing screens. | Settings modal, **Usage and billing** section: credit meter, plan card, top-up button, invoices. |
| (none in CIP) | | `MarketplaceUsageReporter` background job: hourly, sends `ai_credits_1000` and `extra_seat` overage to the Marketplace metering API for Marketplace subscribers only, idempotent per hour per organisation. |

### The credit meter (what people see)

- A slim meter in the settings modal and a compact one in the user menu: "18,240 of 25,000 credits used this month", a bar, and the days left in the period.
- Warnings at 80% and 100%, to administrators only, in the app and by email.
- A breakdown by feature and by person, with a daily chart, and a "cost to you" column for administrators.
- When credits run out: AI features show "AI credits used up for this month" with a "Buy credits" or "Choose a plan" action for administrators, and "Ask your administrator" for everyone else. Nothing pretends to work.

### Rules carried over unchanged

- Credits gate cost, never authority: running out of credits never changes who decides anything, and human approval steps keep working without AI.
- Customers chatting with Kato are never told about credits. If an organisation runs out, Kato falls back to its plain replies and staff are alerted.
- Usage records carry no message content, only counts, costs and references, so the meter adds no customer data to the control plane.

## 6. Earning more around Azure (optional, later)

- **Managed Application edition:** when an insurer runs IIP in its own Azure subscription, the Azure spend is theirs. Otic can still be credited for that consumption through Partner Admin Link and Azure consumed revenue attribution, which counts towards Microsoft partner designations and co-sell incentives. It is recognition, not cash.
- **Becoming a CSP reseller (indirect, through a distributor)** would let Otic resell Azure itself to customers who want one bill, earning the reseller margin. It is a separate programme with its own obligations and is not needed for launch.
- **ISV Success:** worth enrolling now. It provides Azure credits ($5,000 core, more for eligible partners) and Marketplace benefits once the offer is published ([ISV Success packages](https://partner.microsoft.com/en-US/partnership/isv-success-benefits-guide)).

## 7. Build order (added to the launch plan)

1. `AiUsageRecord` table and the recording hook in every AI adapter, with tests that every provider path writes one record. Credits computed from a model price table in configuration.
2. Credit balance per organisation per period, plan catalogue, trial allowance on organisation creation. `IAiCreditGate` replaces the turn counter; existing plan limits migrate to credits.
3. Settings modal: Usage and billing section with the meter, breakdowns and plan card (the CIP layout).
4. Paywall states across AI surfaces (Kato, Command Center, claim summaries, document extraction).
5. Direct billing: top-ups and plan changes for direct sign-ups (payment provider to be chosen; Stripe or Flutterwave are the usual options for East Africa).
6. Marketplace: plans and dimensions in Partner Center, the hourly `MarketplaceUsageReporter`, and reconciliation report.

## 8. Decisions for the owner

1. Prices and credit allowances in section 4.
2. The markup multiplier (3x proposed).
3. Trial length and allowance (30 days, 2,000 credits proposed).
4. Payment provider for direct sign-ups.
5. Whether to enrol in ISV Success now (recommended).
