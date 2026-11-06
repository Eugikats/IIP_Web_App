# IIP Bancassurance QR-to-WhatsApp Campaign Build Brief

**Status:** Planning brief with an approved first implementation slice
**Date:** 2026-11-05
**Related work:** [`IIP_BANCASSURANCE_ACQUISITION_BUILD_PROMPT.md`](../01-strategy-requirements-roadmap/IIP_BANCASSURANCE_ACQUISITION_BUILD_PROMPT.md)
**Audience:** Product, Engineering, Bancassurance, Compliance, and demo operators

## Outcome

An attendee scans a QR code, gives their mobile number and clear permission to receive insurance information on WhatsApp, and is matched to a campaign and product approved by the insurer. IIP reads a customer workbook, suggests how its columns map to IIP fields, reports problems, and lets a person confirm the mapping. The acquisition agent prepares a message for each eligible recipient using an approved WhatsApp template. A named staff member reviews the recipients and messages and approves the campaign. IIP sends only to eligible recipients. A customer can tap **Learn more** and continue the conversation with Kato on the same WhatsApp thread.

The product’s policy wording and approved product record remain the source of truth for cover descriptions. IIP may explain and collect interest; it must not bind cover, set a final price, decide eligibility, or make underwriting decisions.

## Recommended live-demo journey

1. **QR registration.** The QR points to a short, mobile-first IIP campaign page. It names the bank and insurer, the product/campaign, the purpose of collecting the number, the privacy notice, and how to opt out. The person enters a WhatsApp number and optionally their name. The form records affirmative, channel-specific marketing consent. A phone number by itself is not consent.
2. **Import and match.** The operator uploads an XLSX workbook or selects the campaign. IIP detects the likely header row and sheet, proposes field mappings, normalizes phones, and matches rows to QR registrations. The operator reviews the mapping and the validation report before importing. Rows without valid consent are ineligible for outbound marketing.
3. **Prepare.** The campaign selects one insurer-approved product and its published product profile. The system prepares one template instance per eligible contact, filling only approved placeholders from confirmed data. It explains the source of each populated value and shows any unresolved or conflicting values.
4. **Review and approve.** A named staff member reviews the audience, consent evidence, template version, language, media, button destination, and message preview. The approval applies to the exact campaign version and recipient set. Any edit to those items invalidates approval and requires review again.
5. **Send and reconcile.** IIP queues sends idempotently, respects sending limits, and records provider acceptance and later delivery/read/failure updates per recipient. It never reports a message as delivered merely because the API accepted it.
6. **Learn more.** A WhatsApp **Quick reply** button labelled **Learn more** is preferred for this demo. Its incoming button event opens or resumes the same contact thread and routes to Kato with the campaign and product context. A website URL CTA is an alternative when the destination is a real, approved product page; it should provide a clear path back into WhatsApp.
7. **Continue safely.** Kato answers from the approved product record and wording, captures the customer’s questions or interest, and offers a human handoff. The person can opt out at any time. STOP and equivalent supported phrases suppress further marketing.

**Channel ownership:** An inbound WhatsApp message belongs to the workspace with the connected business phone number. Opening a different demonstration workspace in the portal does not move that connection or change the number owner's assistant identity. To demonstrate an inbound conversation in a bank workspace, connect a dedicated number to that workspace before sharing its QR code. A queued reply must confirm the number still belongs to its original workspace before sending.

**Inbound visibility:** A first message creates a contact prospect in the number owner's workspace, including when the sender already holds a policy. Later messages update the same record's last interaction and activity, and request a fresh score. The acquisition record stores a generic contact reason; claim and health details remain in the scoped conversation. The Inbox shows the durable inbound message even while the assistant is disabled or its analysis is pending. An opt-out must not create a new sales prospect.

**Scoring:** A first contact is scored from what is recorded about the person (name, phone, any bank record). It shows little or "Not scored" until the conversation reading adds facts and interests, which needs the conversation insights agent and a configured model. A workspace that switches the Prospect Triage agent off in Admin, Agents is not scored by it, whether the request comes from a message or from the Score prospects button.

**Number ownership:** A message is answered only while its business number still belongs to the workspace that queued it. If the number was moved or disconnected between the message arriving and the answer being prepared, the message stays in the Inbox, is not answered from the old workspace, and the skip is written to the audit trail (`whatsapp.assistant_skipped_number_moved`). The connection status shown in the portal is read from a cache at most 30 seconds old; routing a message always reads ownership fresh.

