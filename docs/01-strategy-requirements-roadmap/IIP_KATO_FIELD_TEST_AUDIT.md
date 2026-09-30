# Kato field test readiness: audit and build brief

Read-only audit of the IIP WhatsApp assistant against the Kato persona and an unsupervised field test with shopkeepers, market traders and boda riders in Kampala, plus IRA microinsurance research and the build order to get there.

- Audited: 30 September 2026, branch `feature/claim-intake`, dev environment `rg-iip-dev`
- Evidence: source code, live dev knowledge index (read-only), live dev app settings (read-only)
- The WhatsApp transcript referenced in the brief was not attached; where it would confirm a cause, the reply text or audit event to look for is named.
- Companion document: [IIP_WHATSAPP_VOICE_DESIGN.md](IIP_WHATSAPP_VOICE_DESIGN.md)

---

## Target behaviour (from the brief)

- The bot becomes **Kato**, Nile Shield's helper on WhatsApp. On first contact he introduces himself, says in plain outcome language what he can do ("if you have cover with us and something has happened, I can help you get your money"), and says the person can text or send a voice note in whichever language they prefer. Kato guides; the user never has to work out what to do next.
- Kato never claims to be a person and never performs humanity, but does not announce himself as a machine either. He always offers a route to a human and answers honestly if asked directly whether he is a person.
- No policy: Kato asks what they do for a living and explains which microinsurance product would suit them.
- Identity off the phone number: name from the Meta WhatsApp profile if available, otherwise ask, then save it against the number as a customer record so policies resolve without reciting a policy number.
- One phone is shared across participants: a hidden `#reset` command clears the customer record and closes open demo claims between sessions.

## Bottom line

Today's bot is a claims assistant for people who already hold traditional cover, with brittle language handling. None of the Kato behaviour exists yet: no introduction, no occupation question, no microinsurance products, no Meta profile name, no `#reset`, no voice.

On a shared phone with strangers, two defects would show up within minutes:

1. **Duplicate claims:** two separate code paths open claims and neither checks for an existing one.
2. **Nobody recognised:** once two participants' names are saved against one number, the identity rule refuses to recognise anyone on that number.

---

## 1. Architecture

```text
Meta webhook POST /api/v1/integrations/whatsapp/webhook
 '- WhatsAppIntegrationEndpoints.ReceiveWebhook                (Api/Integrations)
     |- WhatsAppInboundDocumentIngestionService.IngestWebhookPayloadAsync
     |    verifies X-Hub-Signature-256; for images/documents: downloads media,
     |    matches to a claim (RuleBasedWhatsAppToCaseMatcher) and CAN CREATE A CLAIM
     |- WhatsAppWebhookParser.ParseMessages                     (Infrastructure/WhatsApp)
     |    text / image / document; everything else, including audio, becomes "Unsupported"
     |- WhatsAppMessagingService.IngestInboundAsync             saves messages, dedupes by id
     |- answers Meta 200 immediately
     '- WhatsAppAssistantQueue (in-memory Channel) -> WhatsAppAssistantWorker (3-minute budget)
          '- CustomerAssistantService.HandleInboundMessagesAsync (Application/CustomerAssistant)
               |- agent kill switch        IAgentGate
               |- consent record           ConsentRecord (customer-initiated)
               |- who is writing           IdentifySenderAsync (phone match over all customers)
               |- conversation state       CustomerConversationTurn rows + ConversationContinuation (30 min)
               |- language                 CustomerLanguageResolutionService, LanguageWordListScorer,
               |                           TranslationService -> AfriSlmTranslationProvider
               |- intent                   KeywordCustomerIntentClassifier, then Azure OpenAI fallback
               |- decide                   DecideAsync -> new claim, claim status, policy question,
               |                           buy cover (lead), human, greeting, clarify
               |    |- claims              CaseIntakeService.CreateCaseAsync        (Domain/Cases)
               |    |- policy answers      AzureAiSearchRetrievalProvider + AzureOpenAiAssistantProvider
               |    |                      + CustomerAutoReplyGate
               |    '- portal link         WhatsAppClaimantAccessLinkIssuer        (Api/ClientPortal)
               |- translate reply back     LocaliseAsync (+ machine-translated note)
               '- send                     WhatsAppCloudApiOutboundProvider
          '- new claims -> ClaimTriageQueue (automatic AI review for staff)

Portal: link -> web/portal /access/verify -> ClaimantAuthenticationEndpoints (redeem, 24 h session)
        -> lovable-pages/ClaimAccessStatus.tsx, backed by ClientPortalEndpoints
```

