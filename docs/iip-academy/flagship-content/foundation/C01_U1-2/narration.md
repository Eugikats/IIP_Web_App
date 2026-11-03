# C01_U1-2: How large language models work, and why they get things wrong

Original teaching synthesis from the existing research packs. Fictional scenarios; not insurer product advice. Editorial review remains required before paid release.

### SLIDE 01 - How large language models work, and why they get things wrong

**On-screen text:** Understand the idea · Apply it to insurance work · Check the evidence and responsibilities

**Visual:** Editable teaching text and an insurance example; no invented system screenshot.

NARRATION:
Welcome to How large language models work, and why they get things wrong. This unit connects the concept to practical insurance work. As you listen, distinguish what a tool can prepare from the decisions a person must own. Every scenario is a teaching example, not an actual customer record or a promise that a feature is deployed.
END NARRATION

### SLIDE 02 - Core concepts and terminology

**On-screen text:** An LLM is trained on a very large body of text to predict the next piece of text : a "token", roughly a word or part of a...

**Visual:** Editable teaching text and an insurance example; no invented system screenshot.

NARRATION:
An LLM is trained on a very large body of text to predict the next piece of text : a "token", roughly a word or part of a word : given everything before it. By repeating this prediction many times it produces sentences, paragraphs and documents. It does not consult a database of verified facts unless it has been connected to one; it produces the most plausible continuation based on patterns in its training.
END NARRATION

### SLIDE 03 - Core concepts and terminology

**On-screen text:** Several practical consequences follow.

**Visual:** Editable teaching text and an insurance example; no invented system screenshot.

NARRATION:
Several practical consequences follow. Hallucination (NIST uses the term "confabulation", verify S08) is the production of fluent, confident content that is false or unsupported : a non-existent IRA circular, an invented clause number, a wrong premium calculation. The knowledge cutoff means the model's built-in knowledge stops at a training date, so recent regulation, new product rules or this year's tariff changes may be missing unless the tool can search or the user provides the text. The context window is how much text the model can consider at once; very long documents may be truncated or details in the middle may receive less attention. Non-determinism means the same prompt can give different answers on different attempts. Arithmetic and counting are weak points for language models working without a calculator tool.
END NARRATION

### SLIDE 04 - Core concepts and terminology

**On-screen text:** Language coverage varies: performance in English is usually stronger than in Luganda, Runyankore or Swahili, so translations of policy wording need native-speaker review.

**Visual:** Editable teaching text and an insurance example; no invented system screenshot.

NARRATION:
Language coverage varies: performance in English is usually stronger than in Luganda, Runyankore or Swahili, so translations of policy wording need native-speaker review.
END NARRATION

### SLIDE 05 - Core concepts and terminology

**On-screen text:** Tools connected to search, document retrieval or company systems ("grounding" or "retrieval") reduce but do not eliminate these problems.

**Visual:** Editable teaching text and an insurance example; no invented system screenshot.

NARRATION:
Tools connected to search, document retrieval or company systems ("grounding" or "retrieval") reduce but do not eliminate these problems. A tool that claims to have "checked the IRA website" when it has no browsing capability is generating text, not reporting an action.
END NARRATION

### SLIDE 06 - Realistic Ugandan scenarios

**On-screen text:** An underwriting assistant asks an AI tool for "the current IRA requirements for motor third-party insurance" and receives a confident answer citing a circular number that cannot be...

**Visual:** Editable teaching text and an insurance example; no invented system screenshot.

NARRATION:
An underwriting assistant asks an AI tool for "the current IRA requirements for motor third-party insurance" and receives a confident answer citing a circular number that cannot be found. The assistant learns to ask for the source text and to check the IRA website directly.
END NARRATION

### SLIDE 07 - Realistic Ugandan scenarios

**On-screen text:** A customer service officer asks a tool to translate a funeral-cover exclusion into Luganda for a customer; a colleague who speaks Luganda notices the translation reverses the meaning...

**Visual:** Editable teaching text and an insurance example; no invented system screenshot.

NARRATION:
A customer service officer asks a tool to translate a funeral-cover exclusion into Luganda for a customer; a colleague who speaks Luganda notices the translation reverses the meaning of "not covered".
END NARRATION

### SLIDE 08 - Common professional mistakes

**On-screen text:** Treating the AI as a search engine; trusting citations without opening them; using AI-computed premiums, sums insured or settlement figures without recalculating; pasting a 150-page policy and assuming...

**Visual:** Editable teaching text and an insurance example; no invented system screenshot.

NARRATION:
Treating the AI as a search engine; trusting citations without opening them; using AI-computed premiums, sums insured or settlement figures without recalculating; pasting a 150-page policy and assuming every clause was considered; assuming a polished answer means a correct one.
END NARRATION

### SLIDE 09 - Recommended techniques

**On-screen text:** Provide the source text and ask the model to answer only from it and to quote the clause it relied on; ask the model to state what it...

**Visual:** Editable teaching text and an insurance example; no invented system screenshot.

NARRATION:
Provide the source text and ask the model to answer only from it and to quote the clause it relied on; ask the model to state what it is unsure of; recalculate every number independently; break long documents into sections; use native-speaker review for any customer-facing translation.
END NARRATION

### SLIDE 10 - AI applications and limitations

**On-screen text:** Strong at drafting, restructuring, summarising provided text, generating options and explaining concepts.

**Visual:** Editable teaching text and an insurance example; no invented system screenshot.

NARRATION:
Strong at drafting, restructuring, summarising provided text, generating options and explaining concepts. Weak at recent facts, exact citations, arithmetic without tools, and rare or local knowledge.
END NARRATION

### SLIDE 11 - Practise, check and apply

**On-screen text:** Complete the practice task · Record your reasoning · Take the ten-question quiz independently

**Visual:** Editable teaching text and an insurance example; no invented system screenshot.

NARRATION:
Before taking the quiz, write down the main idea in your own words. Apply it to a fictional task in your role, name the evidence needed and identify what you would check before anyone relies on the result. Keep unresolved questions visible and route decisions to the authorised person. Complete the ten-question quiz independently, then revisit any concept that needs more practice.
END NARRATION

