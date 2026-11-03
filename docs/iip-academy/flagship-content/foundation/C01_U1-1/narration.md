# Course 1 · Unit 1.1 — What AI, machine learning, generative AI and agentic AI actually are

## Slide-by-slide narration script (TTS-ready)

How this file works: each slide has on-screen text and a visual note for the slide designer, and a narration block between `NARRATION:` and `END NARRATION`. The notebook reads only the narration blocks, so on-screen text can use abbreviations while narration is written the way it should be spoken (for example "Uganda shillings", "I-R-A"). Edit the narration freely; keep the markers and slide headings unchanged.

Source basis: Research pack Unit 1.1 (S07, S10, S11). Target length 9–10 minutes.

---

### SLIDE 01 — Opening problem

**On-screen text:** "Our new AI-powered, machine-learning, generative, agentic platform…" — Which of these is actually AI?

**Visual:** A vendor pitch slide crowded with buzzwords; the words fade to grey one by one.

NARRATION:
Imagine a vendor walks into your office. Their presentation promises an A-I-powered, machine-learning, generative, agentic platform that will transform your claims department. Everyone in the room nods. But when the meeting ends, nobody can say exactly what the system does, what it cannot do, or what could go wrong. This happens every week in insurance companies, banks and brokerages across Uganda. The words sound impressive, but they describe very different technologies, and those differences matter to you, to your customers and to your company.
END NARRATION

---

### SLIDE 02 — Learning promise

**On-screen text:** By the end of this unit you will be able to: distinguish four types of AI-related technology · classify any insurance tool correctly · explain why the difference matters for risk

**Visual:** Three checkmarks appearing in sequence.

NARRATION:
In this unit, you will learn to tell apart four kinds of technology that are often all called artificial intelligence. By the end, you will be able to look at any tool, whether it is a vendor product, a system your company already uses, or something a colleague recommends, and say clearly what kind of technology it is. More importantly, you will understand why that classification changes the questions you should ask and the controls your organisation needs.
END NARRATION

---

### SLIDE 03 — Why the labels matter

**On-screen text:** Different technology → different strengths → different risks → different controls

**Visual:** Four linked arrows.

NARRATION:
Why spend time on definitions? Because each type of technology fails in a different way. One type is completely predictable but cannot handle anything new. Another learns from your historical data, including its mistakes and biases. Another writes fluent text that can be confidently wrong. And the newest type can take actions on its own. If you call all of them "A-I", you lose the ability to ask the right questions. So let us take them one at a time.
END NARRATION

---

### SLIDE 04 — Type 1: Rules-based automation and RPA

**On-screen text:** Rules-based automation: fixed if-then instructions written by people · RPA: software that repeats human clicks across systems · Predictable, auditable — but does not learn

**Visual:** A simple if-then flowchart; a cursor icon moving between two screens.

NARRATION:
The first type is rules-based automation. Here, people write the instructions explicitly. For example: if the sum insured is above two hundred million Uganda shillings, refer the proposal to a senior underwriter. The system follows that rule every time, exactly as written. A common form of this is robotic process automation, often called R-P-A, where software copies the clicks and keystrokes a person would make, such as moving claim details from an email into the core insurance system. Rules-based systems are predictable and easy to audit. Their weakness is that they cannot handle situations their authors did not anticipate, and they never learn. When your underwriting guidelines change, someone must update the rules by hand.
END NARRATION

---

### SLIDE 05 — Type 2: Machine learning

**On-screen text:** Machine learning: a model learns patterns from historical data · Predicts, scores, classifies · Only as good as the data it learned from

**Visual:** Five years of claims records flowing into a model that outputs a score from 0 to 100.

NARRATION:
The second type is machine learning. Instead of people writing the rules, the system builds a model by finding patterns in historical data. Suppose an insurer trains a model on five years of motor claims. When a new claim arrives, the model gives it a score showing how likely it is to need investigation. Machine learning is predictive. It scores, classifies and forecasts. Its great strength is that it can spot patterns no person would write down as a rule. Its great weakness is that it inherits whatever is in its data. If past decisions were inconsistent or unfair, or if some customer groups are missing from the records, the model carries those problems forward.
END NARRATION

---

### SLIDE 06 — Type 3: Generative AI

**On-screen text:** Generative AI: produces new content — text, summaries, images · Large language models (LLMs) power most assistants · Fluent but probabilistic: can be confidently wrong

**Visual:** A blank letter template filling itself with text.

NARRATION:
The third type is generative A-I. These systems produce new content: a draft letter, a summary of a medical report, an explanation of a policy exclusion in plain language. The assistants most professionals now use are powered by large language models, known as L-L-Ms. They are remarkably good with language. But their output is probabilistic. They generate the most plausible text, not necessarily the true text. A generative tool summarising a hospital invoice might list a procedure that does not appear on the invoice at all, and present it with complete confidence. That is why everything a generative tool produces must be checked against the source before you rely on it.
END NARRATION

---

### SLIDE 07 — Type 4: Agentic AI

**On-screen text:** Agentic AI: plans multiple steps towards a goal · Uses tools: reads, searches, sends, updates · A chatbot answers — an agent acts

**Visual:** A goal flag at the top; an agent icon connected to email, policy database and messaging icons.

NARRATION:
The fourth type is agentic A-I. An agent uses a generative model as its reasoning engine, but it goes further. It is given a goal, it plans the steps to reach that goal, and it uses tools to act. It can read an email, look up a policy, send a message to a customer, or create a task in a system, and then adjust its plan based on what happens. The simplest way to remember the difference is this: a chatbot answers, an agent acts. The International Association of Insurance Supervisors has identified agentic A-I as one of the new developments accelerating the use of A-I across the insurance sector.
END NARRATION