- **Exists:** clean layered pipeline, honest fallbacks, audit on every turn, slow work off Meta's request.
- **Weaker than it looks:** the queue is in memory, so an API restart drops messages not yet answered; almost all decisions sit in one ~1,500-line class.
- **Gap:** no architectural blocker; the work is in the assistant class, the data model and content.

## 2. Identity and customer records

**Exists**

- `Customer` is `(CustomerId, TenantId, WorkspaceId, FullName, Email, Phone)`. Phone is a plain field, not a key. Every message loads all customers and matches by number (`IdentifySenderAsync`).
- Policies resolve via `policyRepository.ListByCustomerAsync`. A sender not on file can name a policy by typing a number matching `NSA/MO/2026/00033`.
- Two different names on file for one number: nobody is recognised and the bot asks for a policy number (deliberate, for shared family phones).
- Customers are created only by the demo seeder or staff; the assistant never creates one.

**Missing**

- **Meta profile name is never read.** `WhatsAppWebhookParser` ignores `contacts[].profile.name`. It is user-set, so fine for a greeting, not proof of identity.
- No ask-name-then-save step; nowhere to keep occupation. `Customer` has no occupation, language or source fields; `ICustomerRepository` has no delete.
- No `#reset`. Closest is data-subject erasure (`DataSubjectRightsService.RequestErasureAsync`), which anonymises the customer ("Erased customer", phone cleared). It does not clear language preference, conversation turns or claims.
- Nothing issues a policy; a new participant can only be a lead unless demo data seeds one.

**Why it matters for the field test:** saving each participant's name against the shared number makes it "two different people" after the second participant, and from then on nobody is recognised. `#reset` is a precondition for the identity flow.

**Note:** the customer table is relational, so adding an occupation column needs a migration; storing occupation on the conversation turn or lead avoids that.

## 3. Knowledge base

Live dev index `insuranceintelligence-knowledge`, read-only:

| Document | Product line | Type | Chunks | Characters |
|---|---|---|---|---|
| Nile Shield Assurance, Motor Policy Wording (v1.1) | Motor | PolicyWording | 6 | 3,567 |
| Nile Shield Assurance, Medical Policy Wording (v1.1) | Medical | PolicyWording | 5 | 2,804 |
| Nile Shield Assurance, Life Policy Wording (v1.1) | Life | PolicyWording | 6 | 3,052 |
| Nile Shield Assurance, General Policy Wording (v1.1) | General | PolicyWording | 6 | 3,148 |
| Nile Shield Assurance, Agriculture Policy Wording (v1.1) | Agriculture | PolicyWording | 7 | 3,996 |

- **Confirmed all traditional insurance.** 5 documents, 30 chunks, about 16,500 characters total.
- **English only.** No Luganda or Kiswahili, no FAQs, no claim guides, no microinsurance.
- **Retrieval is not product-aware.** The index supports a product-line filter but the WhatsApp path never passes one. Plain keyword search, top 12. "What does Motor insurance cover" returns Motor chunks mixed with Agriculture, Medical, Life and General.

**Needed:** for each microinsurance demo product, a short policy wording and a plain-language key information summary, loaded as approved documents with a product line; pass the sender's product line into retrieval (`AssistantQuestion.ProductLine`).

## 4. Claims

**Where claims are created**

| Path | Trigger | Checks for existing claim? |
|---|---|---|
| `CustomerAssistantService.HandleNewClaimAsync` | Text classified as new claim (keywords "accident", "damaged", "crash", or the model), policy resolved, `CustomerMessageSignals.HasEnoughIncidentDetail` | **No** |
| `WhatsAppInboundDocumentIngestionService` | Photo/document from a recognised sender with no open claim. Creates a "WhatsApp Intake" claim on the **first policy in the list** (may be Medical) | Only "any open claim on your policies" |

