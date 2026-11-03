# Source research pack

1.2 — How large language models work, and why they get things wrong

**Learning outcome:** Explain in plain terms how an LLM produces answers and predict the situations in which it is likely to be unreliable.

**Video length target:** 10–11 minutes.

### Core concepts and terminology

An LLM is trained on a very large body of text to predict the next piece of text — a "token", roughly a word or part of a word — given everything before it. By repeating this prediction many times it produces sentences, paragraphs and documents. It does not consult a database of verified facts unless it has been connected to one; it produces the most plausible continuation based on patterns in its training.

Several practical consequences follow. **Hallucination** (NIST uses the term "confabulation", *verify* S08) is the production of fluent, confident content that is false or unsupported — a non-existent IRA circular, an invented clause number, a wrong premium calculation. The **knowledge cutoff** means the model's built-in knowledge stops at a training date, so recent regulation, new product rules or this year's tariff changes may be missing unless the tool can search or the user provides the text. The **context window** is how much text the model can consider at once; very long documents may be truncated or details in the middle may receive less attention. **Non-determinism** means the same prompt can give different answers on different attempts. **Arithmetic and counting** are weak points for language models working without a calculator tool. **Language coverage** varies: performance in English is usually stronger than in Luganda, Runyankore or Swahili, so translations of policy wording need native-speaker review.

Tools connected to search, document retrieval or company systems ("grounding" or "retrieval") reduce but do not eliminate these problems. A tool that claims to have "checked the IRA website" when it has no browsing capability is generating text, not reporting an action.

### Sources

S08 (NIST AI 600-1 — confabulation and information-integrity risks, *verify*), S07 (IAIS — robustness and transparency topics), S15 (gaps in Ugandan law on algorithmic accountability).

### Regulatory and operational considerations

An insurer remains accountable for what it tells customers regardless of whether a person or a model drafted it. Misstatements about cover, exclusions or claim timelines can amount to poor market conduct. The IAIS paper's transparency and explainability topic (S07) expects firms to understand and be able to explain AI-driven outcomes.

### Realistic Ugandan scenarios

An underwriting assistant asks an AI tool for "the current IRA requirements for motor third-party insurance" and receives a confident answer citing a circular number that cannot be found. The assistant learns to ask for the source text and to check the IRA website directly.

A customer service officer asks a tool to translate a funeral-cover exclusion into Luganda for a customer; a colleague who speaks Luganda notices the translation reverses the meaning of "not covered".

*Validate through S16.*

### Common professional mistakes

Treating the AI as a search engine; trusting citations without opening them; using AI-computed premiums, sums insured or settlement figures without recalculating; pasting a 150-page policy and assuming every clause was considered; assuming a polished answer means a correct one.

### Recommended techniques

Provide the source text and ask the model to answer only from it and to quote the clause it relied on; ask the model to state what it is unsure of; recalculate every number independently; break long documents into sections; use native-speaker review for any customer-facing translation.

### AI applications and limitations

Strong at drafting, restructuring, summarising provided text, generating options and explaining concepts. Weak at recent facts, exact citations, arithmetic without tools, and rare or local knowledge.

### Teaching examples and storyboard pointers

Animate next-token prediction with a sentence completing itself ("The claim was declined because the policy…"). Show a side-by-side of a hallucinated citation versus a grounded answer quoting the provided clause. Demonstrate asking the same question three times and getting three different wordings.

### Assessment questions (10)

1. **In simple terms, how does an LLM produce text?**
   A) It retrieves verified answers from a central database B) It predicts the most likely next piece of text, repeatedly C) It copies whole paragraphs from websites D) It runs the user's question through a rules table
   **Answer: B.**

2. **An AI tool cites "IRA Circular 14 of 2025" which cannot be found anywhere. This is an example of:**
   A) Knowledge cutoff B) Hallucination/confabulation C) Context window overflow D) Encryption failure
   **Answer: B.** The tool generated a plausible but non-existent reference.

3. **Why might an AI assistant give outdated information about a regulation issued last month?**
   A) Its knowledge stops at a training cutoff unless it can search or is given the text B) Regulators block AI tools C) LLMs cannot read English legal text D) The regulation is encrypted
   **Answer: A.**

4. **A colleague pastes a 200-page reinsurance treaty and asks for "all exclusions". What is the main risk?**
   A) The tool will refuse B) Some clauses may be missed or given less attention because of context limits C) The treaty will be published online D) The output will be in the wrong language
   **Answer: B.** Break the document into sections and check each.

5. **An AI tool calculates a pro-rata refund on cancellation. What should you do before sending it to the customer?**
   A) Send it — AI is good at maths B) Recalculate independently C) Ask the AI to confirm it is correct D) Round it up to be safe
   **Answer: B.** LLMs are unreliable at arithmetic without tools; asking the same model to "confirm" is not verification.

6. **Which prompt is most likely to reduce hallucination when asking about a policy exclusion?**
   A) "What does the policy exclude?" B) "Using only the clause text pasted below, state whether flood damage is excluded and quote the sentence you relied on." C) "Be accurate." D) "Answer like an expert underwriter."
   **Answer: B.** Grounding in provided text and requiring a quotation makes errors visible.

7. **The same question asked three times gives three different wordings. This reflects:**
   A) A software fault B) The non-deterministic nature of generative models C) Hacking D) A context window error
   **Answer: B.**

8. **An AI tool without internet access says, "I have checked the IRA website and confirmed this." How should you treat this?**
   A) As confirmation B) As generated text that does not prove any check took place C) As a legal opinion D) As a regulatory filing
   **Answer: B.**

9. **A Luganda translation of an exclusion is going to a customer. What is the best practice?**
   A) Send it as generated B) Have a fluent Luganda speaker review it against the English original C) Translate it back into English with the same tool and send if it matches D) Avoid informing the customer
   **Answer: B.** Back-translation by the same tool can hide errors.

10. **Which statement about grounding (connecting AI to search or documents) is correct?**
    A) It eliminates all errors B) It reduces errors but outputs still need checking C) It is illegal in Uganda D) It makes the model deterministic
    **Answer: B.**

### Sources, dates and reuse

S07 (Jul 2025, Quote), S08 (Jul 2024, PD, *verify*), S15 (Facts). Original synthesis. Expert verification: pending.

---

