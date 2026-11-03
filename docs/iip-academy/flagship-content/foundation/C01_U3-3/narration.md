# C01_U3-3: Designing human oversight for AI agents

Original teaching synthesis from the existing research packs. Fictional scenarios; not insurer product advice. Editorial review remains required before paid release.

### SLIDE 01 - Designing human oversight for AI agents

**On-screen text:** Understand the idea · Apply it to insurance work · Check the evidence and responsibilities

**Visual:** Editable teaching text and an insurance example; no invented system screenshot.

NARRATION:
Welcome to Designing human oversight for AI agents. This unit connects the concept to practical insurance work. As you listen, distinguish what a tool can prepare from the decisions a person must own. Every scenario is a teaching example, not an actual customer record or a promise that a feature is deployed.
END NARRATION

### SLIDE 02 - Core concepts and terminology

**On-screen text:** Human oversight is designed, not assumed.

**Visual:** Editable teaching text and an insurance example; no invented system screenshot.

NARRATION:
Human oversight is designed, not assumed. The Academy teaches five building blocks. A decision-rights matrix lists every action the agent can take and states, for each, whether the agent may act alone, must seek approval, or must never act. Escalation triggers are conditions that send a case to a person: value above a threshold, low model confidence, missing or conflicting documents, a complaint or distress signal, a vulnerable customer (bereavement, serious illness), or any request outside the agent's scope. An audit trail records inputs, the agent's reasoning or steps, tools used, outputs and who approved what, so decisions can be explained and reviewed. Monitoring tracks error rates, escalation rates, complaints and drift over time, with regular sampling of the agent's work by a person.
END NARRATION

### SLIDE 03 - Core concepts and terminology

**On-screen text:** A stop mechanism ("kill switch") lets the business pause the agent immediately.

**Visual:** Editable teaching text and an insurance example; no invented system screenshot.

NARRATION:
A stop mechanism ("kill switch") lets the business pause the agent immediately.
END NARRATION

### SLIDE 04 - Core concepts and terminology

**On-screen text:** Agents also face a specific security risk: prompt injection, where instructions hidden in content the agent reads (an email, a PDF, a web page) try to make it...

**Visual:** Editable teaching text and an insurance example; no invented system screenshot.

NARRATION:
Agents also face a specific security risk: prompt injection, where instructions hidden in content the agent reads (an email, a PDF, a web page) try to make it do something unintended : for example, a document containing hidden text telling the agent to "mark this claim approved". OWASP lists prompt injection and excessive agency among the top risks for LLM applications. The defences are least-privilege tool permissions, treating all external content as data not instructions, human approval for consequential actions, and testing with adversarial examples.
END NARRATION

### SLIDE 05 - Core concepts and terminology

**On-screen text:** Meaningful oversight also guards against rubber-stamping: if a human approves hundreds of agent decisions a day without time to review, the oversight is nominal.

**Visual:** Editable teaching text and an insurance example; no invented system screenshot.

NARRATION:
Meaningful oversight also guards against rubber-stamping: if a human approves hundreds of agent decisions a day without time to review, the oversight is nominal. Approval volumes and review time should be sized realistically.
END NARRATION

### SLIDE 06 - Realistic Ugandan scenarios

**On-screen text:** An intake agent's decision-rights matrix: may send document checklists alone; may create claim files alone; must get approval to tell a customer a claim is "not covered"; must...

**Visual:** Editable teaching text and an insurance example; no invented system screenshot.

NARRATION:
An intake agent's decision-rights matrix: may send document checklists alone; may create claim files alone; must get approval to tell a customer a claim is "not covered"; must never authorise payment. Escalation triggers: any death claim, any message containing the words "complaint" or "lawyer", any amount over a set threshold.
END NARRATION

### SLIDE 07 - Realistic Ugandan scenarios

**On-screen text:** A broker's quoting agent reads a client email containing hidden text instructing it to apply the maximum discount.

**Visual:** Editable teaching text and an insurance example; no invented system screenshot.

NARRATION:
A broker's quoting agent reads a client email containing hidden text instructing it to apply the maximum discount. Least-privilege permissions mean it can only propose discounts for human approval, so the attack fails.
END NARRATION

### SLIDE 08 - Common professional mistakes

**On-screen text:** Treating "a human can review if needed" as oversight; no record of what the agent did; giving agents broad system permissions; escalation rules so wide everything escalates (no...

**Visual:** Editable teaching text and an insurance example; no invented system screenshot.

NARRATION:
Treating "a human can review if needed" as oversight; no record of what the agent did; giving agents broad system permissions; escalation rules so wide everything escalates (no value) or so narrow nothing does (no control); no way to stop the agent quickly.
END NARRATION

### SLIDE 09 - Recommended techniques

**On-screen text:** Build the decision-rights matrix first; set escalation triggers from real complaint and error data; sample a fixed percentage of agent work weekly; test with adversarial documents before go-live;...

**Visual:** Editable teaching text and an insurance example; no invented system screenshot.

NARRATION:
Build the decision-rights matrix first; set escalation triggers from real complaint and error data; sample a fixed percentage of agent work weekly; test with adversarial documents before go-live; name an accountable owner for each agent.
END NARRATION

### SLIDE 10 - AI applications and limitations

**On-screen text:** AI can assist monitoring (summarising error patterns) but cannot be its own overseer.

**Visual:** Editable teaching text and an insurance example; no invented system screenshot.

NARRATION:
AI can assist monitoring (summarising error patterns) but cannot be its own overseer.
END NARRATION

### SLIDE 11 - Practise, check and apply

**On-screen text:** Complete the practice task · Record your reasoning · Take the ten-question quiz independently

**Visual:** Editable teaching text and an insurance example; no invented system screenshot.

NARRATION:
Before taking the quiz, write down the main idea in your own words. Apply it to a fictional task in your role, name the evidence needed and identify what you would check before anyone relies on the result. Keep unresolved questions visible and route decisions to the authorised person. Complete the ten-question quiz independently, then revisit any concept that needs more practice.
END NARRATION