**Moving the number between workspaces:** A person who administers two workspaces (for example two demonstration worlds) can open Admin, WhatsApp Business in the workspace that should receive the messages and choose "Use this number here". The saved connection is re-saved under that workspace and removed from the other without any credential leaving the server, the reason is required, and the move is audited in both workspaces (`POST /api/v1/integrations/whatsapp/configuration/move-here`, listing at `GET /api/v1/integrations/whatsapp/configuration/elsewhere`). It is refused if the person does not administer the workspace giving the number up, or if the receiving workspace already holds a number. Switching between demonstration worlds does not move the number by itself.

**Assistant identity after a move:** The assistant speaks as the identity saved in the workspace that holds the number (Admin, Operating settings, the Kato card). A workspace that held the number while an earlier build wrote a demonstration identity into it keeps that identity until someone clears or corrects it there. After moving a number away from a workspace, check that workspace's assistant name.

The Acquisition dashboard and Inbox show whether WhatsApp is configured in the workspace being viewed. This does not certify that Meta delivered a recent webhook. An empty Inbox in a demonstration workspace does not mean that no messages reached another workspace. A number can have one workspace owner; historical messages remain in the workspace where they were received unless a separately approved, audited migration moves them.

## Consent and customer-data boundaries

WhatsApp requires opt-in for business messaging, an approved template to initiate a conversation or message outside the 24-hour service window, and honoring opt-out requests. Uganda’s Data Protection and Privacy Act gives a person a right to prevent use of their personal data for direct marketing. Public bank privacy notices also describe sensitive records such as account and transaction information. That makes importing an entire bank file for a product demonstration unnecessary and inappropriate.

For the demonstration, use only:

- WhatsApp number in international format, such as `+2567XXXXXXXX`.
- First name, only if the person provided it or the bank is authorized to share it for this exact purpose.
- Campaign ID and insurer-approved product ID, usually held at campaign level rather than repeated on every row.
- Consent status, source, timestamp, consent wording/version, permitted channel and purpose, and withdrawal status. Prefer IIP’s QR flow as the source of this evidence.
- Optional non-sensitive context that the person explicitly supplied, such as a selected product-interest button.

Do not use account numbers, balances, transaction histories, card data, national ID numbers, health data, religion, ethnicity, disability, political views, or inferred financial capacity for this demo’s acquisition ranking or message generation. If a future insurer campaign needs bank-held attributes, the insurer must define the lawful purpose, data-sharing authority, minimization, retention, and approved deterministic targeting rules before import. The LLM must not infer these rules from a spreadsheet.

Suggested QR consent copy for review by the insurer and counsel:

> I agree that [Bank] and [Insurer] may use my phone number to send me WhatsApp information about [named product or campaign] and related follow-up messages. This is optional. I can opt out at any time by replying STOP. Read the privacy notice: [link].

Store the exact text/version shown, timestamp, campaign, source QR, phone number, and consent purpose. Do not pre-check the box. If a person only provides a phone number without this affirmative consent, retain them as non-contactable for marketing until they consent.

## Workbook import contract

The importer should accept varied workbook layouts without silently guessing or silently sending.

### Minimum viable workbook for the demo

| Canonical field | Required | Example header aliases | Purpose |
|---|---:|---|---|
| WhatsApp number | Yes | `phone`, `mobile`, `mobile number`, `telephone`, `whatsapp` | Match a row to the person who registered by QR. Normalize to E.164 and preserve the raw source value for audit. |
| First name | No | `name`, `first name`, `customer`, `client name` | Greeting only. Do not invent a name when absent. |
| Campaign or product ID | Campaign-level preferred | `campaign`, `product`, `cover`, `product code` | Selects an already published campaign/product. Reject unknown or ambiguous values. |
| Consent evidence reference | Yes for imported consent; QR record preferred | `consent`, `opt-in`, `marketing permission`, `permission source` | Must resolve to explicit, current consent evidence. A bare `yes` without source/time/text is insufficient evidence. |

The QR-registration data is the authoritative consent record. A workbook flag cannot overwrite an opt-out or create consent by itself.

### Smart mapping behavior

