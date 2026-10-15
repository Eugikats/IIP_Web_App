# Course 1 · Module 2 — Working with Generative AI in Daily Insurance Work

## Research packs for Units 2.1–2.3

---

## Unit 2.1 — Prompting for insurance tasks

**Learning outcome:** Write structured prompts that produce usable first drafts for common insurance tasks.

**Video length target:** 11–12 minutes (includes live demonstration).

### Core concepts and terminology

A prompt is the instruction and material given to an AI model. Because the model generates the most plausible continuation of what it receives, vague prompts get generic answers and well-structured prompts get specific, usable ones. The Academy teaches a five-part structure, **RCTCF**: **Role** (who the AI should act as, e.g., "a claims communication specialist at a Ugandan general insurer"), **Context** (the facts of the situation, the audience and any source material), **Task** (exactly what to produce), **Constraints** (what to avoid, length, tone, what must be included, "use only the text provided"), and **Format** (letter, table, bullet summary, WhatsApp message under 300 characters). Two further techniques lift quality: giving an **example** of the desired output, and asking the model to **ask clarifying questions** before drafting. Prompting is iterative — the first draft is refined through follow-up instructions ("make it warmer", "remove the jargon", "add the documents still outstanding").

Prompting is not a way to make the model accurate about facts it does not have. It improves structure, tone and relevance; accuracy still depends on providing correct source information and verifying the result (Unit 2.2).

### Sources

S08 (NIST GenAI profile — human-AI configuration risks, *verify*), S18 (vendor prompting guidance for the tool the Academy selects — *pending*), S14 (customers' difficulty with technical policy language, which prompting can help address).

### Regulatory and operational considerations

Prompts themselves can contain personal data; Unit 2.3 governs what may be included. Templates for prompts that will be reused across a team should be approved, stored centrally and versioned like any other procedure.

### Realistic Ugandan scenarios

Weak prompt: "Write a letter about a delayed claim." Strong prompt: "You are a claims communication specialist at a Ugandan general insurer. A customer's motor own-damage claim is delayed because the police abstract has not been received. Write a WhatsApp message under 120 words, in plain English, that apologises for the wait, explains exactly which document is outstanding and where to get it, gives the claims officer's direct line, and does not promise a settlement date. Use a respectful, warm tone."

Underwriting: "Using only the valuation report text pasted below, produce a table of property address, construction type, sum insured recommended, and any risk features mentioned. Write 'not stated' where the report does not say. Do not infer."

Bancassurance: "Explain the credit-life cover in the approved product sheet below to a small-business borrower in five short sentences, avoiding insurance jargon. Include the main exclusion."

*Validate the scenario types through S16.*

### Common professional mistakes

One-line prompts with no context; asking for facts the model cannot know; not stating the audience; forgetting to forbid invented details; accepting the first draft; including customer identifiers unnecessarily.

### Recommended techniques

Use RCTCF; paste source text and tell the model to use only it; specify "write 'not stated' rather than guessing"; ask for clarifying questions; keep a team library of tested prompts.

### AI applications and limitations

Good for drafting letters, messages, summaries, comparisons, training materials and meeting notes. Not a substitute for knowledge of the specific policy, the customer's file or current regulation.

### Teaching examples and storyboard pointers

Split-screen "weak prompt vs strong prompt" with outputs side by side. Build the RCTCF structure as an animated checklist. Live demo: refine a claims delay message through three follow-up instructions.

### Assessment questions (10)

1. **In the RCTCF structure, what does the second "C" stand for?**
   A) Customer B) Constraints C) Compliance D) Calculation
   **Answer: B.** Role, Context, Task, Constraints, Format.

2. **Which prompt element most directly reduces invented details in an underwriting summary?**
   A) "Act as an expert" B) "Use only the report text provided and write 'not stated' where information is missing" C) "Be detailed" D) "Use bullet points"
   **Answer: B.**

3. **A claims officer writes "Write a letter about a delayed claim." What is the main weakness?**
   A) Too polite B) No context, audience, reason for delay or constraints C) Too long D) Uses the word "claim"
   **Answer: B.**

4. **Which constraint is most important in a delay message to a customer?**
   A) Use technical terms B) Do not promise a settlement date that has not been confirmed C) Make it at least 500 words D) Mention competitors
   **Answer: B.** Unfounded promises create complaints and conduct risk.

