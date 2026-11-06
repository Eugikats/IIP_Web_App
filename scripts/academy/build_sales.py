import json
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
PLANS = ROOT / "docs/iip-academy/flagship-content/sales-lesson-plans.json"
OUTPUT = ROOT / "docs/iip-academy/flagship-content/sales"


def shorten(text, maximum=24):
    words = text.split()
    return " ".join(words[:maximum]) + ("..." if len(words) > maximum else "")


def make_questions(unit, title, steps, case):
    result = []
    for index, (heading, explanation) in enumerate(steps):
        first = re.split(r"(?<=[.!?])\s+", explanation)[0]
        second = re.split(r"(?<=[.!?])\s+", explanation)[-1]
        for variant, answer in [("best next step", first), ("review principle", second)]:
            choices = [answer, "Treat a fluent draft as sufficient authority and proceed without checking the relevant record.", "Replace missing facts with a plausible assumption so the worklist appears complete."]
            rotation = (index + (1 if variant == "review principle" else 0)) % 3
            choices = choices[rotation:] + choices[:rotation]
            result.append({"prompt": f"For {title.lower()}, which {variant} follows the lesson's '{heading.lower()}' method?", "options": choices, "answer": choices.index(answer), "explanation": explanation})
    result.extend([
        {"prompt": "In this unit's fictional case, what should a reviewer do before a customer-facing action?", "options": ["Confirm the relevant source, facts, unknowns, contact status and responsible next action.", "Assume the fictional example establishes the real customer's policy terms.", "Treat a prepared message as proof it was delivered."], "answer": 0, "explanation": case},
        {"prompt": "Which evidence best demonstrates that you can apply this unit rather than merely repeat its title?", "options": ["A claim that AI improved performance without a source or denominator.", "The completed practice work, with checked evidence, visible unknowns and an assigned next action.", "An unreviewed AI output copied directly into the customer record."], "answer": 1, "explanation": "The work product must show the reasoning and the checks. A confident assertion or an unreviewed draft is not evidence of competent application."},
    ])
    return result


def make_script(unit, lesson):
    slides = [(lesson["title"], "Understand the task · Work through a fictional case · Practise and check your reasoning", f"Welcome to {lesson['title']}. The learning goal is to {lesson['goal'][0].lower()+lesson['goal'][1:]} We will connect the method to a fictional insurance scenario, then ask you to prepare a work product you can check. The examples are not insurer product advice and do not establish that any feature is live.")]
    for heading, explanation in lesson["steps"]:
        sentences = re.split(r"(?<=[.!?])\s+", explanation)
        screen = " · ".join(shorten(sentence, 13) for sentence in (sentences[0], sentences[-1]))
        slides.append((heading, screen, explanation))
    slides.append(("Work through the fictional case", shorten(lesson["case"], 32), lesson["case"]))
    slides.append(("Prepare the practical work", shorten(lesson["task"], 32), lesson["task"] + " Pause the lesson and create the work in your own words. Use fictional information. Where a source or fact is missing, mark it as unresolved rather than filling the gap with an assumption. Keep your first draft so that you can compare it with the checked version."))
    slides.append(("Review your work", "Evidence · Meaning · Recipient · Authority · Next action", "Review your work in five passes. First, check each factual statement against the information supplied. Second, check that simplified wording has not changed the meaning. Third, check the intended recipient and whether the information is appropriate for them. Fourth, identify the authority required for any consequential action. Finally, make sure the next step has an owner and that unresolved questions remain visible. Record a correction you made and why it matters to the customer."))
    slides.append(("Compare an unsafe and a useful response", "Unsupported certainty hides a gap · A checked next step makes the gap manageable", "An unsafe work product can look complete because it substitutes certainty for missing evidence. A useful one says what is known, what remains unconfirmed and what an authorised person should do next. Apply that distinction to your own draft. If you have written that a policy is active, a price is final, a claim will be paid or an improvement is proven, identify the record that supports that statement. Remove or refer any conclusion that the source does not establish."))
    slides.append(("Your takeaways and independent quiz", "Explain the method · Complete the task · Take the ten-question quiz independently", f"The core outcome is to {lesson['goal'][0].lower()+lesson['goal'][1:]} Keep your completed practice work and the reasons for your corrections. Use the independent ten-question quiz to check your understanding. The unit is part of a broader course, so connect this method to the enquiry, service and review responsibilities you learned earlier. The assistant must not complete your assessment for you."))
    text = f"# {unit}: {lesson['title']}\n\nAuthored teaching draft. Fictional scenarios, not actual policy advice. Source and expert review remain part of release checks.\n\n"
    for index, (heading, screen, narration) in enumerate(slides, 1):
        text += f"### SLIDE {index:02} - {heading}\n\n**On-screen text:** {screen}\n\n**Visual:** Editable teaching content; emphasise the method or fictional case rather than a fabricated system screenshot.\n\nNARRATION:\n{narration}\nEND NARRATION\n\n"
    return text


def build():
    plan = json.loads(PLANS.read_text(encoding="utf-8"))
    OUTPUT.mkdir(parents=True, exist_ok=True)
    units = []
    for module_number, module in enumerate(plan["modules"], 1):
        for unit_number, lesson in enumerate(module["units"], 1):
            unit = f"C02_U{module_number}-{unit_number}"
            destination = OUTPUT / unit
            destination.mkdir(exist_ok=True)
            narration = (ROOT / lesson["existingScript"]).read_text(encoding="utf-8") if lesson.get("existingScript") else make_script(unit, lesson)
            (destination / "narration.md").write_text(narration, encoding="utf-8")
            questions = make_questions(unit, lesson["title"], lesson["steps"], lesson["case"])
            (destination / "quiz.instructor.json").write_text(json.dumps(questions, ensure_ascii=False, indent=2), encoding="utf-8")
            learner = [{"id": index, "prompt": question["prompt"], "options": question["options"]} for index, question in enumerate(questions)]
            (destination / "quiz.learner.json").write_text(json.dumps(learner, ensure_ascii=False, indent=2), encoding="utf-8")
            (destination / "practice.md").write_text(f"# Practice: {lesson['title']}\n\n{lesson['task']}\n\n## Check your work\n\nState the source, facts, unknowns, recipient, authority and next owner. Record at least one correction and the reason for it. Do not supply real customer information.\n", encoding="utf-8")
            units.append({"id": unit, "title": lesson["title"], "module": module_number, "moduleTitle": module["title"], "goal": lesson["goal"], "script": str((destination / "narration.md").relative_to(ROOT)).replace("\\", "/"), "practice": lesson["task"], "questions": questions, "editorialStatus": "draft_requires_expert_and_assessment_review"})
    course = {"id": plan["courseId"], "title": plan["title"], "priceUsd": 120, "units": units, "finalTask": "Customer journey work pack and bounded pilot plan", "publicationStatus": "not_ready_for_paid_release"}
    (OUTPUT / "course.instructor.json").write_text(json.dumps(course, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({"course": course["id"], "units": len(units), "unitQuestions": sum(len(unit["questions"]) for unit in units), "status": course["publicationStatus"]}))


if __name__ == "__main__":
    build()