- Inspect sheet names, header rows, column names, and a small masked sample. Suggest mappings using known aliases first and AI assistance only for ambiguous headers.
- Show the selected sheet, detected header row, each proposed mapping, confidence, example values (masked where personal), and unresolved columns. Let an operator change mappings.
- Show row counts for valid, duplicate, no matching QR registration, invalid number, no consent, opted out, unknown product, missing required data, and eligible-to-draft.
- Normalize Ugandan numbers to E.164; flag ambiguous country codes and invalid lengths for correction. Do not guess a number’s country silently.
- Detect duplicates by normalized phone within the workspace and campaign. Show which row wins and why; never send twice to one person in a campaign.
- Preserve original file, sheet/row provenance, mapping chosen, operator, timestamp, and import summary according to the approved retention policy. Protect workbook content as untrusted input and ignore any embedded instructions.
- Never use a workbook upload as authority to send. The send gate rechecks tenant, connection, consent, opt-out, product/template approval, campaign approval, sending hours, and recipient eligibility immediately before dispatch.

### Helpful source fields for a later, contract-approved bank integration

Public privacy notices from Ugandan banks indicate they may hold identity and contact details, banking relationship/product details, account or transaction information, and other personal data. Those are examples of what an institution may hold, not a recommended IIP demo schema. A later integration should expose only insurer-approved, purpose-limited fields with provenance and retention rules. Start with coarse, non-sensitive campaign attributes only after legal and business approval; keep product suitability and underwriting decisions with the insurer’s approved rules and accountable staff.

## WhatsApp templates to prepare

Templates are submitted and approved in the insurer’s WhatsApp Business Account. The insurer must approve the wording, language, image, and destination. The categories below are recommendations; Meta assigns or may reclassify a template.

### 1. Required for the demo: product introduction

| Property | Proposed value |
|---|---|
| Name | `iip_bancassurance_product_intro` |
| Category | Marketing |
| Language | Submit the actual campaign language, such as `en` or `en_US` as accepted by the account. Submit separate language variants when needed. |
| Header | Image: `assets/bancassurance-whatsapp-header-concept.png` (illustrative concept, not insurer-approved artwork). |
| Body | `Hello {{1}}, thanks for asking about insurance at {{2}}. You can explore {{3}} cover and learn what it includes. Tap Learn more to chat with our assistant. Cover and eligibility depend on the insurer's policy terms. Reply STOP to opt out.` |
| Variables | `{{1}}` first name when known; otherwise use an approved neutral greeting variant. `{{2}}` event or bank name. `{{3}}` published product name. Include realistic examples for every variable in the submission. |
| Button | Quick reply: `Learn more`. Route its payload to the existing customer thread and the selected campaign/product context. |
| Purpose | Starts an explicitly consented marketing conversation and invites the customer to ask for product information. |

The body is deliberately restrained. Replace the generic wording only with product facts verified against the insurer’s published wording. Do not claim that the product is suitable, guarantee acceptance or benefits, or promise a premium that has not been calculated through an approved insurer process.

### 2. Recommended after the first response: conversation continuation

Usually unnecessary while the customer-service window is open because a free-form answer is allowed. Prepare only if the insurer has a real reason to re-open the conversation later.

| Property | Proposed value |
|---|---|
| Name | `iip_bancassurance_requested_followup` |
| Category | Utility when it responds to a specific customer-requested follow-up; Meta may reclassify if the copy becomes promotional. |
| Body | `Hello {{1}}, you asked us to follow up about {{2}}. Would you still like information? Reply STOP to opt out.` |
| Button | Quick reply: `Yes, tell me more`. Optional second button: `Not now`, if the flow handles it as a stop/snooze request. |
| Gate | Requires evidence that this customer requested this follow-up and still has the applicable consent. |

### 3. Add only for future scope

- **Renewal reminder:** for an existing policyholder and an identified policy expiry, with approved policy data and consent. Do not include policy numbers or sensitive financial details in the message.
- **Appointment reminder:** confirms a customer-requested or staff-booked appointment. Include only the minimum logistics needed.
- **Claim status notice:** transactional and based on a human-authorized workflow state. A template may notify the customer that an update is ready; final claim decisions and sensitive details should be communicated through the insurer’s approved process.
- **Authentication/OTP:** only if IIP adds a verified identity flow. Use Meta’s authentication-template format rather than a marketing template.