5. **Why ask the model to ask clarifying questions before drafting?**
   A) It makes the model slower B) It surfaces missing information before a draft is built on assumptions C) It is required by law D) It changes the model's training
   **Answer: B.**

6. **Prompting well will make an AI tool:**
   A) Accurate about regulations it has never seen B) Better at structure, tone and relevance, but still dependent on correct source information C) Able to access the core system D) Free from bias
   **Answer: B.**

7. **A team reuses the same prompt for motor claim acknowledgements every day. Best practice is to:**
   A) Let each officer write their own each time B) Approve, store and version a tested template C) Share it on social media D) Avoid templates
   **Answer: B.**

8. **Which format instruction suits a WhatsApp update to a customer?**
   A) "A formal three-page letter" B) "Under 120 words, plain language, no attachments" C) "A table with ten columns" D) "Legal citations for every sentence"
   **Answer: B.**

9. **Giving the model an example of a good past message (with personal details removed) mainly helps it to:**
   A) Copy the customer's data B) Match the desired tone, length and structure C) Verify facts D) Connect to the internet
   **Answer: B.**

10. **After receiving a first draft that is too formal, the best next step is to:**
    A) Start again with a new tool B) Give a follow-up instruction such as "make it warmer and simpler, keep the same facts" C) Send it anyway D) Rewrite the whole thing by hand without using the draft
    **Answer: B.** Iteration is part of prompting.

### Sources, dates and reuse

S08 (*verify*), S14 (Facts), S18 (*pending*). Prompt examples are original. Expert verification: pending.

---

## Unit 2.2 — Verifying AI output before you use it

**Learning outcome:** Apply a systematic check to AI output to catch factual, numerical, coverage and tone errors.

**Video length target:** 10–11 minutes.

### Core concepts and terminology

The professional, not the tool, is accountable for anything sent to a customer, broker, bank partner or regulator. The Academy teaches a three-part verification routine, the **3C check**. **Correct**: every fact, name, date, policy number, clause reference and figure matches the source — numbers are recalculated independently, never "confirmed" by asking the same AI. **Complete**: nothing material is missing — exclusions, conditions, outstanding documents, deadlines, the customer's right to complain or escalate. **Compliant**: the output respects data protection, does not promise what the company has not decided, does not misstate cover, and uses an appropriate tone for the customer's situation (for example, a death claim).

Two further concepts help. **Automation bias** is the tendency to accept machine output because it looks authoritative, especially under time pressure; the antidote is a fixed checklist used every time. **Proportionate review** means the depth of checking matches the stakes: an internal meeting summary needs a quick read; a repudiation letter needs line-by-line verification against the file and policy wording.

### Sources

S08 (NIST — human-AI configuration and confabulation risks, *verify*), S07 (IAIS — governance and accountability, transparency), S14 (customer misunderstanding of technical language).

### Regulatory and operational considerations

Statements to customers about cover and claims are conduct matters under the Insurance Act and IRA oversight (S04, *verify*). Errors in documents sent to a bank partner can create liability for both parties. Firms should record which outputs were AI-assisted and who verified them, supporting the accountability principle in S07.

### Realistic Ugandan scenarios

An AI-drafted settlement letter states a sum of UGX 4,850,000; the claims file shows UGX 4,580,000 after excess. The digits were transposed when the officer pasted figures; the 3C check catches it.

A summary of a medical policy for a corporate client omits the waiting period for maternity cover. The "Complete" check against the policy schedule catches it before the HR manager circulates the summary to staff.

*Validate through S16.*

### Common professional mistakes

Checking only spelling and tone; asking the AI "is this correct?"; skipping verification under time pressure; trusting citations without opening them; reviewing the AI output without reopening the source.

### Recommended techniques

Keep the source document open beside the draft; highlight every number and recompute; tick off a completeness list specific to the document type; read the output once from the customer's perspective; record the verifier's name for customer-facing documents.

### AI applications and limitations

AI can help verification by, for example, listing every figure in a draft so a person can check each — but the check itself must be against the source by a person or a deterministic tool.

### Teaching examples and storyboard pointers

Show an AI-drafted letter with three planted errors and pause for learners to find them before revealing. Animate the 3C checklist. Short segment on automation bias with a relatable example.

### Assessment questions (10)

