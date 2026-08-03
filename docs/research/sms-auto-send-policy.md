# Research: May a low-risk status SMS auto-send, or must every outbound SMS have human approval?

**Date:** 3 August 2026
**Context:** `OutboundSmsGuard` in the Africa's Talking SMS provider. Deciding whether a system-generated, low-risk status SMS (e.g. "we received your claim") may auto-send, or whether every outbound SMS needs individual human approval.
**Note:** This is a design/compliance research note, not legal advice. A Ugandan lawyer should confirm before production.

## Bottom line

**A low-risk, transactional status SMS MAY auto-send — but "low-risk" must be defined narrowly, and a bare `LowRisk` boolean is not a sufficient gate.** Auto-send should be permitted only when the message is a bounded, non-consequential transactional notification from an approved template, consent + opt-out are honored, and tenant policy has opted in (conservative default OFF). Any SMS that communicates a **consequential insurance decision** (approval + payout amount, rejection, settlement, repudiation, anything above a configured amount) must have **individual human approval** — by both Ugandan law and IIP's own contract.

## Evidence

### 1. Uganda Data Protection and Privacy Act, 2019
- **Automated decision-taking:** a data subject has the right *not to be subject to a decision which significantly affects them based solely on automated processing.* → A **status notification** ("we received your claim / it is under review") is **not a decision that significantly affects** the data subject, so this provision does **not** bar auto-sending it. It **does** bar auto-deciding a claim — which IIP already routes to a human (`ApprovalDecisionGuard`). ([Act 9 of 2019, ulii](https://media.ulii.org/media/legislation/18002/source_file/b6ae5cce4290322a/2019-9.pdf))
- **Direct marketing:** data subjects may object to processing for *direct marketing*. A transactional update about the customer's **own claim** is not direct marketing, so the marketing-consent regime does not gate it. ([Act 9 of 2019, ulii](https://ulii.org/akn/ug/act/2019/9/eng@2019-05-03/source))
- **Consent** = freely given, specific, informed, unambiguous. A customer who lodges a claim via a phone number provides a lawful basis for **transactional** contact on that number. ([DPPA overview, Securiti](https://securiti.ai/uganda-data-protection-and-privacy-act/))

### 2. UCC — Text and Multimedia Messaging Regulations, 2019 + Electronic Transactions Act, 2011
- *"A person shall not use an automated system to send an **unsolicited** SMS or MMS to a subscriber."* The prohibition targets **unsolicited** (promotional/value-added) messages. A claim-status update to a customer who **initiated** the claim is **solicited/transactional**, not unsolicited. ([UCC Text & Multimedia Messaging Regulations 2019, PDF](https://ucc.co.ug/wp-content/uploads/2023/10/THE-UGANDA-COMMUNICATIONS-TEXT-AND-MULTIMEDIA-MESSAGING-REGULATIONS-2019.pdf))
- The Electronic Transactions Act 2011 requires opt-in consent + an opt-out option for unsolicited communications; UCC mandates a functional opt-out (uniform `*196#`). → **Even transactional SMS should honor opt-out.** ([UCC on unsolicited messages](https://uccinfoblog.wordpress.com/2019/08/07/how-to-avoid-unsolicited-messages/))

### 3. IIP internal contract (AGENTS.md)
- §4: AI must never be final actor for consequential decisions; "send final regulated communications without required human approval" is prohibited. A transactional acknowledgement is **not** a final regulated communication; a claim-decision SMS **is**.
- §16: tenant policy already anticipates bounded auto-send fields (`AutoDraftCustomerCommunicationsEnabled`, `AutoSendRequiresApprovalAboveAmount`) that may only **narrow** the platform ceiling. This supports a **tenant-opt-in, conservative-default** auto-send for transactional messages.
- The draft pipeline (`AzureOpenAiCustomerUpdateDraftingProvider`) already computes `RequiresApproval` (true for sensitive content, ReadyForReview, EscalationReceived, provider fallback) — a ready-made **consequential signal** the send guard should honor.

### 4. Global norm (corroboration)
- Transactional/service messages are universally carved out from marketing-consent regimes (e.g. TCPA/GDPR transactional carve-outs). Auto-sending transactional notifications is standard and expected; auto-sending decisions/marketing is not.

## Recommended policy (tiered, not binary)

An outbound SMS may **auto-send** only if **all** hold; otherwise **human approval required**:
1. It is a **system-generated transactional status notification** from a bounded, enumerated category (claim received / documents received / under review / more info needed) — **not a decision**.
2. It uses an **approved template** (controlled, customer-safe content).
3. It carries **no consequential decision** and no sensitive content (no payout amount, reserves, adverse outcome, fraud language).
4. **Consent + opt-out** are honored for that number.
5. **Tenant policy** has enabled transactional auto-send for that category (**default OFF**).

Anything else — especially any communication of a claim decision, payout, rejection, or a message above a configured amount — requires individual human approval.

## Implication for the code

**Do not loosen `OutboundSmsGuard` to `LowRisk` alone.** A faithful implementation needs:
- a **category / consequential signal** on the SMS path — reuse the draft pipeline's `RequiresApproval` rather than a bare `LowRisk`;
- a tenant policy flag (e.g. `AutoSendTransactionalSmsEnabled`, default false);
- guard logic: `permit = request.Approved || (autoSendEnabled && !requiresApproval && isTransactionalCategory)`.

Until that plumbing exists, the current conservative default (`Approved` only) is the correct, compliant behavior.

## Sources
- [Uganda Data Protection and Privacy Act, 2019 (Act 9 of 2019)](https://media.ulii.org/media/legislation/18002/source_file/b6ae5cce4290322a/2019-9.pdf)
- [DPPA as at 3 May 2019, ulii](https://ulii.org/akn/ug/act/2019/9/eng@2019-05-03/source)
- [UCC Text and Multimedia Messaging Regulations, 2019 (PDF)](https://ucc.co.ug/wp-content/uploads/2023/10/THE-UGANDA-COMMUNICATIONS-TEXT-AND-MULTIMEDIA-MESSAGING-REGULATIONS-2019.pdf)
- [UCC — how to avoid unsolicited messages (opt-out `*196#`)](https://uccinfoblog.wordpress.com/2019/08/07/how-to-avoid-unsolicited-messages/)
- [DPPA guide, Securiti](https://securiti.ai/uganda-data-protection-and-privacy-act/)