**Why three motor claims on one policy:** the text path has no duplicate check. Once a claim is opened, the 30-minute continuation no longer applies, so any later message mentioning the accident again ("I had an accident yesterday, what documents do I need?", or a Luganda message translated to "accident") opens a new claim. A photo sent before describing the accident can add another through the ingestion path.

- **Confirmation step:** none with the customer. `RequiresConfirmation: true` means staff confirm later (SLA waits). The customer is told the claim is opened immediately.
- **Classification:** none on the WhatsApp path. `ClaimType` is hard-coded "Unclassified, reported via WhatsApp", which is the UNCLASSIFIED tag. `ClaimTypeClassifier` only understands Motor, from structured motor fields WhatsApp never fills.
- **0% completeness:** no required document received yet.
- **Document requirements:** from the pinned product's `DocumentMatrix` (`ProductDocumentRequirement`: key, label, required, stage), else hard-coded per-line lists in `DocumentChecklistRules`. The WhatsApp message lists at most four (`DocumentsToRequestAsync`, `Take(4)`). **Nothing depends on claim value or claim type**, and the WhatsApp flow never asks for an amount.

**To make requirements configurable by product and claim value:** add optional conditions to `ProductDocumentRequirement` (claim type, benefit, min/max claimed amount); evaluate in `DocumentChecklistRules.ForProduct`; capture amount or benefit at intake; expose the fields on the product admin screen.

## 5. Language

- **Detection:** hand-written word lists in `LanguageWordListScorer` (~70 Luganda, ~85 Kiswahili words), scored by share of matching words. English via `TranslationQualityJudge.LooksLikeEnglish`: at least 4 words and 3 hits from a ~30-word English list.
- **Storage:** only when the customer explicitly asks ("reply in Luganda") or answers the language menu. Kept 90 days per number (`LanguageResolutionOrder.PreferenceFreshness`).
- **Translation:** a model. AfriSLM (0.8B, llama.cpp) on CPU in Container App `ca-afrislm-dev`, **West US 2**, 1 replica always on, 2 vCPU. Output passes a mechanical quality judge. The Sunbird replacement is built locally but uncommitted and undeployed.
- **Intent** is always decided in English after translation, by English keyword lists.

**Why the Luganda phrases fall through**

| Message | What the code does |
|---|---|
| "Gyebaleko" | List has *gyebale*, not the usual one-word *gyebaleko*; greetings list has only two-word "gyebale ko". Zero hits, not a greeting, too short for the model classifier. Result: "Sorry, I didn't quite catch that." |
| "Mbadde ngude mu kabenje" | Only *mu* matches; list has *akabenje* but not locative *kabenje*, nor *mbadde*, *ngude*. One hit in four words = exactly the 0.5 threshold, so it goes to AfriSLM. Whether the translation contains an English keyword like "accident" depends entirely on AfriSLM. A weak or rejected translation ends in clarify or hand-off. |
| "Mbadde njagala kulopa akabenje" | Only *akabenje* matches; same threshold case. *Kulopa* ("to report", from English) is exactly the kind of word a 0.8B model mistranslates. |

**The footer:** "(Obubaka buno bwavvuunulwa kompyuta)" is `MachineTranslatedNote` ("This message was translated by a computer."), itself machine translated. `LocaliseAsync` appends it to every translated reply with no memory of having said it, and it costs a second translation call per reply.

## 6. Capability consistency

Both "what do you offer in motor insurance" and "what does Motor insurance cover" start with "what", so both classify as a policy question. Run against the live index exactly as the bot does, **each returns 12 chunks, Motor first**. So retrieval and the "fewer than two chunks" refusal rule do not explain the difference.

The language layer does:

- "what **do you** offer in motor insurance" hits *what*, *you*, *insurance* on the English list: 3 hits, recognised as English, never translated.
- "what **does** Motor insurance **cover**" hits only *what*, *insurance* (*does*, *cover* not on the list): 2 hits, **not** recognised as English.

