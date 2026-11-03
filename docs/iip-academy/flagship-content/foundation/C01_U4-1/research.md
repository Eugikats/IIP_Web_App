# Source research pack

4.1 — AI risks specific to insurance

**Learning outcome:** Identify fairness, explainability, security, third-party and conduct risks in an AI use case.

**Video length target:** 11–12 minutes.

### Core concepts and terminology

Insurance is built on differentiating between risks, which makes the line between legitimate risk assessment and unfair discrimination central to AI governance. Six risk families matter most.

**Fairness and bias.** Models learn from historical data, which may reflect past unfair practices or under-representation of some groups. **Proxy discrimination** occurs when a variable that seems neutral — a location, a phone type, a mobile-money pattern — closely tracks a characteristic that should not drive decisions. **Explainability.** Customers declined, loaded or delayed are entitled to understand why; complex models can make this hard, and the IAIS treats transparency and explainability as a core supervisory topic (S07). **Robustness and security.** Models can fail on unusual inputs, degrade as conditions change (**model drift**), be manipulated (prompt injection, Unit 3.3), or leak sensitive data. **Third-party risk.** Most insurers will use vendor models and cloud platforms; the insurer remains accountable for outcomes, so contracts, data terms, audit rights and exit plans matter. **Conduct and customer outcomes.** AI that pushes products customers do not need, misstates cover, or makes claims harder to pursue creates conduct risk. **Over-reliance.** Staff may lose skills or stop challenging outputs (automation bias, Unit 2.2).

### Sources

S07 (IAIS — five topics including fairness, ethics and redress), S08 (NIST — trustworthy AI characteristics, *verify*), S09 (EU AI Act — life and health insurance pricing as high-risk), S15 (DPPA gaps on profiling), S17 (*verify*).

### Regulatory and operational considerations

The EU AI Act classifies AI used for risk assessment and pricing of natural persons in life and health insurance as high-risk (S09); although not binding in Uganda, it signals where international expectations are heading and will apply to Ugandan firms serving EU-linked groups or clients. In Uganda, DPPA obligations apply to profiling and automated decisions (S05).

### Realistic Ugandan scenarios

A micro-health product uses an ML model to price cover. The model learns that customers in certain districts have higher claims; investigation shows those districts have better hospital access, so people there actually use their cover. Pricing on this pattern would penalise access to care rather than risk.

A vendor's chatbot for a broker stores conversations on overseas servers for "model improvement"; the contract does not address deletion or DPPA compliance.

*Validate through S16.*

### Common professional mistakes

Assuming removing a protected attribute removes bias; accepting "the model is proprietary" as an answer to explainability questions; not monitoring after go-live; signing vendor terms without data and audit clauses.

### Recommended techniques

Ask of every use case: Who could be treated unfairly? Can we explain the outcome to the customer? What happens if it fails? Who is the vendor and what do they do with our data? How will we know if it drifts?

### Teaching examples and storyboard pointers

Six-tile risk map. Animated proxy-discrimination example (the district pricing case). Vendor-contract checklist slide.

### Assessment questions (10)

1. **Proxy discrimination occurs when:**
   A) A broker acts on behalf of a client B) A seemingly neutral variable closely tracks a characteristic that should not drive decisions C) A model is too small D) A premium is paid late
   **Answer: B.**

2. **A model raises micro-health prices in districts with better hospital access because claims are higher there. What is the concern?**
   A) None — higher claims justify higher prices B) The model may be penalising access to care rather than true risk, creating an unfair outcome C) The model is too accurate D) The districts are too small
   **Answer: B.**

3. **Model drift means:**
   A) The model moves to another server B) Model performance changes over time as real-world conditions change C) A model is copied by a competitor D) The model's name changes
   **Answer: B.**

4. **A vendor refuses to explain how its model declines applications, citing proprietary rights. The insurer should:**
   A) Accept this B) Recognise it remains accountable and require sufficient explanation, testing access or alternatives C) Stop explaining declines to customers D) Rely on the vendor's reputation
   **Answer: B.**

5. **Which IAIS supervisory topic most directly relates to customers understanding why AI affected them?**
   A) Proportionality B) Transparency and explainability C) Capital adequacy D) Reinsurance
   **Answer: B.** (S07)

6. **Removing gender from a dataset guarantees the model will not discriminate by gender. True or false?**
   A) True B) False — other variables may act as proxies
   **Answer: B.**

7. **Which contract clause is most important when a chatbot vendor processes customer conversations?**
   A) Logo usage B) Data use, retention, deletion, location and audit rights C) Office hours D) Font choice
   **Answer: B.**

8. **Under the EU AI Act, AI used for risk assessment and pricing of individuals in life and health insurance is classified as:**
   A) Prohibited B) High-risk C) Minimal risk D) Unregulated
   **Answer: B.** (S09)

9. **An AI sales assistant consistently recommends the highest-commission product regardless of need. Which risk family is this?**
   A) Conduct and customer outcomes B) Model drift C) Prompt injection D) Data minimisation
   **Answer: A.**

10. **Which practice best addresses over-reliance on AI?**
    A) Removing human review B) Keeping staff trained to challenge outputs and sampling AI work regularly C) Using more AI tools D) Hiding AI use from staff
    **Answer: B.**

### Sources, dates and reuse

S05 (PD), S07 (Quote), S08 (*verify*), S09 (Facts), S15 (Facts), S17 (*verify*). Original synthesis. Expert verification: pending.

---