### Template approval submission checklist

- Correct WhatsApp Business Account and connected phone number.
- Lowercase unique template name, category, language, and exact body/header/footer.
- A sample image uploaded for image-header approval; final media must be licensed and appropriate for the insurer’s brand.
- Example values for every placeholder.
- Button type and label selected; quick-reply handling tested end to end, or a working HTTPS URL on an insurer-approved domain.
- Clear, truthful purpose and consent wording. No sensitive identifiers in the message.
- Store Meta’s template ID, status, category, language, version, and rejection/reclassification reason. Only an `APPROVED` version can be selected for a send.

## Image direction

The generated concept at [`assets/bancassurance-whatsapp-header-concept.png`](assets/bancassurance-whatsapp-header-concept.png) shows a fictional Ugandan small-business owner speaking with an adviser in a bank setting. It has no brand or embedded text. It is a starting point for layout and review, not a final insurer campaign asset or a submitted Meta sample. Ask the bank/insurer to approve imagery, brand use, any depicted adviser role, and the product context before production use.

## Build sequence and acceptance

This brief narrows the existing acquisition build prompt; it does not replace its prospect, consent, campaign, and audit requirements.

### First usable slice

1. **Campaign and product selection:** use one published, insurer-approved product and campaign; keep insurer and bank identity explicit.
2. **Consent QR flow:** record the customer’s affirmative WhatsApp marketing opt-in with exact notice/version and source.
3. **XLSX import:** multi-sheet discovery, suggested column mapping, validation report, editable mapping, deduplication, and explicit import confirmation.
4. **Contact matching:** normalize phone numbers and match import rows to valid QR consent records. Unknown/no-consent/opted-out people are excluded with a visible reason.
5. **Template management:** submit and list templates through the WhatsApp Business Account API; receive status changes; store approved template versions and languages; show only approved templates as sendable.
6. **Rich template sending:** support image headers and template buttons in the send payload. At minimum, send the selected approved image template with its placeholders and a quick-reply button. Map the button reply to the same prospect and conversation.
7. **Draft and review:** agent fills fixed approved placeholders from verified source fields. Staff can inspect, edit only permitted fields, remove recipients, preview the template, and approve the exact audience and version.
8. **Dispatch and recovery:** enforce consent, opt-out, connection health, sending hours, permissions, approval, tenant scope, idempotency, reconciliation of unknown sends, and provider delivery status.
9. **Conversation continuity:** a click/reply updates the existing prospect, displays campaign context to Kato, and obeys the assistant’s review/autopilot workspace mode and human escalation rules.
10. **Presentation verification:** demonstrate on a small, explicitly consented test group; verify desktop and mobile QR form, upload, mapping, review, approval, receipt, button reply, conversation continuity, opt-out, and audit trail. Do not use live people as recipients without their explicit demo consent.

### Acceptance evidence

- Variations in header spelling, column order, extra columns, blank rows, multiple sheets, and phone formats map predictably; ambiguous mappings require operator confirmation.
- Invalid, duplicate, no-match, opted-out, and no-consent rows are not sent and have clear reasons.
- The same campaign cannot send twice to a normalized phone because of duplicate rows or an API retry.
- A message cannot send with an unapproved template, mismatched language/version, unresolved placeholder, stale campaign approval, or invalid consent.
- Tapping **Learn more** is recorded and reaches the intended prospect/campaign/product conversation without creating a second customer identity.
- Staff see queued, accepted, delivered, read, failed, and unknown states accurately; unknown outcome blocks a duplicate retry until reconciled.
- The demo states honestly whether Meta submission, approval, live send, delivery, and button webhook have been verified in the target environment.

## Current repository status relevant to this plan

The Inbox can now include a Meta quick-reply payload when sending a named template and accepts the returned **Learn more** button tap as inbound customer text on the same WhatsApp conversation. The selected template must already be approved by Meta with a matching quick-reply button. IIP still has no WhatsApp Business Account template-create/list/status API, image-header payload support, QR consent intake, or workbook-to-dispatch step, so the QR-to-image-template journey remains incomplete. Do not present a template as submitted or approved unless Meta confirms that state.

### Kato workspace (workbook analysis, export and charts)