1. **What does the "Complete" part of the 3C check ask?**
   A) Is the spelling correct? B) Is anything material missing, such as exclusions, deadlines or outstanding documents? C) Is the letter long enough? D) Has the AI finished generating?
   **Answer: B.**

2. **An AI-drafted settlement letter shows UGX 4,850,000; the file shows UGX 4,580,000. Which check should catch this?**
   A) Compliant B) Correct C) Complete D) Format
   **Answer: B.**

3. **What is the weakness of asking the same AI tool "Is this calculation correct?"**
   A) It is rude B) The tool may simply affirm its own output; it is not independent verification C) It costs more D) It deletes the draft
   **Answer: B.**

4. **Automation bias is:**
   A) Bias in training data against certain groups B) The tendency to accept machine output because it looks authoritative C) A virus D) A type of premium loading
   **Answer: B.**

5. **Which document needs the deepest verification?**
   A) Internal brainstorming notes B) A repudiation letter to a policyholder C) A draft agenda for a team meeting D) A list of possible training topics
   **Answer: B.** Proportionate review matches depth to stakes.

6. **A summary for a corporate client omits the maternity waiting period. Which risk does this create?**
   A) None — summaries are informal B) Staff may rely on the summary and later dispute a declined claim C) The policy becomes void D) The premium doubles
   **Answer: B.**

7. **Which practice best supports accountability for AI-assisted customer letters?**
   A) Deleting drafts after sending B) Recording that the letter was AI-assisted and who verified it C) Letting the AI sign the letter D) Avoiding all AI use
   **Answer: B.** Aligns with governance and accountability (S07).

8. **An AI letter to a bereaved family uses a cheerful marketing tone. Which check catches this?**
   A) Correct B) Complete C) Compliant (including appropriate tone) D) None
   **Answer: C.**

9. **What is the best way to verify a clause reference an AI provided?**
   A) Trust it if formatted correctly B) Open the policy wording and confirm the clause says what the draft claims C) Ask a second AI D) Remove it
   **Answer: B.**

10. **Under heavy workload, a colleague proposes skipping checks on AI-drafted status messages. The best response is:**
    A) Agree B) Use a quick fixed checklist proportionate to the message — at minimum names, numbers, documents and promises C) Stop sending updates D) Send them unsigned
    **Answer: B.**

### Sources, dates and reuse

S07 (Quote), S08 (*verify*), S14 (Facts). Original synthesis. Expert verification: pending.

---

## Unit 2.3 — Protecting personal data when using AI

**Learning outcome:** Decide what information may and may not be entered into an AI tool, and apply the DPPA 2019 principles to AI use.

**Video length target:** 11–12 minutes.

### Core concepts and terminology

Insurance runs on personal data: names, national identification numbers, phone numbers, addresses, vehicle registrations, medical records, bank details, beneficiary information. Uganda's Data Protection and Privacy Act 2019 (DPPA) applies to the collection, processing, holding or use of personal data within Uganda, and to processing outside Uganda of data relating to Ugandan citizens (S05, S06). Its supporting Regulations were gazetted in March 2021, and the Personal Data Protection Office (PDPO), under NITA-U, has been operational since August 2021 (S06). Every data collector, processor and controller must register with the PDPO (S06). The Act sets out principles of data protection, requires consent for collection and processing subject to exceptions, restricts processing of special personal data, sets conditions for processing personal data outside Uganda, and gives data subjects rights including rights in relation to automated decision-taking (S05). Where processing is likely to threaten data subjects' rights, a data protection impact assessment is expected before processing begins (secondary summary; *verify against the Regulations*).

For AI use, four questions guide every professional. **Is personal data needed for this task?** Most drafting and summarising can be done with names and identifiers removed or replaced. **Where does the data go?** Many public AI tools process data on servers outside Uganda and may retain or use inputs, which engages the DPPA's conditions for processing outside Uganda. **Is the tool approved?** The organisation, not the individual, should decide which tools may handle which data, based on contract terms and security (S18). **Is the processing consistent with the purpose for which data was collected?** Data collected to process a claim should not be repurposed for marketing via AI without a lawful basis.

Key practical terms: **anonymisation** (removing identifiers so the person cannot be identified), **pseudonymisation** (replacing identifiers with codes, e.g., "Claimant A"), **special personal data** (categories requiring extra protection — the DPPA list includes health-related information; *verify the full list*), and **data minimisation** (use only what is needed).

### Sources

