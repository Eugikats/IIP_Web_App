import argparse
import json
import subprocess
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--course", choices=("foundation", "sales", "both"), default="both")
    arguments = parser.parse_args()
    content = ROOT / "docs/iip-academy/flagship-content"
    destinations = ["foundation", "sales"] if arguments.course == "both" else [arguments.course]
    for folder in destinations:
        course = json.loads((content / folder / "course.instructor.json").read_text(encoding="utf-8"))
        for unit in course["units"]:
            script = ROOT / unit["script"]
            command = [sys.executable, str(ROOT / "scripts/academy/produce_lesson.py"), "--script", str(script), "--unit", unit["id"], "--course", "AI and Agentic AI for Insurance" if folder == "foundation" else "Insurance Sales and Customer Experience", "--max-cost", "20"]
            if arguments.dry_run:
                command.append("--dry-run")
            completed = subprocess.run(command, cwd=ROOT)
            if completed.returncode:
                raise SystemExit(f"Stopped at {unit['id']}; fix the failure before resuming. No failed unit was treated as complete.")


if __name__ == "__main__":
    main()
