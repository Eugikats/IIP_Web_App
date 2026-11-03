# Source research pack

2.1 — Prompting for insurance tasks

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

