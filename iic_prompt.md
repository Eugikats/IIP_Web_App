This is a read-only audit. Do not change any code. I need a clear picture of
what exists before we decide what to build, and I need it tonight.

BACKGROUND

IIP is an insurance servicing platform. Customers reach it over WhatsApp; a
web portal handles claim documents. It was built around traditional insurance
(a motor policy with TPO/TPFT/Comprehensive tiers, a medical policy) for
customers who already hold cover with an insurer.

We are entering a microinsurance innovation challenge, and tomorrow we field
test with shopkeepers, market traders and boda riders in Kampala. They will
use my phone. They will be given no instructions at all; we hand them the
phone and watch what they do.

WHERE WE ARE GOING

The bot becomes a persona called Kato. On first contact Kato introduces
himself, says in plain outcome language what he can do (not "I can file a
claim" but "if you have cover with us and something has happened, I can help
you get your money"), and tells the person they can text or send a voice note
in whichever language they prefer. Kato guides; the user should never have to
work out what to do next.

Kato never claims to be a person and never performs humanity, but does not
announce himself as a machine either. He identifies as Nile Shield's helper
on WhatsApp, always offers a route to a human, and answers honestly if asked
directly whether he is a person.

If the person has no policy, Kato asks what they do for a living, and based
on the answer explains which microinsurance product would suit them.

Identity works off the phone number: pull the name from the Meta WhatsApp
profile if available, otherwise ask, then save it against the number as a
customer record so policies resolve without anyone reciting a policy number.

Because one phone is shared across participants, we need a hidden "#reset"
command that clears the customer record and closes open demo claims between
sessions.

READ FIRST

- The attached WhatsApp transcript. It shows the current bot's real behaviour
  and most of the defects below are evidenced in it.
- The whole codebase, the knowledge base contents, and the data model.

WHAT I NEED YOU TO REPORT

1. ARCHITECTURE
   How a WhatsApp message flows through the system today, end to end. Which
   component decides intent, which holds conversation state, which writes
   claims, which issues access links, which renders the portal. Name the
   files.

2. IDENTITY AND CUSTOMER RECORDS
   Is there any concept of a customer keyed to a phone number? Does anything
   read the Meta profile name? How does the system currently decide which
   policies belong to the person messaging? What would need to exist for the
   identity flow described above? Be specific about what is missing.

3. KNOWLEDGE BASE
   Inventory exactly what is in it. Which products, which documents, which
   languages. I believe it is all traditional insurance. Confirm or correct.
   Report what would need to be added or replaced for microinsurance
   products, and whether the retrieval layer is product-aware or just does
   flat search across everything.

4. CLAIMS
   Where a claim is created, what triggers creation, and why the transcript
   shows three motor claims opened on one policy with two of them hours
   apart. Is there any duplicate check, any confirmation step, any claim
   classification? The portal shows a claim tagged UNCLASSIFIED at 0%
   completeness demanding four documents regardless of claim size. Report how
   document requirements are determined and whether they can be made
   configurable by product and claim value.

5. LANGUAGE
   How is language detected, stored and switched? Why do Luganda phrases like
   "Gyebaleko", "Mbadde ngude mu kabenje" and "Mbadde njagala kulopa akabenje"
   fall through to the fallback? Is translation done by a model, a service, or
   static strings? Where does the "(Obubaka buno bwavvuunulwa kompyuta)"
   footer get appended and why on every message?

6. CAPABILITY CONSISTENCY
   In the transcript, "what do you offer in motor insurance" answered fully
   at 7:19 PM and the near-identical "what does Motor insurance cover"
   refused and escalated at 6:34 AM. Find the cause. Is it retrieval,
   intent routing, a confidence threshold, or session state?

7. VOICE
   Does any audio handling exist at all? Inbound media download, format
   conversion, any speech-to-text or text-to-speech integration, any Sunbird
   AI integration. Report honestly if there is none.

8. ACCESS LINKS AND DOCUMENTS
   How tokens are generated, what verification happens before issuing one
   (the transcript suggests none), how long they live, and what the portal
   does with uploads. Where are documents stored and is anything encrypted
   at rest?

9. TONE AND RESPONSE GENERATION
   Are replies templated, model-generated, or a mix? Where would a persona
   and voice be defined? How much work is it to change tone across the board?

10. DEMO DATA
    What seeding exists. What would be needed so that my phone number has
    working policies, a customer record, and claims in sensible states for a
    live session tomorrow.

ALSO RESEARCH (in parallel, cite sources)

- The IRA microinsurance guidelines and what they require of a microinsurance
  product: documentation, policy wording, claim requirements, any limits on
  sum insured or premium that define a product as microinsurance in Uganda.
- What microinsurance products are actually sold to this segment in Uganda
  and comparable East African markets: hospital cash, funeral, credit life,
  personal accident, property/stock cover for traders, boda-specific covers.
  For each, the typical premium, sum insured, and what evidence is required
  to claim. I want to design demo products that a regulator would recognise
  as legitimate, not invented ones.

DELIVERABLE

A written report, no code changes. For each area: what exists, what is
missing, and how much work the gap represents. Flag anything that surprises
you. Be blunt about what is weaker than it looks. End with a ranked list of
what must be fixed before a stranger uses this unsupervised tomorrow, versus
what can wait.