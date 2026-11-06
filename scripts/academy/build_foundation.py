import json
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "docs/iip-academy/academy-content"
OUTPUT = ROOT / "docs/iip-academy/flagship-content/foundation"
MODULES = [
    "Foundations: AI in Ugandan insurance",
    "Working with generative AI in daily insurance work",
    "Agentic AI: from assistants to agents",
    "Responsible AI governance in insurance",
]


def plain(text):
    text = re.sub(r"\*\*([^*]+)\*\*", r"\1", text)
    text = re.sub(r"\*([^*]+)\*", r"\1", text)
    text = re.sub(r"\s*\(S\d+[^)]*\)", "", text)
    text = text.replace("\u2014", ":").replace("\u2013", " to ")
    return " ".join(text.split())


def questions(text):
    section = re.search(r"### Assessment questions[^\n]*\n(.*?)(?=\n### |\Z)", text, re.S)
    if not section:
        raise ValueError("No question section")
    result = []
    blocks = re.split(r"\n\s*\d+\.\s+", "\n" + section.group(1))[1:]
    for block in blocks:
        answer = re.search(r"\*\*Answer:\s*([A-D])\.?\*\*\s*(.*)", block, re.S)
        first_option = re.search(r"\bA\)", block)
        if not answer or not first_option:
            raise ValueError("Question format not recognised")
        prompt = plain(block[:first_option.start()])
        options_text = block[first_option.start():answer.start()]
        options = [plain(part) for part in re.split(r"\b[A-D]\)\s*", options_text)[1:]]
        index = ord(answer.group(1)) - ord("A")
        if index >= len(options) or len(options) < 2:
            raise ValueError("Question options invalid")
        result.append({"prompt": prompt, "options": options, "answer": index, "explanation": plain(answer.group(2)) or "Use the lesson's evidence and human-review principles."})
    if len(result) != 10:
        raise ValueError(f"Expected ten questions, found {len(result)}")
    return result


def script(unit, title, sections):
    text = f"# {unit}: {title}\n\nOriginal teaching synthesis from the existing research packs. Fictional scenarios; not insurer product advice. Editorial review remains required before paid release.\n\n"
    slides = []
    for heading, paragraph in sections:
        paragraphs = [part.strip() for part in paragraph.split("\n\n") if part.strip() and not part.lstrip().startswith(("|", "*Validate", "S0", "S1"))]
        for paragraph_text in paragraphs:
            cleaned = plain(paragraph_text)
            if not cleaned or len(cleaned.split()) < 12:
                continue
            sentences = re.split(r"(?<=[.!?])\s+", cleaned)
            pieces = []
            current = []
            for sentence in sentences:
                if sum(len(item.split()) for item in current) + len(sentence.split()) > 130 and current:
                    pieces.append(" ".join(current))
                    current = []
                current.append(sentence)
            if current:
                pieces.append(" ".join(current))
            for piece in pieces:
                first_sentence = re.split(r"(?<=[.!?])\s+", piece)[0]
                screen_words = first_sentence.split()
                screen = " ".join(screen_words[:28]) + ("..." if len(screen_words) > 28 else "")
                slides.append((heading, screen, piece))
    slides.insert(0, (title, "Understand the idea · Apply it to insurance work · Check the evidence and responsibilities", f"Welcome to {title}. This unit connects the concept to practical insurance work. As you listen, distinguish what a tool can prepare from the decisions a person must own. Every scenario is a teaching example, not an actual customer record or a promise that a feature is deployed."))
    slides.append(("Practise, check and apply", "Complete the practice task · Record your reasoning · Take the ten-question quiz independently", "Before taking the quiz, write down the main idea in your own words. Apply it to a fictional task in your role, name the evidence needed and identify what you would check before anyone relies on the result. Keep unresolved questions visible and route decisions to the authorised person. Complete the ten-question quiz independently, then revisit any concept that needs more practice."))
    for index, (heading, screen, narration) in enumerate(slides, 1):
        text += f"### SLIDE {index:02} - {heading}\n\n**On-screen text:** {screen}\n\n**Visual:** Editable teaching text and an insurance example; no invented system screenshot.\n\nNARRATION:\n{narration}\nEND NARRATION\n\n"
    return text


