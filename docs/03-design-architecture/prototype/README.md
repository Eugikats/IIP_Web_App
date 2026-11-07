# IIP clickable prototype

Status: Design review aid, not a contract. Nothing in it is connected to a real service. Last reviewed 2026-11-07.
Audience: the product owner, who edits it, and the engineer who then builds from it.

## What it is

One HTML file, [iip-prototype.html](iip-prototype.html), with 33 screens and 12 settings sections. It runs by double-clicking it in a browser. It has no build step and needs no network (Inter loads from Google Fonts when online; otherwise system fonts are used).

It follows the rules in [DESIGN.md](../../../DESIGN.md): light and border-led, one blue accent for the next action, status as a neutral pill with a dot and a word, AI preparation and the human decision in separate containers, no emoji, no em dashes, and gradients only on the assistant orb and chart bars. Two deliberate exceptions, both stated in DESIGN.md: tinted status pills on Acquisition and Inbox, which are ported from CIP.

All names, numbers and amounts are invented for design review and are labelled as sample content on screen.

## How to use it

The dark bar at the top is for review and is not part of the product:

- **Screen** jumps to any screen. The profile menu (bottom of the rail) reaches the same screens the way the real product does.
- **State** shows the loading, empty, empty-after-filter, error, not configured and no permission versions of the current screen. DESIGN.md says a screen is not finished without all six.
- **Labels** shows a tag beside each title: In IIP now, From CIP, or Proposed.
- **Proposed additions** hides everything marked Proposed, so you can see the product as it is today.

Open Kato (the bar at the bottom of every screen) to see an instruction turned into a previewed proposal that waits for confirmation.

### Handing it back

Edit the file, upload it in the chat, and say what to change or build. The screens are plain template strings in one script, one entry per screen (search for `R.claims`, `R.operatorinvoice` and so on), so small edits by hand are safe. Settings sections are `SET.billing` and the like.

## What is in it

| Group | Screens | Source |
|---|---|---|
| Access | Sign in (Microsoft, Google, email; Google and email disabled until External ID is set up) | From CIP |
| Work | Brief, Claims, claim review, New claim, Acquisition, Kato workspace, Inbox, Academy | In IIP now; Acquisition and Inbox ported from CIP |
| Data | Data Hub with internal and customer agent tabs | In IIP now, with CIP's split |
| More | Assistant, My work, Customers, customer profile, Communications, Retention, Documents, Policy, Fraud, Underwriting, Automation, Insights, Audit, Search, Support | In IIP now; Assistant from CIP's Command Center |
| Admin | Admin console, Integrations and health | In IIP now |
| Settings | Integrations, Data Hub, Agent rules, Security and login, Notifications, Usage, Billing, Account seats, Organization, Products, Profile, Governance and policies | From CIP's settings screens; Billing, Usage, Seats and Products already exist in IIP in another form |
| Operator console | Customer accounts, account detail, Invoices, invoice detail, Onboard a customer, Billing setup | Proposed |

Proposed additions on existing screens: a Suggested for you list on the Brief, "Try saying" prompts on the Assistant, and Standing instructions, Suggestions and Decision log tabs on Automation. They come from [the agentic research](../../08-program-plan/IIP-AGENTIC-INTENT-SUGGESTIONS-AND-AUTOMATION-RESEARCH-2026-11-07.md).

The Kato workspace was reworked so that the steps, column mapping, reasons, exclusions, charts and the approval step are separate regions, and the send step says Not configured until templates and an approver exist.

## Billing: what was found

### IIP (read from `main`)

IIP already has a self-serve billing system. It is configured and works like this:

- **Plans** come from `PlanCatalogue` (`Domain/Billing/Plans.cs`). The default catalogue is Free, Agent (UGX 75,000 or USD 20), Team (UGX 370,000 or USD 99) and Enterprise (not self-serve, agreed directly), each with allowances for claims, documents, AI tokens, storage and seats. A deployment can replace it with the `INSURANCEINTELLIGENCE_PLAN_CATALOGUE` setting, which takes JSON. A malformed value or a catalogue with no free plan falls back to the default.
- **Subscriptions, payment attempts and invoices** are records (`Payments.cs`, `Plans.cs`). An invoice has a number, plan, amount, currency, kind (New or Renewal), status (Open, Paid, Void), receipt number, how it was paid and the period it covers.
- **Collecting money** goes through `PaymentService` and a provider selector. Flutterwave hosted card page and mobile money providers exist (`Infrastructure/Payments/LivePaymentProviders.cs`). The customer pays on Flutterwave's page, so card and phone details never touch IIP. IIP counts a payment only after asking the provider for its status; a call arriving at the webhook is never believed on its own. Each attempt has its own secret in the callback address, stored only as a fingerprint.
- **Settings** are `Flutterwave:SecretKey`, `Flutterwave:SecretHash` (the webhook secret) and `Flutterwave:BaseUrl`. With no key set the gateway is Not configured. **No Flutterwave setting was found on the dev API app**, so checkout on dev would say it is not configured.
- **Routes** are under `/api/v1/admin/billing`: plan, checkout, payments, invoices, receipt, cancel, resume, plus the public callback at `/api/v1/payments/callback/{id}/{secret}`. The portal page is Admin, Billing and Usage.
- Invoices are created **when a customer pays online**. No way for the Otic team to raise an invoice by hand, for example for a negotiated Enterprise deal or a bank transfer, was found.

### CIP (could not be fully verified)

- The local CIP checkout (`CIP_Web_App`, commit `1a4be27`) has **no Flutterwave code**. Its billing screen is `V2BillingView.tsx`, backed by `OfficerSettingsController.GetBilling` and `IOfficerBillingService`. It shows a payment method and a list of invoices, and its Update payment link goes to the Azure billing portal.
- The CIP screens you captured (Core, Growth and Enterprise plans, pay-as-you-go meters, 18% VAT, CIP credits, a usage budget, a note that the card is entered on Flutterwave's page, and annual plans held back above a USD 5,000 card limit) are **newer than that checkout**. Where that billing is configured could not be confirmed from code.
- A `git fetch` of CIP failed with an authentication error, and a retry hung on the credential prompt. Until it succeeds, the newer CIP billing code cannot be read. The prototype's Usage and Billing screens copy the layout from your screenshots, filled with IIP's own plans.

## Customer accounts: does Otic have a section to control them?

**No cross-customer console was found.** What exists:

- `PlatformRole.PublisherSupport` with the `ManageTenantOnboarding` permission, which operates a publisher queue for WhatsApp number setup requests, and a Tenant onboarding admin page for one customer's Entra tenant.
- Organisation records with a status, which sign-in checks (a suspended, cancelled or closed organisation is refused), and Microsoft Marketplace fulfilment that creates and syncs organisations.
- Everything else under Admin works inside one organisation.

So today there is no screen where the Otic team can list customers, see plan and payment state, raise an invoice, record a bank transfer, suspend or reactivate an account, or run onboarding from a paid invoice. The Operator console in the prototype proposes exactly that:

- **Customer accounts**: every organisation, plan, status, seats, billing.
- **Account detail**: overview, billing, seats, a go-live checklist and account events.
- **Invoices**: all invoices, create one, send it, copy the Flutterwave payment link, record a manual payment with a named person and issue a receipt.
- **Onboard a customer**: organisation, plan and price, invoice, payment, provision, invite administrator, handover.
- **Billing setup**: Flutterwave keys (write-only, validated before saving), the plan catalogue as an editable table instead of an environment variable, VAT 18%, invoice numbering, bank transfer details.

**Data boundary.** The operator console reads organisation, plan, seat and billing metadata only. It must never show claim, customer or document content, in line with the control-plane rule in the implementation guide. Every operator action is audited, and access is limited to a separate operator role, not to customer administrators.

## Not covered

- It is not the production UI and shares no code with `web/portal`.
- It does not exercise real permissions, real data or any provider.
- Layout was checked at 1280 and 768 pixels in Chromium. Other browsers were not tried.