S05 (DPPA 2019 and Regulations 2021), S06 (RSM summary: PDPO, registration), S15 (gaps in DPPA on AI profiling), S18 (tool data terms — *pending*).

### Regulatory and operational considerations

The DPPA creates offences including unlawful obtaining or disclosure of personal data and sale of personal data (S05). Insurers' data protection officers should be involved in approving AI tools. Commentators argue the DPPA does not yet address algorithmic profiling and AI-specific impact assessments in detail (S15), so firms should apply the existing principles conservatively.

### Realistic Ugandan scenarios

A claims officer pastes a full medical report, including the patient's name and national ID, into a free public AI tool to summarise it. The same summary could have been produced with "Patient A", no ID and only the clinical sections needed.

A broker wants to use AI to draft renewal reminders using the full client list exported from the CRM. The safer approach is to draft one template with placeholders and merge personal details inside the broker's own system.

*Validate through S16.*

### Common professional mistakes

Pasting complete customer files; assuming deleting the chat removes the data from the provider; using personal accounts on public tools for work; sharing screenshots of AI chats containing customer data on messaging groups; repurposing claims data for marketing.

### Recommended techniques

Strip or replace identifiers before using AI; use templates with placeholders; use only organisation-approved tools for any personal data; keep special personal data (especially medical information) out of public tools entirely; ask the data protection officer when unsure.

### AI applications and limitations

AI can help detect personal data in documents before sharing, but such checks must themselves be tested. AI tools cannot make an unlawful processing activity lawful.

### Teaching examples and storyboard pointers

"Red-amber-green" slide sorting data types for public AI use. Before/after demonstration of pseudonymising a claim summary. Simple diagram of data flowing from Uganda to an overseas AI server.

### Assessment questions (10)

1. **Which body regulates the processing of personal data in Uganda?**
   A) Insurance Regulatory Authority B) Personal Data Protection Office under NITA-U C) Uganda Revenue Authority D) Bank of Uganda
   **Answer: B.** (S06)

2. **A claims officer wants an AI summary of a medical report. What is the best practice?**
   A) Paste the full report into any free tool B) Remove name, ID and unneeded details, refer to "Patient A", and use an organisation-approved tool C) Email the report to the AI provider D) Ask the customer to summarise it
   **Answer: B.**

3. **Pseudonymisation means:**
   A) Encrypting the hard drive B) Replacing identifiers with codes such as "Claimant A" C) Deleting the file D) Translating the document
   **Answer: B.**

4. **Why does using a public AI tool hosted abroad raise a DPPA issue?**
   A) Foreign tools are banned B) The Act sets conditions for processing personal data outside Uganda C) Foreign tools cannot read English D) It does not raise any issue
   **Answer: B.** (S05)

5. **Who should decide which AI tools may process customer data?**
   A) Each staff member B) The organisation, involving the data protection officer, based on contract and security terms C) The customer D) The AI vendor alone
   **Answer: B.**

6. **A broker wants AI help with renewal reminders. Which approach applies data minimisation?**
   A) Upload the full CRM export B) Draft a template with placeholders and merge details within the broker's own system C) Share the client list on WhatsApp D) Ask the AI to remember all clients
   **Answer: B.**

7. **Deleting an AI chat on a public tool:**
   A) Always removes the data from the provider's systems B) May not remove data the provider has already retained, depending on its terms C) Notifies the PDPO D) Is illegal
   **Answer: B.** Check the tool's terms (S18).

8. **Using claims data to target customers with AI-generated marketing without a lawful basis conflicts with which principle?**
   A) Purpose limitation B) Premium adequacy C) Indemnity D) Subrogation
   **Answer: A.**

9. **The DPPA gives data subjects rights in relation to:**
   A) Automated decision-taking B) Choice of loss assessor C) Premium discounts D) Reinsurance placement
   **Answer: A.** (S05)

10. **Which statement about AI and Ugandan data protection law is most accurate?**
    A) The DPPA does not apply to AI B) The DPPA applies to AI processing of personal data, though commentators argue it does not yet address algorithmic profiling in detail C) AI tools are exempt if hosted abroad D) Only government bodies must comply
    **Answer: B.** (S05, S15)

### Sources, dates and reuse

S05 (2019/2021, PD; section numbers *verify*), S06 (Facts), S15 (Facts), S18 (*pending*). Original synthesis. Expert verification: pending — recommend review by a data protection practitioner.