With a stored language preference (likely after the Luganda messages and a language-menu answer), the English sentence is treated as Luganda and sent to AfriSLM for Luganda-to-English translation. A rejected translation gives "I'm not able to help with that directly, I've passed this to our team": a refusal plus escalation.

**Cause:** intent routing via the language layer (session state plus a too-small English word list), not retrieval, not a confidence threshold.

**Confirm from data:** audit event `customer_assistant.translation_failed` near 6:34 AM, or a conversation turn with `InboundTranslationStatus = FellBackToEnglish`. If the reply was instead "Thank you for your question... I've passed it to our team to confirm the answer", the answer gate refused it; check that turn's citations and model call.

**Fix:** a message with no local-language signal is English even when a preference exists.

## 7. Voice

**None.** No audio parsing, download, conversion, speech to text, text to speech or Sunbird speech integration. A voice note is labelled `Unsupported` and skipped, so **the customer gets no reply at all**.

Reusable: the media download used for photos (`IWhatsAppInboundProvider.DownloadMediaAsync`), and locally (undeployed) the Sunbird connection and the typing indicator. Full design in [IIP_WHATSAPP_VOICE_DESIGN.md](IIP_WHATSAPP_VOICE_DESIGN.md).

## 8. Access links and documents

- **Generation:** 32 random bytes, SHA-256 hash stored, single use; redeeming creates a 24 h session scoped to one claim. Link lives 24 h (`WhatsAppClaimantAccessLinkIssuer.LinkLifetime`).
- **Verification before issuing:** only that the conversation came from that WhatsApp number and the claim exists. **For a sender not on file, typing a valid-looking policy number is enough to open a claim on someone else's policy and receive a portal link to it.** Policy numbers are printed on certificates and follow a guessable pattern.
- **Uploads:** through the API, validated, stored in Azure Blob, classified and extracted by Document Intelligence.
- **Encryption at rest:** Microsoft-managed keys only; storage account still allows shared-key access. Tenant credentials are encrypted with ASP.NET Data Protection keys in the app's default file store, not Key Vault.

## 9. Tone and response generation

- **Mostly templated.** 24 outcome templates and about 70 long English strings hard-coded in `CustomerAssistantService.cs` (welcome, claim opened, status, clarify, hand-off, holding reply). Only policy answers (Azure OpenAI over retrieved chunks) and the intent fallback are model-generated.
- **Persona for model text** can be set per tenant via the Agent Behaviour Profile (Admin > Agents) on top of a locked platform preamble. It does not reach the hard-coded templates.
- The insurer's name appears in no customer-facing reply ("our claims team"). Asked directly, the bot says it is "the automated assistant for our claims team" and offers a person, which matches Kato's honesty rule.
- **To become Kato:** rewrite the templates in outcome language as Kato, Nile Shield's helper. Do not route every reply through a model "rephrase as Kato" step; that reintroduces the drift the verbatim translation work removed.

## 10. Demo data

- `DemoSeedService.SeedAsync`: traditional composite insurer "Nile Shield Assurance": 5 products, 50 customers, policies, claims in every workflow state. Products carry made-up references like `IRA/DEMO/MOTOR/2026`.
- `SeedTestPolicyholderAsync` (form under Admin > Tenant Policy): gives a phone number Motor Comprehensive + Medical Gold policies and two closed past claims; repeatable.
- Nothing seeds knowledge documents, microinsurance products, or an open claim mid-way.

**Needed:** published microinsurance products with document matrices; their wordings and key-information summaries in the knowledge base; the demo phone seeded with those products; `#reset`; optionally one demo policyholder with a claim at "documents requested" so "where is my claim?" has something to show.

---

## Research: IRA microinsurance rules

The instrument on the IRA's regulations page is the **Interim Micro Insurance Guidelines, 2016**, alongside a 2025 compliance circular for microinsurance organisations. Quoted:

