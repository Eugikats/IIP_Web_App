# Kato build pass (field test)

Read [IIP_KATO_FIELD_TEST_AUDIT.md](IIP_KATO_FIELD_TEST_AUDIT.md) and [IIP_WHATSAPP_VOICE_DESIGN.md](IIP_WHATSAPP_VOICE_DESIGN.md) first. This is the build pass against that audit.

## Decisions already made (do not ask again)

- **Demo phone:** `+256776678989` (Eugene Katusiime). `#reset` works only from this number, only in the Demo runtime profile. The dev API runs `INSURANCEINTELLIGENCE_RUNTIME_PROFILE=Demo`.
- **Deploy authority:** commit to `feature/claim-intake`, push the branch to origin, deploy API and portal to the Azure dev environment (`rg-iip-dev`), apply database migrations there, verify live. No other environments. Follow `docs/05-deployment-release-enablement/AZURE_DEPLOYMENT_AND_ENVIRONMENTS.md`.
- **Sunbird key:** supplied by the user in chat. Store it only as the tenant's encrypted Translation connection (never in code, app settings or docs), verify it live, use it for transcription and translation.
- **Acceptance:** automated scenario tests plus a local end-to-end run plus live dev API checks. The user does a final 5-minute phone pass in the morning from a checklist you write (exact messages to send, in order, with expected replies).
- **No questions until finished.** The user is asleep. Make the reasonable call and record it in the report.

## Hard constraint

Unsupervised field test today with shopkeepers, market traders and boda riders in Kampala, on one shared phone, with no instructions given to participants. Build in the order below and stop when you run out of time rather than leaving something half-done. Items 1 to 6 are the floor: if all six are not done and deployed, say so plainly rather than proceeding to 7. Items 9 to 11 of the audit's own build order are out of scope for this pass.

## 1. #RESET

As specified in the audit: hidden, exact match, handled before anything else, demo number only, in the Demo profile only. Anonymise every customer record on the sending number, clear the language preference, reset thread continuation, close the number's open demo claims with an audit reason, short confirmation reply. Everything else depends on this working, so do it first and test it hardest.

## 2. STOP DUPLICATE CLAIMS

In `HandleNewClaimAsync`: if the sender has an open claim on the same policy from the last 72 hours, add the detail to it and ask whether this is a new incident before opening another. Turn off claim creation entirely in `WhatsAppInboundDocumentIngestionService`; attach photos to the latest open claim, or hold for a person if there is none. Never open a claim on the first policy in a list.

## 3. LINK SAFETY

A typed policy number must no longer be sufficient to open a claim or receive a portal link. Issue a link only when the sending number matches the policyholder's number on record. If it does not, offer a human. Treat this as a security fix, not a demo fix.

## 4. LANGUAGE ROUTING FIX

A message with no positive local-language signal is English, even when a language preference is stored. This is the cause of the 6:34 AM refusal. Add the missing Luganda forms: gyebaleko, kabenje, mbadde, ngude, njagala, kulopa. Show the machine-translation note once per conversation rather than on every reply.

## 5. KATO'S FIRST CONTACT

Rewrite the hard-coded templates in `CustomerAssistantService.cs` as Kato. Do not add a model rephrase step.

On first contact from an unknown number, Kato:

- introduces himself as Nile Shield's helper on WhatsApp
- says what he can do in outcome language, not process language. Not "I can help you file a claim" but closer to "if you have cover with us and something has happened, I can help you get your money"
- says they can write or send a voice note, in whichever language they prefer, ONLY IF item 8 ships a real voice reply. If voice does not ship, Kato must not mention voice notes at all.
- offers a person at any time

Kato never claims to be a person and never performs humanity. He does not announce himself as a machine. If asked directly whether he is a person, he answers honestly and simply.

Every reply should be readable aloud by someone with limited literacy. Short sentences, no bullet lists, no citation blocks mid-chat, no policy extracts unless asked.

## 6. MICROINSURANCE PRODUCTS AND THE NO-POLICY PATH

Publish the three demo products from the research: hospital cash and funeral, boda rider personal accident with daily income, market stall stock cover. All sums insured under UGX 10 million, labelled as microinsurance, document matrices matching the evidence column in the audit table, key-information summaries stating waiting period, exclusions and grace period per the 2016 guidelines.

Load wordings and key-information summaries into the knowledge base with product lines, and pass the sender's product line into retrieval so a motor question stops returning agriculture chunks.

REMOVE the made-up `IRA/DEMO/...` references. We are being judged by the IRA; a fabricated IRA reference number in front of field participants is unacceptable. Use an unmistakable demo marker instead.

No-policy path: Kato asks what they do for a living, maps occupation to product (boda rider -> accident + hospital cash; trader or shopkeeper -> stock cover + hospital cash; anyone else -> hospital cash), explains it in plain words from the knowledge base, and records interest as a lead. He never completes a sale and never recommends across insurers.

Seed the demo phone with these products, and one policy with a claim at "documents requested" so "where is my claim?" has something real to show.

## 7. IDENTITY (only if 1 to 6 are done)

Parse `contacts[].profile.name` in `WhatsAppWebhookParser`, carry it on `WhatsAppInboundMessage`, greet by it. If absent, Kato asks. Save a `Customer` against the number on first contact. Store occupation on the conversation turn or lead, not as a new customer column, to avoid a migration.

## 8. VOICE, CONDITIONAL (only if 1 to 7 are done AND a Sunbird key works)

Minimum acceptable: a voice note gets a real reply, not silence. Voice in, text out: download, transcode, Sunbird transcription, existing assistant, text reply.

If transcription is unreliable or the key is not working, do NOT ship it. Instead make a voice note return a short text reply in the user's language saying Kato cannot listen yet and asking them to write. Silence is the one unacceptable outcome.

Full voice out is out of scope for this pass.

## Acceptance (run before finishing)

a. First message from an unknown number gets Kato's introduction, and it is clear what to do next without being told.
b. "I've had an accident" -> read-back -> no claim until confirmed.
c. Repeat immediately -> existing claim surfaced, no second claim.
d. "what does Motor insurance cover" and "what do you offer in motor insurance" both answer. Neither escalates.
e. "Gyebaleko" gets a real response, not the fallback.
f. "Mbadde njagala kulopa akabenje" starts a claim report in Luganda.
g. A policy number typed from an unrecognised number does NOT yield a link.
h. "I sell tomatoes in Nakasero" gets a relevant microinsurance product explained in plain language.
i. A voice note gets a reply of some kind. Never silence.
j. #reset clears everything and the next participant starts fresh.
k. No reply anywhere invites ID documents or permits into the chat.
l. No fabricated IRA reference appears anywhere a participant can see.

## Report at the end

What shipped, what did not, what to avoid demonstrating as a result, and the morning phone checklist.
