# IIP omnichannel channel expansion plan

| Field | Value |
|---|---|
| Status | Implementation in progress, not a release commitment |
| Date | 2026-10-29 |
| Scope | Add Instagram Direct, Facebook Messenger, TikTok Business Messaging, X Direct Messages, and Telegram Bot messaging as optional connections beside existing channels. Keep WhatsApp working. LinkedIn is excluded. |
| Authority | `AGENTS.md`, `DESIGN.md` and its owning documents, the product requirements, and `IIP-CONSOLIDATED-ROADMAP.md` remain controlling. |

## Outcome and boundary

An authorized insurer connects an eligible business account, sees its real status in the existing channel settings, and handles permitted customer conversations in the existing Inbox. One assistant and one human handover policy serve supported channels. A channel may have different allowed actions, message types, account permissions, and response windows. An unavailable permission or feature shows `Not configured` or a specific connection limitation; it never appears live.

This is an optional expansion after the existing Release 1 WhatsApp obligation and its live provider, consent, configuration, legal, and proof gates. Do not represent this proposal as a change to the consolidated roadmap's current work order. No platform is promised for launch until a real account and approved API access prove its capabilities.

## Architecture assessment from the current repository

**Shared message store and Telegram messaging slice (2026-10-31):** A tenant-scoped `ChannelMessages` table stores normalized messages with provider-event and idempotency uniqueness. Telegram account saves require a public HTTPS API base URL and register a webhook with a generated secret encrypted with the bot token. The webhook resolves the bot's workspace from its indexed external account ID, verifies Telegram's secret header in constant time, accepts private updates, deduplicates provider update IDs, records customer-initiated consent, honors `/stop`, and audits metadata without message content. Staff with both communication permissions can send human-approved replies from the shared Inbox; sends are persisted before provider calls, idempotent, consent checked, and an unknown outcome blocks another send. Telegram AI handling, attachments, richer delivery reconciliation, and live end-to-end validation remain unfinished. The new database migrations have not been applied.

**Meta messaging slice (2026-11-01):** Instagram and Messenger now have a shared public webhook route, per-account verify token handshake, Meta `X-Hub-Signature-256` verification, customer-initiated text normalization, event deduplication, consent capture and opt-out, shared Inbox threads, and human-approved text replies. Replies require both communication permissions, a current consent grant, the same connected account, and a customer message within Meta's 24-hour window. Account configuration shows the exact callback path and waits for a signed provider event before reporting the webhook active. External Meta app setup and webhook subscription remain manual. Media, delivery/read receipts, AI replies, live account proof, and migration application remain unfinished.

**TikTok and X adapter status (2026-11-01):** The shared Inbox send service has locally tested text send adapters for TikTok Business Messaging and X Direct Messages. TikTok uses the documented conversation recipient/text request shape and parses the message ID; X uses the v2 DM endpoint with user-context OAuth 2.0 bearer authorization. X also has a public inbound X Activity API webhook route that handles CRC, verifies the raw-body HMAC signature, scopes the event to the configured account, deduplicates retries, captures consent/STOP, and is covered by local endpoint tests. X connection settings show the callback path and event subscription instructions. These changes do not activate either platform: TikTok inbound is not implemented because its Business Messaging event and signature contract still needs first-party confirmation; neither platform has account-specific staging proof. TikTok Business Messaging access, data/privacy review and eligible business account remain external gates; X DM access tier, developer app, scopes, XAA subscription and user authorization remain external gates. Keep both channels visibly unavailable until provider setup and a full staging exchange pass.

**First implementation slice (2026-10-29):** the Inbox now reads a channel-neutral, tenant-scoped thread contract for retained WhatsApp messages, inbound email previews, and normalized messages stored for new channel adapters. The contract audits reads and reports whether the viewer may use the existing WhatsApp reply action. The old WhatsApp thread and send routes remain supported.

**Account configuration slice (2026-10-29):** Settings and Integrations link to the existing governed Connections screen. Administrators can enter, replace, recheck, and remove one account per workspace for Instagram, Messenger, TikTok, X, or Telegram. A fixed provider identity endpoint validates each account before secrets are encrypted and saved; account ID and safe profile data are shown. These identity checks do not establish platform messaging permissions or delivery. Telegram inbound is documented in the later 2026-10-31 slice; Meta inbound/replies and X signed inbound are in the 2026-11-01 slice; TikTok inbound remains unavailable. The existing one-connection-per-type key is a deliberate interim constraint; it does not fulfill the target multi-account model. A future migration must add multiple account keys before that capability is offered.