---

### SLIDE 08 — The capability ladder

**On-screen text:** Rules → Machine learning → Generative AI → Agentic AI · More capability = more need for oversight

**Visual:** A four-step ladder; a second arrow labelled "oversight needed" rising alongside it.

NARRATION:
Picture these four types as a ladder. As you climb from rules, to machine learning, to generative A-I, to agents, the systems become more capable and more flexible. But at every step, they also become harder to predict and harder to explain, and the potential consequences of a mistake grow. An agent that can send messages and update records can turn one small error into many. So as capability rises, the need for human oversight rises with it. Keep this ladder in mind. It will come back throughout this course.
END NARRATION

---

### SLIDE 09 — The four-question classifier

**On-screen text:** 1. Does it act using tools towards a goal? → Agentic · 2. Does it create new content? → Generative · 3. Does it learn from data to predict? → Machine learning · 4. Does it follow fixed rules written by people? → Rules/RPA

**Visual:** Four questions stacked as a decision path.

NARRATION:
Here is a simple tool you can use on any system. Ask four questions, in this order. First: does it take actions using tools to reach a goal? If yes, it is agentic. Second: does it create new content, such as text or images? If yes, it is generative. Third: does it learn from historical data to make predictions? If yes, it is machine learning. Fourth: does it follow fixed rules that people wrote? If yes, it is rules-based automation. Real products often combine several types, so treat the first "yes" as the main category, and then note the others it uses.
END NARRATION

---

### SLIDE 10 — Demonstration: classifying insurance tools (part 1)

**On-screen text:** Tool A: refers proposals above UGX 200m to a senior underwriter · Tool B: copies claim details from email into the core system · Tool C: scores new claims for likelihood of investigation, trained on five years of claims

**Visual:** Three tool cards; each flips to reveal its category after narration.

NARRATION:
Let us practise. Tool A refers any proposal with a sum insured above two hundred million Uganda shillings to a senior underwriter. Pause and classify it. It does not act towards a goal, create content, or learn. It follows a fixed rule. So it is rules-based automation. Tool B copies claim details from incoming emails into the core system by repeating the steps a clerk would take. That is also rules-based, specifically robotic process automation. Tool C gives each new claim a score for how likely it is to need investigation, based on a model trained on five years of claims. It learns from data to predict, so it is machine learning.
END NARRATION

---

### SLIDE 11 — Demonstration: classifying insurance tools (part 2)

**On-screen text:** Tool D: drafts claim acknowledgement letters · Tool E: receives a WhatsApp claim notice, checks the policy, sends a document checklist, opens a claim file

**Visual:** Two more tool cards flip; Tool E shows small icons for "generative", "rules" and "tools".

NARRATION:
Tool D drafts claim acknowledgement letters for officers to review and send. It creates new content, so it is generative A-I. Tool E is more interesting. A customer reports an accident on WhatsApp. The system checks that the policy is in force, works out which documents are still needed, sends the customer a checklist, and opens a claim file for an officer. It is working towards a goal and using tools to act, so its main category is agentic. But notice that it combines several types. It uses a generative model to understand the message, rules to decide which documents apply, and tools to take action. This is typical of real systems, and it is exactly why classifying each part matters when you assess risk.
END NARRATION

---

### SLIDE 12 — Common mistakes to avoid

**On-screen text:** Calling every automated system "AI" · Assuming generative AI looks up facts · Assuming an agent is safe because each step looks harmless · Believing AI replaces professional judgment

**Visual:** Four warning icons, one per mistake.

NARRATION:
Now, four mistakes professionals often make. The first is calling every automated system "A-I". A rules table sold as an "A-I engine" needs manual maintenance, not data science. The second is assuming generative tools look up facts. They generate plausible text, which is different. The third is assuming an agent is safe because each individual step seems harmless. Small errors can add up across many steps and many customers. And the fourth is believing that A-I replaces professional judgment. In practice, it changes where your judgment is needed most.
END NARRATION

---

### SLIDE 13 - A practical division of responsibility

**On-screen text:** AI prepares summaries and handovers · People check evidence and retain decision authority

**Visual:** Preparation steps lead to an authorised human review.

NARRATION:
In this course, we use a practical division of responsibility. A tool may help organise information, prepare a summary and route work for review. An authorised person checks the evidence and makes the consequential insurance decision. This is a design and governance boundary, not a claim about how many insurers have deployed a particular technology. When you assess a real tool, ask what it is allowed to do, which source supports its output and who can stop or correct it. A vendor's description or an industry trend cannot establish those facts for your organisation.
END NARRATION

### SLIDE 14 — Summary and quiz

**On-screen text:** Rules follow instructions · Machine learning predicts from data · Generative AI creates content · Agentic AI plans and acts · Next: 10-question quiz (AI assistant hidden)

**Visual:** The four-step ladder returns with one-line labels; a "Start quiz" button.

NARRATION:
Let us summarise. Rules-based automation follows instructions that people write. Machine learning learns from historical data to predict. Generative A-I creates new content, fluently but not always accurately. And agentic A-I plans and takes action using tools. Use the four-question classifier whenever someone presents you with a new tool, and remember that more capability always calls for more oversight. You are now ready for the unit quiz. It has ten questions, and the Academy assistant will be hidden while you take it. In the next unit, we look inside large language models to understand exactly why they sometimes get things wrong.
END NARRATION