def build():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    units = []
    for module_number in range(1, 5):
        path = next(SOURCE.glob(f"C01_M{module_number}_Research_Packs.md"))
        content = path.read_text(encoding="utf-8-sig")
        blocks = re.split(r"^## Unit ", content, flags=re.M)[1:]
        for block in blocks:
            title_match = re.match(r"(\d+)\.(\d+)\s*[\u2014-]\s*([^\n]+)", block)
            if not title_match:
                raise ValueError("Unit heading missing")
            module, number, title = title_match.groups()
            unit = f"C01_U{module}-{number}"
            headings = list(re.finditer(r"^### ([^\n]+)\n", block, re.M))
            sections = []
            permitted = ("Core concepts", "Realistic", "Common professional", "Recommended techniques", "AI applications", "Worked example")
            for index, heading in enumerate(headings):
                if heading.group(1).startswith(permitted):
                    ending = headings[index + 1].start() if index + 1 < len(headings) else len(block)
                    selected = block[heading.end():ending].strip()
                    selected = "\n\n".join(paragraph for paragraph in selected.split("\n\n") if not any(term in paragraph for term in ["Evident's tracking", "ScienceSoft reports", "UGX 2.024", "IRA projects premium growth"]))
                    sections.append((heading.group(1), selected))
            assessment = questions(block)
            for question_index, question in enumerate(assessment):
                if re.search(r"202[56]|GWP|trillion|gross claims", question["prompt"], re.I):
                    replacements = [
                        ("A vendor cites rapid AI adoption elsewhere. What does this establish about your insurer's readiness?", ["A reason to investigate, but not evidence that the local workflow is ready", "That the same accuracy will transfer without testing", "That human review is no longer needed"]),
                        ("A claims-summary pilot saves time but omits exclusions. What is the best response?", ["Pause consequential use, examine omissions and retest against source documents", "Scale immediately because speed is the only success measure", "Delete the failed examples from the evaluation set"]),
                        ("How should a team compare a bounded pilot with its current process?", ["Use comparable cases, documented quality checks and time measurements", "Compare the best AI example with the worst manual example", "Use only a demonstration supplied by the vendor"]),
                        ("Which evidence is needed before relying on a reported performance improvement?", ["The baseline, sample, measurement method and observed results", "A percentage without a denominator", "The confidence of the presenter"]),
                        ("Who owns the decision to approve a consequential insurance action after an AI draft?", ["The authorised accountable person applying the relevant evidence and controls", "The model because it wrote the draft", "The person who purchased the software"]),
                        ("What makes an insurance AI pilot bounded?", ["A defined task, permitted data, reviewer, limits and stop conditions", "Unrestricted access to every customer record", "A general instruction to automate everything"]),
                        ("A pilot reports fewer errors after excluding difficult cases. What should a reviewer ask?", ["Whether exclusions changed the sample and whether difficult cases have safe handling", "Only whether the headline figure looks better", "Whether failed cases can be hidden permanently"]),
                        ("What is the strongest reason to retain a pilot's evaluation examples?", ["They support repeatable checks, error analysis and review of changes", "They replace the need for access control", "They guarantee that every future case will be correct"]),
                        ("What should happen when the pilot lacks the source needed to establish a fact?", ["Mark the fact unresolved and request the missing evidence", "Infer the fact from similar customers", "Publish the draft without showing the gap"]),
                        ("Which proposal gives a manager the clearest basis for a pilot decision?", ["A scoped use case with evidence, risks, ownership, measures and stop criteria", "A promise that AI will remove all uncertainty", "A vendor headline with no local test plan"]),
                    ]
                    prompt, options = replacements[question_index]
                    assessment[question_index] = {"prompt": prompt, "options": options, "answer": 0, "explanation": "Use relevant local evidence, explicit limits and accountable human review rather than an unsupported industry statistic."}
            if unit == "C01_U4-2":
                sections = [
                    ("Insurance supervision and organisational accountability", "The International Association of Insurance Supervisors published an application paper on AI supervision in July 2025. It addresses proportionality, governance and accountability, robustness and security, transparency and explainability, and fairness and redress. Treat it as supervisory guidance, not an accreditation or an automatic finding that a product complies with Ugandan law. In a review, identify which risk is relevant, who owns it and what evidence supports the control."),
                    ("A practical risk-management workflow", "The NIST AI Risk Management Framework offers four connected functions. Govern establishes ownership and policy. Map describes context, affected people and risks. Measure tests and tracks relevant performance and risks. Manage prioritises and responds to the findings. An organisation can use these functions to structure its own review. A completed form is not proof that a control works; retain test results and the decision made from them."),
                    ("Applicable law is a separate question", "An insurer using AI must identify the laws, supervisory requirements and contractual obligations that apply to its actual activities and information processing. For Uganda, consult the current official data-protection instruments, insurance legislation and regulator material with the relevant legal or compliance owner. Do not infer that an activity is unregulated because there is no familiar AI-specific label. Avoid relying on an unverified section number, a proposal or a remembered regulatory deadline."),
                    ("International links require an applicability review", "A foreign framework can be a useful reference without being the law that governs a particular activity. The EU AI Act has its own territorial scope, system categories, exceptions and implementation dates. A firm with international operations needs a current applicability review rather than a blanket statement that every insurance AI tool is high-risk or that a customer location alone decides the answer. Use the current official text and record the reviewer, date and reasoning. This unit does not provide a definitive legal opinion or teach a fixed commencement date."),
                    ("Work through a governance review", "Consider a fictional insurer preparing claim summaries. The process owner describes the documents, users and intended result. The information-governance owner checks permitted processing and vendor terms. The reviewer tests the summaries against source documents and examines missing information, language errors and unsupported coverage statements. The manager decides whether the evidence justifies a bounded pilot and records conditions. A legal question that remains unresolved is assigned, not filled with an AI-generated answer."),
                    ("Apply the distinction", "Prepare a small table with four columns: source or framework, its role in your review, the evidence you need and the accountable owner. Separate a voluntary framework, international supervisory guidance, applicable law and the organisation's own policy. Name one question you cannot resolve from the supplied information and write the precise request you would send to the relevant owner. This makes the uncertainty actionable without pretending that the review itself grants approval.")
                ]
                assessment = [
                    {"prompt": "What is the appropriate use of the IAIS AI application paper in this lesson?", "options": ["A replacement for applicable law", "A source of supervisory guidance to inform an evidence-based review", "An automatic certification for an AI vendor"], "answer": 1, "explanation": "Guidance informs the review but does not certify the system or replace applicable requirements."},
                    {"prompt": "Who should own a consequential approval in the fictional summary pilot?", "options": ["An accountable authorised person", "The language model", "Whichever tool generated the most confident answer"], "answer": 0, "explanation": "The review needs a responsible person and a recorded decision."},
                    {"prompt": "Which NIST function establishes policies and responsibilities?", "options": ["Measure", "Map", "Govern"], "answer": 2, "explanation": "Govern establishes organisational ownership and risk-management responsibilities."},
                    {"prompt": "Which NIST function examines the use-case context and affected people?", "options": ["Map", "Measure", "Manage"], "answer": 0, "explanation": "Map describes the context and relevant risks."},
                    {"prompt": "Testing summaries against source documents primarily supports which function?", "options": ["Map", "Measure", "A claim of accreditation"], "answer": 1, "explanation": "Measure evaluates performance and risk using evidence."},
                    {"prompt": "Responding to failed tests and deciding whether to stop supports which function?", "options": ["Manage", "A sales approval", "Automatic policy issuance"], "answer": 0, "explanation": "Manage prioritises and responds to assessed risks."},
                    {"prompt": "A colleague cannot identify a familiar AI-specific law. What should follow?", "options": ["Assume no obligations apply", "Review the laws and requirements relevant to the actual activity and processing", "Ignore information-governance review"], "answer": 1, "explanation": "The technology label does not settle the legal obligations of the activity."},
                    {"prompt": "How should an international legal applicability question be resolved?", "options": ["Use a remembered deadline", "Let the model infer the answer from a country name", "Check current official sources and obtain an accountable applicability review"], "answer": 2, "explanation": "Scope and dates need a current review tied to the actual facts."},
                    {"prompt": "Does a completed governance form prove that controls work?", "options": ["Yes, if it is signed", "No; evidence and testing are still needed", "Yes, if the vendor is well known"], "answer": 1, "explanation": "A record of intended controls is not evidence of their actual operation."},
                    {"prompt": "What belongs in the review when an important legal issue remains unresolved?", "options": ["A plausible AI-generated answer", "An omitted row so the review looks complete", "The exact issue, assigned owner and next step before the affected action proceeds"], "answer": 2, "explanation": "Keep the unresolved dependency visible and route it to an accountable owner."}
                ]
            text = script(unit, plain(title), sections)
            if unit == "C01_U1-1":
                original = ROOT / "docs/iip-academy/course-library/01_Courses/C01_AI_and_Agentic_AI_for_Insurance/Unit_1.1/C01_U1-1_Narration_Script.md"
                text = original.read_text(encoding="utf-8-sig")
                replacement = "### SLIDE 13 - A practical division of responsibility\n\n**On-screen text:** AI prepares summaries and handovers · People check evidence and retain decision authority\n\n**Visual:** Preparation steps lead to an authorised human review.\n\nNARRATION:\nIn this course, we use a practical division of responsibility. A tool may help organise information, prepare a summary and route work for review. An authorised person checks the evidence and makes the consequential insurance decision. This is a design and governance boundary, not a claim about how many insurers have deployed a particular technology. When you assess a real tool, ask what it is allowed to do, which source supports its output and who can stop or correct it. A vendor's description or an industry trend cannot establish those facts for your organisation.\nEND NARRATION\n\n"
                text = re.sub(r"### SLIDE 13[^\n]*\n.*?(?=### SLIDE 14)", replacement, text, flags=re.S)
            destination = OUTPUT / unit
            destination.mkdir(exist_ok=True)
            (destination / "narration.md").write_text(text, encoding="utf-8")
            (destination / "quiz.instructor.json").write_text(json.dumps(assessment, ensure_ascii=False, indent=2), encoding="utf-8")
            learner = [{"id": index, "prompt": item["prompt"], "options": item["options"]} for index, item in enumerate(assessment)]
            (destination / "quiz.learner.json").write_text(json.dumps(learner, ensure_ascii=False, indent=2), encoding="utf-8")
            (destination / "research.md").write_text("# Source research pack\n\n" + block, encoding="utf-8")
            practice = "Prepare a fictional example for this unit, apply its method, identify the supporting source and record the correction or referral an authorised reviewer should make. Do not use real customer information."
            units.append({"id": unit, "title": plain(title), "module": int(module), "moduleTitle": MODULES[int(module)-1], "script": str((destination / "narration.md").relative_to(ROOT)).replace("\\", "/"), "questions": assessment, "practice": practice, "editorialStatus": "draft_requires_expert_review"})
    if len(units) != 12:
        raise ValueError("Expected twelve foundation units")
    manifest = {"id": "ai-agentic-insurance", "title": "AI & Agentic AI for Insurance", "priceUsd": 120, "units": units, "finalTask": "AI Use-Case Proposal and Governance Review", "publicationStatus": "not_ready_for_paid_release"}
    (OUTPUT / "course.instructor.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({"course": manifest["id"], "units": len(units), "unitQuestions": sum(len(unit["questions"]) for unit in units), "status": manifest["publicationStatus"]}))


if __name__ == "__main__":
    build()