As of 2026-11-06 `/app/workspace` is a staff chat (visible with `ViewSalesLeads`; attaching a file needs `ManageSalesLeads`) that reaches three tools through plain keyword routing in `web/portal/lib/workspace.ts` (`routeInstruction`), not a language model. It declines measures the platform does not record, such as conversion and response rates.

- **Analyse a workbook** (`POST /api/v1/workspace/prospects/analyse`): reads `.xlsx` or `.csv` through `PolicyImportFileParser`, maps columns by alias, normalises phone numbers to E.164 (an unclear country is rejected, not guessed), removes duplicates by number, reads consent and opt-out columns, and lists sensitive columns (national ID, account numbers, balances and similar) without reading them. Scoring is the existing `ProspectScoring` rules inside `WorkbookProspectAnalyzer`, so each score carries its reasons. A cover that scores only on its built-in baseline shows as "No clear match" and gets the generic message. Consent in a file shows as "stated in file (evidence not verified)". The nine progress steps come from the run's real counts. The call is stateless; it writes only an audit event (`workspace.prospects_analysed`).
- **Export** (`POST /api/v1/workspace/prospects/export`): a four-sheet `.xlsx` (Prospect Analysis, Campaign Recommendations, Exclusions, Summary) carrying the analysis id, contact numbers masked, audited as `workspace.analysis_exported`.
- **Charts** (`GET /api/v1/workspace/charts?metric=&days=`): `new_prospects` per day (East Africa Time), `stage`, `product_interest` and `channel`, counted from lead records. An empty period returns `isEmpty` and the screen shows an empty state.

Not built: sending from an analysis (the card shows "Not configured"; sending stays with the named-approval outreach, which rechecks consent evidence per person); server-side storage of an analysis version (export re-posts the analysis); Meta template create, list and status sync; model-chosen tools; SharePoint ingestion; verified 30-second and 5-minute reminders; per-product onboarding forms; products outside the microinsurance catalogue (interest in an unconfigured cover is flagged for human review). The preview uses the QR-registration template, whose wording says the person asked about insurance, so it applies only to people who registered. A synthetic demo workbook is at `tests/fixtures/workspace/synthetic_uba_attendees.xlsx`.

The broader product requirement already appears in section F and G of the related [`IIP_BANCASSURANCE_ACQUISITION_BUILD_PROMPT.md`](../01-strategy-requirements-roadmap/IIP_BANCASSURANCE_ACQUISITION_BUILD_PROMPT.md): catalogue and approval lifecycle, campaign drafting, named approval, consent gates, and send status. This brief adds the event/demo specifics, flexible workbook import behavior, rich-media/button requirements, and customer-data limits.

## Research references

- [WhatsApp Business Messaging Policy](https://whatsappbusiness.com/policy/): opt-in, approved templates outside the 24-hour service window, automation/escalation, opt-out, and data restrictions. Policy reviewed 2026-11-05.
- [Meta WhatsApp Business Platform Postman collection: image-header template with CTA buttons](https://www.postman.com/meta/whatsapp-business-platform/request/n3jhmr4/create-template-w-image-header-text-body-text-footer-and-2-call-to-action-buttons) and [quick-reply template example](https://www.postman.com/meta/whatsapp-business-platform/request/uzphwqw/create-template-w-text-header-text-body-text-footer-and-2-quick-reply-buttons): confirms the API template patterns to validate against the current Graph API version.
- [Uganda Data Protection and Privacy Act, 2019](https://ict.go.ug/ims/public/site/documents/Data-Protection-and-Privacy-Act-2019.pdf), especially consent and the right to prevent direct marketing.
- [Stanbic Uganda privacy and security statement](https://www.stanbic.co.ug/uganda/personal/about-us/legal/privacy-and-security-statement): discusses express consent for electronic marketing to prospective clients.
- [Ecobank Uganda privacy notice](https://ecobank.com/ug/privacy-centre/personal-banking-privacy-notice): says marketing contact details are used with prior explicit consent.
- Existing IIP implementation scope: [`IIP_BANCASSURANCE_ACQUISITION_BUILD_PROMPT.md`](../01-strategy-requirements-roadmap/IIP_BANCASSURANCE_ACQUISITION_BUILD_PROMPT.md).

This is product and engineering planning, not legal advice. The bank/insurer’s privacy and compliance owners should approve consent wording, data-sharing purpose, message copy, and campaign audience before real customer use.
