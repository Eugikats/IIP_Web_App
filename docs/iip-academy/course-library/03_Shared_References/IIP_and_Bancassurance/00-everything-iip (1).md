# IIP: everything we're working on

Prepared 13 Oct 2026 for Eugene (Otic Group, leads IIP, the Insurance Intelligence Platform).

## 1. Product and go-to-market
- **IIP** = Insurance Intelligence Platform, a sister product to CIP. IIP derives from CIP because their costs are essentially the same.
- Two planned editions: **bancassurance** and **insurance companies**, plus **training courses** (IIP Academy).
- Uganda go-to-market: partnerships with existing insurance system vendors, an outreach list and proposals, demo bookings with key people at insurance companies.
- Product focus now:
  - Make the **claims flow** simple end to end: product configuration, customer upload link with per-document slots, scanning.
  - Get **IIP Academy** working.
  - **Self-serve onboarding** (agent-guided setup, no developer) so it can be sold at roughly $20 per user.
- Rule: **every client gets a dedicated environment**, never a shared one, because clients handle sensitive data. This replaces the earlier shared multi-tenant plan.

## 2. Pricing decisions (Oct 2026)
- IIP pricing uses the same logic as CIP: tiers (Core / Growth / Enterprise), credits, a shared pool, usage-based billing.
- Moving from **per-seat** to **per-company with unlimited users** drawing on a shared credit pool.
- Same price points as CIP, with offerings tuned so clients need **credit top-ups**.
- Profit target is now **profit above cost** (profit as % of cost over 100%, as CIP reports it). This replaces the earlier 80% margin target and the UGX 2M / 4M / 6M prices.
- Model: **organisational licence + included AI credits + paid top-ups**.
- Otic is starting to **meter IIP's token usage**, weighing a free or low-upfront licence with usage-based (metered) billing as a **Microsoft ISV**.

## 3. Prospects
### Pearl Bank (bancassurance team)
- Met on 8 Oct 2026; preparing a proposal deck.
- Budget indicated: about **UGX 70M**.
- Decision: propose **IIP Core with no setup fee** and without the earlier 25-seat desk (previously a bundled setup plus subscription).

### Housing Finance Bank (Uganda)
- Met a contact about IIP bancassurance.
- Preparing: an illustration of how IIP connects to the bank's data (**batch export to a drive IIP reads, not direct core access**), a **customer journey diagram**, and **how the bank makes money** from it.

## 4. Recent achievement
- Represented Otic with colleague Martina Namutebi at the **UNDP x IRA Insurance Innovation Challenge 2026** (late Sep to early Oct), field-tested and pitched IIP, and co-wrote the five-day report to Otic management.
- IIP won **first runner-up and $4,000**, which they plan to turn into value for IIP.

## 5. This chat: Azure questions
Full detail is in the numbered documents in this pack. Summary:

### 5.1 Token metering (`01-azure-token-metering.md`)
- APIM gateway (key per bank, token cost per operation), Event Hubs, Functions with idempotency keys, Azure SQL Ledger wallet, Power BI statements.
- Each burn posts two entries: burn against the bank's wallet and Otic's fee as revenue.
- Reserve tokens for long jobs, alerts at 20% balance, contract terms on expiry and audit rights.

### 5.2 Email costs (`02-email-costs-azure.md`)
- Email is **usage-based, not fixed**. ACS Email: $0.00025 per email + $0.00012 per MB.
- At ~0.5 MB per email: ~$0.31 (1,000), ~$1.55 (5,000), ~$7.75 (25,000) per month.
- Azure bills **monthly in arrears**. How the bank pays Otic is Otic's choice; bundling an email allowance into the subscription is suggested.
- If IIP sends through **Microsoft Graph**, there is no per-email fee (mailbox licence, sending caps).
- Likely email senders: claims flow (upload links, reminders, status), staff alerts; account mail is negligible.

### 5.3 Fixed-cost verification (`03-fixed-cost-verification.md`)
- Check each fixed line in the Azure calculator (same region, SKU, currency), priced per dedicated environment.
- Screenshot was too blurry to verify; the rows are needed.

## 6. Open items
- [ ] Does IIP send email via **ACS or Graph**?
- [ ] Does the claims flow email the upload link, or use SMS/WhatsApp?
- [ ] Are the fixed lines in the pricing sheet **confirmed in the calculator** or estimated?
- [ ] Services to meter and expected bank volumes (to draft the token rate card)
- [ ] Pearl Bank proposal deck
- [ ] Housing Finance Bank illustration: data connection, customer journey, bank revenue
- [ ] Re-verify all Azure rates before sending any deck

## 7. Sources
- [Azure Communication Services email pricing](https://learn.microsoft.com/azure/communication-services/concepts/email-pricing)

## 8. Files in this pack
- `00-everything-iip.md` (this file)
- `01-azure-token-metering.md`
- `02-email-costs-azure.md`
- `03-fixed-cost-verification.md`
- `attachments/pricing-sheet-screenshot.png` (the photo you shared)