| Existing asset | Current state | Build implication |
|---|---|---|
| Channel overview | `ChannelQueryService` and `EfChannelReadRepository` supply a tenant and workspace scoped Inbox overview. The read repository projects WhatsApp, inbound email, and normalized messages from the new channel store. Unknown channel values are normalized to `unknown`. | Add explicit supported channel values and a common conversation projection. Reject or label unknown values safely rather than misclassifying them as email. Keep existing response fields during migration. |
| Inbox | `web/portal/lovable-pages/Channels.tsx` shows several channel labels, but thread loading, reply, control, and customer insight actions use WhatsApp-specific hooks and IDs. | Extend the existing Inbox through a versioned conversation API. Show only actions supported by the selected connected account. Do not create a second social inbox. |
| WhatsApp | `WhatsAppIntegrationEndpoints`, `WhatsAppMessagingService`, `CustomerAssistantService`, and `WhatsAppMessage` implement signed webhooks, stored messages, deduplication, staff replies, consent checks, response-window enforcement, AI replies, and handover. | Preserve the live WhatsApp contract and history. Extract shared meaning carefully; keep provider parsing, credentials, templates, and policy in its adapter. Do not make the new path silently call an old path. |
| Connections | `IntegrationConnection` covers SMS, email, policy system, translation, and one verified account each for Instagram, Messenger, TikTok, X, and Telegram. WhatsApp has separate tenant configuration and validation. | Introduce an account connection model keyed by tenant, workspace, platform, and external business account before supporting multiple Pages or business accounts. Validate messaging permissions as well as identity before activation. Keep the current one-account limitation visible. |
| AI and handover | `CustomerAssistantService` and `ConversationControl` contain useful behavior but are named and typed for WhatsApp. | Move only channel-neutral intent, grounding, lead/claim guidance, audit requirements, and human control into Application/Domain services. Pass channel-specific send eligibility and content limits in through ports. Preserve AI's advisory role and case approval gates. |
| Consent and communications | Consent purposes and WhatsApp staff-send checks exist. `CommunicationChannel` is a separate workflow enum for portal, email, SMS, WhatsApp, and Teams. | Define purpose-specific, channel-specific consent and withdrawal for outbound engagement. Coordinate contract changes across the communications workflow; do not assume an Inbox channel is automatically a permitted outbound channel. |

## Target shape

1. **Connection and capability:** a tenant and workspace scoped `ChannelConnection` records platform, external business account, authorization state, approval state, token health, webhook health, and verified capabilities. A versioned capability policy records supported operations and current provider restrictions. Server checks this policy at each send, not just when drawing a button.
2. **Inbound:** each official adapter verifies webhook authenticity, resolves exactly one configured business account to a tenant and workspace, parses untrusted content, and writes a durable normalized event. A unique provider-event key makes retries safe. Attachments pass through size, type, malware, retention, and access controls before becoming evidence.
3. **Conversation:** `ChannelIdentity` is the platform-scoped external user ID. `Conversation` belongs to one tenant, workspace, connection, and channel identity. `Message` tracks direction, provider ID, content classification, delivery state, correlation, and provenance. A customer link is optional and carries its verification method, actor, timestamp, and audit trail. Matching display names never links identities.
4. **Assistant and human control:** common Application use cases classify intent, retrieve approved tenant knowledge, draft or send only within the configured automation gate, create provisional leads or claims, and escalate. Channel adapters supply supported media and reply formatting. Human takeover and handback are shared conversation state with a named accountable actor. AI never makes a final insurance decision.
5. **Outbound:** an eligibility service checks tenant permission, recipient and account identity, current consent by purpose, opt-out, platform permission, response window, template or message category, frequency, and content before queuing. An outbox records the intent and idempotency key. Provider acknowledgement, unknown outcome, delivery, and reconciliation remain distinct states. No automatic retry of an unknown send without reconciliation.
6. **Experience and reporting:** extend the current Inbox and channel settings. Show channel, connected account, handover, delivery, and eligibility reason. Aggregate metrics from actual message and lead events with per-channel attribution; no fabricated campaign or conversion numbers. Keep existing customer and claim details permission gated.

Do not add a separate service or queue product by default. First establish whether the current background work mechanism provides durability, retry, and recovery for the required volume. If it does not, choose the smallest supported durable mechanism and document its operational owner and rollback.

## Delivery slices and exit criteria