- "'micro-insurance policy' means an insurance policy where the **sum insured /assured does not exceed UGX 10 million** and has been specially approved by the Authority as such and qualifies for VAT exemption."
- 4.1 "No micro insurance product shall be launched or sold without the approval of the Authority." 4.2 "All micro insurance products shall be clearly/ visibly labeled as such."
- 4.3 approval considers: nature of product, cover type, sum insured, maximum contract term, **waiting periods, exclusions, grace period, policy documentation**, premium basis, actuary premium sign-off.
- 5.2 "A new policyholder must receive confirmation of his status as a policyholder **within 7 working days** of signing up for the policy together with **Key Information concerning the product in a simple language**."
- 7.1 to 7.3: claims settled under the IRA's Insurance Claims Guidelines; "The M.I.O shall offer **reasonable guidance to help a policyholder make a claim** and appropriate information on its progress shall be given"; no unreasonable rejection.
- 8.2 complaints reported quarterly; 9.1 register of everyone covered, premiums, cover period, claims reported.
- Insurance Act 2017: microinsurance is "insurance for the protection of low-income people against specific perils in exchange for regular premium payments proportionate to the likelihood and cost of risk involved".

No premium ceiling appears in the guidelines. The referenced "Insurance Claims Guidelines" were not in the IRA's current listing, so claim-evidence rules in them could not be checked.

**For demo products:** every sum insured at or under UGX 10 million; labelled as microinsurance; key-information summary states waiting period, exclusions and grace period; plain-language confirmation after sign-up. Kato's "I will help you get your money" is the "reasonable guidance to make a claim" duty.

## Research: real products in the market

| Product (market) | Premium | Benefit | Claim evidence |
|---|---|---|---|
| **Hospital Sente**, Airtel Money + Prudential + Turaco (Uganda) | UGX 1,000/month; UGX 5,000/month | UGX 40,000 per hospital night + UGX 1M funeral; UGX 100,000 per night + UGX 4M funeral | Guided over WhatsApp; exact list not published. Hospital cash normally needs admission/discharge note and ID |
| **Turaco** hospital cash and funeral (Uganda, Kenya) | from UGX 2,000/month (~US$2 average) | Hospital cash UGX 200,000 to 1M/year; funeral UGX 500,000 to 2M | "Minimal documentation", forms via WhatsApp, average under 3 business days |
| **MTN + Sanlam** life and hospitalisation (Uganda) | from UGX 500/month | Customer plus up to six family members | Not published |
| **SafeBoda Plus Ride** (Uganda, riders and passengers) | UGX 6,000/month or 72,000/year | Up to UGX 1M; UGX 2.5M goods, permanent disability or death; UGX 50,000/day up to 15 days off work | Not published; injury normally needs police report and medical evidence |
| **SanlamAllianz Traveller Accident** (Uganda, all road transport) | from UGX 1,300 | Up to UGX 3.15M: death 1M, disability 1M, medical 650,000, funeral 500,000 | Not published |
| **Motor Third Party**, compulsory for every boda | ~UGX 70,000 to 80,000/year incl. UGX 35,000 stamp duty | UGX 1M per person, UGX 10M per accident | Police report, claim form |
| **Credit life** via SACCOs/MFIs (Uganda) | ~1% of the loan, deducted from it | Outstanding loan paid to lender on death or permanent disability | Death certificate or burial permit |
| **Funeral cover** (e.g. ICEA LION, Uganda) | varies | Lump sum for burial | Claim form, burial permit, LC1 letter or birth notification for a child; death certificate for ongoing payments |
| **Micro property / stock** (e.g. APA "Commercial Micro", Kenya) | not published | Fire and perils, burglary with forced entry, theft, political violence | Normally police report or fire report, stock list |

**Demo product set** (all under UGX 10M, labelled as microinsurance):

1. **Hospital cash and funeral** (everyone): UGX 1,000 or 5,000/month; UGX 40,000 or 100,000 per night; UGX 1M or 4M funeral. Evidence: discharge note or admission letter; burial permit or LC1 letter.
2. **Boda rider personal accident with daily income** (riders): ~UGX 6,000/month; UGX 50,000/day off work up to 15 days; UGX 2.5M death or permanent disability. Evidence: police report, medical form.
3. **Market stall stock cover** (shopkeepers, traders): sum insured UGX 1M to 5M, fire and burglary. Evidence: police or fire report, stock list, photos. Premium for the insurer's actuary to set; no published Ugandan price found.
4. Optional: **credit life** through a SACCO, ~1% of the loan.

