import hashlib
import json
import re
from pathlib import Path

from PIL import Image, ImageDraw
from produce_lesson import ENDPOINT, clean_stale_slide_cache, parse_script, probe, speech_payload


ROOT = Path(__file__).resolve().parents[2]
MEDIA = ROOT / "docs/iip-academy/media"
REPORT = ROOT / "docs/iip-academy/quality"


def main():
    REPORT.mkdir(exist_ok=True)
    results = []
    failures = []
    for folder in ("foundation", "sales"):
        course = json.loads((ROOT / "docs/iip-academy/flagship-content" / folder / "course.instructor.json").read_text(encoding="utf-8"))
        sheet = Image.new("RGB", (1600, 12 * 255), "white")
        for row, unit in enumerate(course["units"]):
            target = MEDIA / unit["id"]
            script = ROOT / unit["script"]
            slides = parse_script(script)
            clean_stale_slide_cache(target, len(slides))
            try:
                assert target.joinpath("transcript.md").read_bytes() == script.read_bytes(), "Stale transcript"
                for file in ("video.mp4", "audio.mp3", "captions.vtt", "slides.pptx", "transcript.md"):
                    assert (target / file).stat().st_size > 0, f"Missing or empty {file}"
                for slide in slides:
                    receipt = json.loads((target / f"slide-{slide['number']:02}.receipt.json").read_text())
                    expected = hashlib.sha256(speech_payload(slide["narration"]) + ENDPOINT.encode()).hexdigest()
                    assert receipt["fingerprint"] == expected, f"Stale narration slide {slide['number']}"
                duration = probe(target / "video.mp4")
                assert abs(duration - probe(target / "audio.mp3")) < 1, "Timing mismatch"
                assert (target / "captions.vtt").read_text(encoding="utf-8").startswith("WEBVTT"), "Invalid captions"
                metadata = json.loads((target / "media.json").read_text())
                metadata["sourceSha256"] = hashlib.sha256(script.read_bytes()).hexdigest()
                metadata["sourceAndAssetAuditPassed"] = True
                (target / "media.json").write_text(json.dumps(metadata, indent=2), encoding="utf-8")
                results.append({"course": course["id"], "unit": unit["id"], "slides": len(slides), "durationSeconds": duration, "sourceAndAssetAuditPassed": True})
            except (AssertionError, OSError, KeyError) as error:
                failures.append({"unit": unit["id"], "failure": str(error)})
            choices = [slides[0], slides[len(slides)//3], slides[2*len(slides)//3], slides[-1]]
            for column, slide in enumerate(choices):
                thumb = Image.open(target / f"slide-{slide['number']:02}.png").resize((400, 225))
                sheet.paste(thumb, (column*400, row*255+25))
            ImageDraw.Draw(sheet).text((8, row*255+5), unit["id"]+"  "+unit["title"], fill="black")
        sheet.save(REPORT / f"{folder}-slide-review.jpg", quality=88)
    ledger = json.loads((MEDIA / "narration-cost-ledger.json").read_text())
    output = {"units": results, "failures": failures, "reservedNarrationUsd": ledger["reservedUsd"], "narrationLimitUsd": 20,
              "totalVideoMinutes": round(sum(unit["durationSeconds"] for unit in results)/60, 1),
              "subjectiveAudioReview": "pending", "captionAlignmentReview": "pending", "paidReleaseApproved": False}
    (REPORT / "media-audit.json").write_text(json.dumps(output, indent=2), encoding="utf-8")
    print(json.dumps({key: value for key, value in output.items() if key != "units"}, indent=2))
    if failures:
        raise SystemExit("Media audit failed. Do not publish stale assets.")


if __name__ == "__main__":
    main()