| Slice | Build work | Exit evidence |
|---|---|---|
| 0. Platform and product gate | Confirm target insurer accounts, jurisdictions, business use cases, approved app access, permission scopes, actual provider endpoints, data-use terms, pricing, message windows, media, webhook behavior, and retention. Decide which channels are usable for service replies and which, if any, permit proactive campaigns. | A signed capability matrix per platform and target account; unresolved capabilities marked unavailable. Product owner selects a first pilot account. |
| 1. Shared contracts and migration | Define versioned connection, identity, conversation, message, consent, capability, and audit contracts. Add tenant/workspace scoped storage and uniqueness constraints. Design a WhatsApp history projection or backfill with reconciliation and rollback. Map old thread IDs to new IDs without losing deep links. | Existing WhatsApp histories, API clients, handover, and consent behavior still work. Backfill counts and sampled thread histories agree. No second source of truth remains after cutover. |
| 2. First new channel | Add one official Meta channel, preferably Instagram Direct if pilot account approval is available, otherwise Messenger. Deliver connection, signed inbound, normalized text, Inbox read/reply, eligible AI response, staff takeover, send status, audit, and disconnect. | A real approved business account passes an inbound-to-human-to-reply flow in staging; unauthorized, cross-tenant, duplicate, expired-window, revoked-token, and provider-timeout cases fail safely. |
| 3. Second Meta channel | Add the other Meta messaging adapter through the same contracts. Share only genuinely common Meta authentication/webhook code; keep platform identity and policy separate. | Same live staging proof plus per-channel eligibility evidence and no regression in WhatsApp or the first Meta channel. |
| 4. TikTok and X gates | Pursue official account/app approval and review current APIs for the target market and plan. Build an adapter only for capabilities confirmed with an approved account. If access is absent, retain a documented connector boundary and honest status, with no fake receive or send path. | Each activated channel has real account, webhook/read, eligible response, audit, and recovery proof. Otherwise it remains unavailable. |
| 5. Identity, outreach, and analytics | Add verified cross-channel customer linking, unified search, actual source attribution, and consent-aware outbound workflows. Introduce campaigns only for a channel and message category with explicit permission, templates where required, suppression, approval, limits, cancellation, and reconciliation. | Staff can explain why each recipient is eligible, trace each send, withdraw consent, separate mistaken identity links, and reconcile counts to persisted events. |

Each slice is a coherent vertical change across Domain, Application, Infrastructure, API, portal, migration, audit, documentation, and affected checks. Feature flags are scoped per tenant and connection, with named owner and removal condition. Do not ship dead controls for later slices.

## Interface placement

- **Settings > Channels:** show existing connections plus eligible new channel types. An administrator opens a platform-specific connection flow, sees required business account and permissions, validates against the provider, and sees connected, pending approval, action required, or `Not configured` with a reason. Only real configured accounts are selectable.
- **Inbox:** keep the current list, thread, and customer panel. Add a channel filter and account label when they help disambiguate conversations. A thread offers reply, handover, attachment, and AI actions only when permitted for that conversation. A blocked reply explains the specific rule and safe next action.
- **Campaigns:** add a channel selector only after an outbound-eligible channel is proven. Unsupported channels do not appear as sendable choices. A preview shows audience, consent, policy exclusions, approval, and estimated eligible count from real records.

All screen work starts with `DESIGN.md` and its owning documents and must clear the UI craft pre-merge checklist. Open changed screens in a browser before declaring them ready.

## Decisions to record before implementation

1. Pilot insurer, market, and first approved business account; this determines the first non-WhatsApp adapter.
2. Whether the first release is inbound service only or includes a specifically authorized outbound campaign use case. Default to inbound service.
3. Conversation source of truth, backfill method, old-to-new thread mapping, cutover point, and rollback.
4. Minimum identity-link verification and unlink/recourse workflow.
5. Automation policy per intent and channel, including who approves customer-facing answers and how unresolved messages enter a staffed queue.
6. Durable event and send processing choice, retention, replay, reconciliation, and operational ownership.

## Acceptance and verification plan

For each activated channel, check provider authentication and webhook verification; account-to-tenant mapping; duplicate and out-of-order events; safe media handling; unsupported content; channel-specific response eligibility; consent and withdrawal; restricted insurance assertions; staff takeover; idempotent sends; unknown provider outcomes; token expiry; rate limits; tenant separation; permissions; safe audit metadata; export and deletion duties; and honest UI states. Run the affected backend and portal checks from `AGENTS.md`, review the diff and migrations, open the changed UI in a browser, and obtain a real staging exchange with the approved business account. Source-complete, locally verified, and staging-validated are distinct statuses.

## Current official platform references

These references establish available product surfaces, not approval for IIP or a particular insurer account. Recheck them at Slice 0 and at each adapter build.

- [Meta Instagram API messaging collection](https://www.postman.com/meta/instagram/documentation/6yqw8pt/instagram-api)
- [Meta Messenger Platform API collection](https://www.postman.com/meta/messenger-platform-api/documentation/iyp204x/messenger-platform-api)
- [TikTok Business Messaging API getting started](https://business-api.tiktok.com/portal/docs/business-messaging-api-get-started/v1.3)
- [X API documentation](https://docs.x.com/x-api/introduction)
- [Telegram Bot API](https://core.telegram.org/bots/api)