**Occupation to product:** boda rider -> 2 + 1; trader or shopkeeper -> 3 + 1; anyone else -> 1. Explaining an insurer's own products is fine for the insurer's own assistant; recommending across insurers or completing a sale by bot is intermediation.

---

## Surprises

- **A voice note gets no reply at all.** Silence, not a refusal.
- **Photos can open claims on the wrong policy.** First policy in the list, possibly Medical.
- **A typed policy number is a key to someone else's claim,** from any phone.
- **Customer messages are translated in the United States** (AfriSLM in West US 2): cross-border processing under s.19 of the Data Protection and Privacy Act without specific consent.
- **Made-up IRA references are visible** on seeded products (`IRA/DEMO/MOTOR/2026`).
- **The newest fixes are not live:** Sunbird translation, typing indicator and the "Want to ask about your claim?" portal copy are uncommitted on the dev machine.
- **A restart loses messages:** the assistant queue is in memory.

---

## Build order for the field test

All items are in scope. Build in this order so the phone is safe to hand over at every step.

1. **`#reset`** (hidden command, exact match, before any other handling): anonymise every customer record on the sending number (reuse `DataSubjectRightsService` erasure semantics), clear the language preference, mark the thread's conversation state so continuation starts fresh, close the number's open demo claims with an audit reason, and reply with a short confirmation. Never reachable by anyone but the demo number in the Demo profile.
2. **Stop duplicate claims:** in `HandleNewClaimAsync`, if the sender has an open claim on the same policy created in the last 72 hours, add the new detail to it and ask whether this is a new incident before opening another. Turn off claim creation from photos in `WhatsAppInboundDocumentIngestionService` (attach to the latest open claim or hold for a person instead).
3. **Kato's first contact:** introduction as Nile Shield's helper, in outcome language, text or voice note in any language, always a route to a person, honest if asked whether he is a person. Rewrite the hard-coded templates in `CustomerAssistantService.cs` in Kato's voice.
4. **Identity:** parse `contacts[].profile.name` in `WhatsAppWebhookParser`, carry it on `WhatsAppInboundMessage`; greet by it; if absent, ask; save a `Customer` against the number on first contact.
5. **No-policy path:** ask what they do for a living; map occupation to the demo products (section above); describe the product in plain words from the knowledge base; record interest as a lead for a licensed person.
6. **Microinsurance products and content:** publish the demo products (labelled, sum insured under UGX 10M, document matrices matching the evidence column), load their wordings and key-information summaries into the knowledge base with product lines, pass the sender's product line into retrieval, and seed the demo phone with them. Replace made-up `IRA/DEMO/...` references with a clearly-labelled demo marker.
7. **Voice:** at minimum, reply to a voice note instead of silence. Full voice in and out per [IIP_WHATSAPP_VOICE_DESIGN.md](IIP_WHATSAPP_VOICE_DESIGN.md).
8. **Language fixes:** treat a message with no local-language signal as English even with a stored preference; add missing Luganda forms and greetings (*gyebaleko*, *kabenje*, *mbadde*, *ngude*, *njagala*, *kulopa*); show the machine-translation note once per conversation.
9. **Link safety:** issue a portal link only when the sender's number matches the policyholder's, or after a second check.
10. **Document requirements by product and claim value:** conditions on `ProductDocumentRequirement`, evaluated in `DocumentChecklistRules.ForProduct`, amount or benefit captured at intake.
11. **Deploy and rehearse:** commit, deploy API and portal per `docs/05-deployment-release-enablement/AZURE_DEPLOYMENT_AND_ENVIRONMENTS.md`, apply migrations, switch translation to Sunbird if verified, and run full sessions on the demo phone with `#reset` between runs.

Later hardening (not needed for the field test): durable queue, customer-managed storage keys, Data Protection keys in Key Vault, product-aware Luganda knowledge content, editable per-language templates.

### Build outcome (2026-09-30)

Built from [IIP_KATO_BUILD_PROMPT.md](IIP_KATO_BUILD_PROMPT.md) on `feature/claim-intake` and deployed to dev (`rg-iip-dev`). Status against the list above:

- **Done and deployed:** 1 (`#reset`, plus `#demo` to load the demo policyholder), 2 (read-back before any claim, 72-hour duplicate check, photos never open claims), 3, 4 (profile name, ask when absent, contact record that holds no cover), 5, 6, 8 and 9 (link issued only when the sending number is the policyholder's, checked in the assistant and again in `WhatsAppClaimantAccessLinkIssuer`; a typed policy number unlocks nothing).
- **7, voice:** voice in, text out. `SunbirdVoiceNoteTranscriber` sends the OGG voice note as it is to Sunbird's `/tasks/audio/transcriptions` using the tenant's Sunbird connection, and the words are handled exactly like typed text. Without a working speech engine the customer gets a written "I can't listen to voice notes yet" reply. Voice out is not built.
- **Not done:** 10 (document requirements by claim value).
- **Also changed:** the WhatsApp missing-documents reminder no longer invites papers into the chat.

Dev configuration added for the field test:

- API app settings: `INSURANCEINTELLIGENCE_DEMO_PHONE_NUMBER` (the one number `#reset` and `#demo` answer) and `INSURANCEINTELLIGENCE_DEMO_POLICYHOLDER_NAME`. Both commands exist only under the Demo runtime profile.
- Translation connection: Sunbird (`sunflower-9b`), saved through `POST /api/v1/integrations/connections/translation`. The key is held encrypted in the connection, not in app settings.
- Microinsurance catalogue seeded with `POST /api/v1/admin/demo-seed/microinsurance` (idempotent; with `{"phone": ...}` it also loads the demo policyholder).

---

## Sources

- IRA Uganda: [Interim Micro Insurance Guidelines, 2016](https://ira.go.ug/cp/uploads/Micro_InsuranceGuidelines2016..pdf); [Regulations listing](https://ira.go.ug/regulations/); [2025 microinsurance compliance circular](https://ira.go.ug/download/circular-compliance-requirements-for-microinsurance-organisations-for-the-year-2025/)
- [Insurance Act, 2017](https://media.ulii.org/media/legislation/18566/source_file/c7ad460a562d3c9d/2017-6.pdf) (definition of microinsurance)
- Hospital Sente: [The Fintech Times](https://thefintechtimes.com/low-cost-hospital-cash-insurance-product-with-funeral-benefits-launched-in-uganda/), [TechMoran](https://techmoran.com/2023/03/23/turaco-airtel-money-launch-hospital-sente-to-provide-8-million-ugandans-with-health-insurance/)
- Turaco: [Insurance solutions](https://turaco.insure/insurance-solutions), [SenteGuide profile](https://senteguide.com/providers/insurance/turaco-micro)
- MTN and Sanlam: [Telecompaper](https://www.telecompaper.com/news/mtn-uganda-launches-life-hospitalisation-insurance-with-sanlam--1541104)
- SafeBoda: [SafeBoda](https://safeboda.medium.com/safeboda-passengers-to-get-1m-insurance-compensation-in-case-of-accident-a4e8a2bcec7b); SanlamAllianz: [SoftPower News](https://softpower.ug/sanlamallianz-unveils-ugandas-first-local-traveller-accident-insurance-cover/)
- Motor third party: [SenteGuide](https://senteguide.com/calculators/motor-insurance), [Uganda Insurers Association](https://uia.co.ug/faq/what-motor-third-party-insurance)
- Credit life: [IRA credit life flyer](https://ira.go.ug/download/credit-life-insurance-flyer/?ind=7054&filename=Credit-Life-Insurance-Flyer.pdf&wpdmdl=7053&refresh=689cf090992661755115664); funeral: [ICEA LION funeral cover](https://icealion.co.ug/funeral-cover-policy/)
- Micro property: [APA Commercial Micro](https://www.apainsurance.org/product_commercial_micro); market context: [allAfrica](https://allafrica.com/stories/202606300195.html)
- Live dev environment, read-only: Azure AI Search index `insuranceintelligence-knowledge`, app settings of `app-api-dev-h5girbnzrayqe`, container app `ca-afrislm-dev`
