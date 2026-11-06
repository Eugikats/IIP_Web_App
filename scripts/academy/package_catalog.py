import argparse
import copy
import json
import math
from pathlib import Path

from produce_lesson import parse_script


ROOT = Path(__file__).resolve().parents[2]


def clean(value):
    if isinstance(value, str):
        for _ in range(2):
            if any(marker in value for marker in ("\u00e2", "\u00c2", "\u00c3")):
                try:
                    value = value.encode("cp1252").decode("utf-8")
                except UnicodeError:
                    break
        return value.replace("\u2014", ", ").replace("\u2013", " to ")
    if isinstance(value, list):
        return [clean(item) for item in value]
    if isinstance(value, dict):
        return {key: clean(item) for key, item in value.items()}
    return value


def assessment(identifier, title, questions, maximum=0):
    return {"id": identifier, "title": title, "passMark": 70, "maxAttempts": maximum,
            "questions": [{"id": f"{identifier}-q{index+1}", "q": item["prompt"], "options": item["options"], "answer": item["answer"], "why": item["explanation"]}
                          for index, item in enumerate(questions)]}


def package(destination):
    document = json.loads(destination.read_text(encoding="utf-8"))
    foundation = next(course for course in document["courses"] if course["id"] == "ai-agentic-insurance")
    final_template = copy.deepcopy(foundation["finalTask"])
    for folder, course_id in [("foundation", "ai-agentic-insurance"), ("sales", "ai-sales-distribution")]:
        source = json.loads((ROOT / "docs/iip-academy/flagship-content" / folder / "course.instructor.json").read_text(encoding="utf-8"))
        course = next(course for course in document["courses"] if course["id"] == course_id)
        if len(source["units"]) != 12:
            raise ValueError("Expected twelve units")
        new_modules = []
        total_seconds = 0
        for module_number in range(1, 5):
            existing = course["modules"][module_number-1] if folder == "foundation" else {}
            module_id = existing.get("id", f"sales-module-{module_number}")
            units = [unit for unit in source["units"] if unit["module"] == module_number]
            lessons = []
            module_questions = []
            for unit_number, unit in enumerate(units, 1):
                if len(unit["questions"]) != 10 or len({item["prompt"] for item in unit["questions"]}) != 10:
                    raise ValueError(f"Duplicate or missing questions: {unit['id']}")
                for question in unit["questions"]:
                    if not 0 <= question["answer"] < len(question["options"]):
                        raise ValueError("Invalid answer")
                script = ROOT / unit["script"]
                slides = parse_script(script)
                media = json.loads((ROOT / "docs/iip-academy/media" / unit["id"] / "media.json").read_text())
                total_seconds += media["durationSeconds"]
                lesson = copy.deepcopy(existing["lessons"][unit_number-1]) if existing else {}
                lesson_id = lesson.get("id", f"sales-unit-{module_number}-{unit_number}")
                lesson.update({"id": lesson_id, "number": f"{module_number}.{unit_number}", "title": unit["title"],
                               "minutes": math.ceil(media["durationSeconds"] / 60), "mediaUnitId": unit["id"],
                               "video": None, "tryIt": unit["practice"],
                               "quiz": assessment(lesson_id+"-quiz", unit["title"]+": unit quiz", unit["questions"])})
                if not existing or unit["id"] == "C01_U4-2":
                    lesson["blocks"] = [block for slide in slides for block in [{"type": "h", "text": slide["title"]}, {"type": "p", "text": slide["narration"]}]]
                    lesson["outcome"] = unit.get("goal", "Apply this unit's method with evidence and accountable human review.")
                    lesson["sources"] = []
                else:
                    lesson["blocks"] = [block for block in lesson["blocks"] if not any(term in json.dumps(block) for term in ("Evident", "ScienceSoft", "UGX 2.024", "IRA projects premium growth"))]
                lessons.append(lesson)
                module_questions.extend(unit["questions"][:5])
            new_modules.append({"id": module_id, "title": units[0]["moduleTitle"], "lessons": lessons,
                                "assessment": assessment(module_id+"-assessment", f"Module {module_number}: independent assessment", module_questions, 3)})
        course.update({"title": source["title"], "modules": new_modules, "check": None,
                       "duration": f"{math.ceil(total_seconds/60)} minutes of recorded teaching, plus practice and assessments",
                       "contentStatus": "Pilot reviewer edition. All twelve units have recorded teaching and editable slides. Expert, assessment and subjective audio review remain pending. Caption timing is estimated. Not approved for paid release.",
                       "paidReleaseApproved": False,
                       "requirements": "Complete twelve unit quizzes and four module assessments at 70% or higher, then submit the final task for human marking. The final-task pass mark is 60%. Certificates of completion are not accredited qualifications."})
        if folder == "sales":
            task = copy.deepcopy(final_template)
            task.update({"id": course_id+"-final", "title": source["finalTask"],
                         "brief": "Build a fictional customer journey work pack covering acquisition and registration, qualification, outreach, human handover, service, reporting a loss and renewal. Propose a bounded pilot with evidence, owners, measurements and stop conditions. Do not use actual customer personal data.",
                         "deliverables": "Complete all ten canvas boxes and a 1,500 to 2,000 word proposal. Include a journey map, discovery record, qualification rules, a checked outreach draft, loss-reporting handover, service and renewal plan, authority boundaries, risk controls, baseline metrics and pilot decision criteria.",
                         "canvasBoxes": ["Journey and customer need", "Discovery evidence and unknowns", "Qualification and consent checks", "Outreach draft and review", "Registration and human handover", "Service and loss reporting", "Renewal and retention", "Authority and data controls", "Measures and denominators", "Pilot owner and stop criteria"],
                         "rubric": [{"criterion": title, "marks": 20, "distinction": description} for title, description in [
                             ("Journey and evidence", "A coherent journey, stated facts and visible unknowns"),
                             ("Customer work pack", "Useful checked drafts and role-appropriate handovers"),
                             ("Human authority and controls", "Clear decision rights, data boundaries and escalation"),
                             ("Measurement and pilot", "Comparable baselines, denominators, owners and stop conditions"),
                             ("Reasoning and practical application", "Specific choices, corrections and a feasible implementation")]]})
            course["finalTask"] = task
        cleaned = clean(course)
        course.clear()
        course.update(cleaned)
        print(f"Packaged {course_id}: 12 lessons, 120 unit questions, 60 module questions, human-marked final, reviewer edition")
    destination.write_text(json.dumps(document, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--catalogue", type=Path, required=True)
    package(parser.parse_args().catalogue)
