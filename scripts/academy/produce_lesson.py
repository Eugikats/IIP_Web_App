import argparse
import hashlib
import html
import json
import os
import re
import shutil
import subprocess
import sys
import textwrap
import threading
import time
import urllib.error
import urllib.request
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor

from PIL import Image, ImageDraw, ImageFont
from pptx import Presentation
from pptx.dml.color import RGBColor
from pptx.util import Inches, Pt


SUBSCRIPTION = "88c8b4ab-3510-4a8d-832c-73bf72088b5a"
ENDPOINT = "https://oticgroup-8430-resource.cognitiveservices.azure.com"
VOICE = "en-GB-Ada:DragonHDLatestNeural"
NAVY = "001C8C"
ORANGE = "FF6F00"
LEDGER_LOCK = threading.Lock()


def command(arguments):
    result = subprocess.run(arguments, capture_output=True, text=True, encoding="utf-8", errors="replace")
    if result.returncode:
        raise RuntimeError(f"Command failed: {Path(arguments[0]).name}; {result.stderr[-1200:]}")
    return result.stdout.strip()


def parse_script(path):
    content = path.read_text(encoding="utf-8-sig")
    slides = []
    pattern = r"^### SLIDE (\d+)\s*[-\u2014]\s*([^\n]+)\n(.*?)(?=^### SLIDE |\Z)"
    for match in re.finditer(pattern, content, re.M | re.S):
        body = match.group(3)
        narration = re.search(r"^NARRATION:\s*\n(.*?)^END NARRATION\s*$", body, re.M | re.S)
        screen = re.search(r"\*\*On-screen text:\*\*\s*(.*?)(?=\n\n|\Z)", body, re.S)
        if not narration or not screen:
            raise ValueError(f"Slide {match.group(1)} needs narration and on-screen text")
        slides.append({"number": int(match.group(1)), "title": match.group(2).strip(), "screen": screen.group(1).strip(), "narration": " ".join(narration.group(1).split())})
    if not slides or [slide["number"] for slide in slides] != list(range(1, len(slides) + 1)):
        raise ValueError("Slide numbers must be contiguous, starting at one")
    return slides


def speech_payload(text):
    return f'<speak version="1.0" xml:lang="en-GB"><voice name="{VOICE}">{html.escape(text)}</voice></speak>'.encode("utf-8")


def font(size, bold=False):
    windows = Path(os.environ.get("WINDIR", "C:/Windows")) / "Fonts"
    path = windows / ("segoeuib.ttf" if bold else "segoeui.ttf")
    return ImageFont.truetype(str(path), size)


def wrap(draw, text, face, width):
    lines = []
    current = ""
    for word in text.split():
        candidate = f"{current} {word}".strip()
        if draw.textlength(candidate, font=face) > width and current:
            lines.append(current)
            current = word
        else:
            current = candidate
    if current:
        lines.append(current)
    return lines


def clean(text):
    return text.replace("\u2014", ":").replace("**", "").strip('"')


def slide_image(slide, unit, course, destination):
    canvas = Image.new("RGB", (1600, 900), "white")
    draw = ImageDraw.Draw(canvas)
    draw.text((90, 45), "IIP ACADEMY", font=font(24, True), fill=f"#{NAVY}")
    draw.text((1350, 45), unit, font=font(22), fill="#475569")
    draw.line((90, 98, 1510, 98), fill="#D9E1ED", width=2)
    heading = wrap(draw, clean(slide["title"]), font(56, True), 1390)
    if len(heading) > 3:
        raise ValueError(f"Slide {slide['number']} title overflows")
    top = 135
    for line in heading:
        draw.text((90, top), line, font=font(56, True), fill=f"#{NAVY}")
        top += 72
    draw.line((90, top + 10, 180, top + 10), fill=f"#{ORANGE}", width=5)
    top += 65
    parts = [part.strip() for part in re.split(r"\s*[·\n]\s*", clean(slide["screen"])) if part.strip()]
    if not parts:
        raise ValueError("Empty slide")
    line_count = sum(len(wrap(draw, part, font(36), 1280)) for part in parts)
    if line_count * 48 + len(parts) * 22 > 795 - top:
        raise ValueError(f"Slide {slide['number']} body overflows; edit source text")
    for index, part in enumerate(parts):
        draw.text((93, top), f"{index + 1:02}", font=font(25, True), fill=f"#{ORANGE}")
        for line in wrap(draw, part, font(36), 1280):
            draw.text((160, top - 4), line, font=font(36), fill="#26334D")
            top += 48
        top += 22
    draw.line((90, 820, 1510, 820), fill="#D9E1ED", width=2)
    draw.text((90, 842), course, font=font(20), fill="#475569")
    draw.text((1405, 842), f"{slide['number']:02}", font=font(23, True), fill=f"#{NAVY}")
    canvas.save(destination)


def presentation(slides, unit, destination):
    deck = Presentation()
    deck.slide_width = Inches(16)
    deck.slide_height = Inches(9)
    for slide in slides:
        page = deck.slides.add_slide(deck.slide_layouts[6])
        for position, text, size, bold, colour in [
            ((0.9, 0.4, 14, 0.4), f"IIP ACADEMY | {unit}", 17, True, NAVY),
            ((0.9, 1.3, 14, 1.6), clean(slide["title"]), 34, True, NAVY),
            ((1.1, 3.2, 13.8, 4.2), clean(slide["screen"]).replace(" · ", "\n\n"), 25, False, "26334D"),
            ((0.9, 8.3, 14, 0.3), f"Otic Group | {slide['number']:02}", 13, False, "475569"),
        ]:
            box = page.shapes.add_textbox(*[Inches(value) for value in position])
            box.text_frame.word_wrap = True
            for index, line in enumerate(text.split("\n")):
                paragraph = box.text_frame.paragraphs[0] if index == 0 else box.text_frame.add_paragraph()
                paragraph.text = line
                paragraph.font.name = "Segoe UI"
                paragraph.font.size = Pt(size)
                paragraph.font.bold = bold
                paragraph.font.color.rgb = RGBColor.from_string(colour)
        page.notes_slide.notes_text_frame.text = slide["narration"]
    deck.save(destination)


def probe(path):
    return float(command(["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "default=nw=1:nk=1", str(path)]))


def timestamp(seconds, separator="."):
    milliseconds = round(seconds * 1000)
    hours, remainder = divmod(milliseconds, 3600000)
    minutes, remainder = divmod(remainder, 60000)
    whole, fraction = divmod(remainder, 1000)
    return f"{hours:02}:{minutes:02}:{whole:02}{separator}{fraction:03}"


def synthesise(slide, output, token, ledger_path, maximum):
    payload = speech_payload(slide["narration"])
    fingerprint = hashlib.sha256(payload + ENDPOINT.encode()).hexdigest()
    receipt = output.with_suffix(".receipt.json")
    if output.exists() and receipt.exists() and json.loads(receipt.read_text())["fingerprint"] == fingerprint:
        probe(output)
        return
    estimate = len(payload.decode("utf-8")) * 22 / 1_000_000
    for attempt in range(4):
        with LEDGER_LOCK:
            ledger = json.loads(ledger_path.read_text()) if ledger_path.exists() else {"reservedUsd": 0.0, "attempts": []}
            if ledger["reservedUsd"] + estimate > maximum:
                raise RuntimeError("Aggregate narration budget would be exceeded")
            ledger["reservedUsd"] += estimate
            ledger["attempts"].append({"fingerprint": fingerprint, "charactersIncludingSsml": len(payload.decode("utf-8")), "reservedUsd": estimate, "time": time.time()})
            temporary_ledger = ledger_path.with_suffix(".tmp")
            temporary_ledger.write_text(json.dumps(ledger, indent=2), encoding="utf-8")
            temporary_ledger.replace(ledger_path)
        request = urllib.request.Request(ENDPOINT + "/tts/cognitiveservices/v1", data=payload, headers={"Authorization": "Bearer " + token, "Content-Type": "application/ssml+xml; charset=utf-8", "X-Microsoft-OutputFormat": "audio-24khz-96kbitrate-mono-mp3", "User-Agent": "otic-academy-production"})
        try:
            with urllib.request.urlopen(request, timeout=180) as response:
                data = response.read()
            temporary_audio = output.with_suffix(".partial.mp3")
            temporary_audio.write_bytes(data)
            if probe(temporary_audio) <= 0:
                raise RuntimeError("Synthesised audio has no duration")
            temporary_audio.replace(output)
            receipt.write_text(json.dumps({"fingerprint": fingerprint, "voice": VOICE, "subscription": SUBSCRIPTION, "durationSeconds": probe(output)}, indent=2), encoding="utf-8")
            return
        except urllib.error.HTTPError as error:
            if error.code not in (429, 500, 502, 503) or attempt == 3:
                raise RuntimeError(f"Azure Speech returned HTTP {error.code}; no response body or token logged") from None
            time.sleep(2 ** (attempt + 1))


def clean_stale_slide_cache(target, count):
    resolved = target.resolve()
    for file in target.glob("slide-*"):
        match = re.fullmatch(r"slide-(\d+)\.(png|mp3|receipt\.json)", file.name)
        if match and int(match.group(1)) > count:
            if file.resolve().parent != resolved:
                raise RuntimeError("Refusing to remove a cache outside the lesson folder")
            file.unlink()


def produce(args):
    slides = parse_script(args.script)
    target = args.output / args.unit
    target.mkdir(parents=True, exist_ok=True)
    clean_stale_slide_cache(target, len(slides))
    estimated = sum(len(speech_payload(slide["narration"]).decode("utf-8")) for slide in slides) * 22 / 1_000_000
    print(json.dumps({"unit": args.unit, "slides": len(slides), "words": sum(len(slide["narration"].split()) for slide in slides), "estimatedHdUsd": round(estimated, 4)}), flush=True)
    shutil.copy2(args.script, target / "transcript.md")
    presentation(slides, args.unit, target / "slides.pptx")
    for slide in slides:
        slide_image(slide, args.unit, args.course, target / f"slide-{slide['number']:02}.png")
    if args.dry_run:
        return
    azure = shutil.which("az") or shutil.which("az.cmd")
    if not azure:
        raise RuntimeError("Azure CLI is required")
    token = command([azure, "account", "get-access-token", "--resource", "https://cognitiveservices.azure.com", "--subscription", SUBSCRIPTION, "--query", "accessToken", "-o", "tsv"])
    ledger = args.output / "narration-cost-ledger.json"
    def generate(slide):
        synthesise(slide, target / f"slide-{slide['number']:02}.mp3", token, ledger, args.max_cost)
        print(f"Audio verified: {args.unit} slide {slide['number']:02}", flush=True)
    with ThreadPoolExecutor(max_workers=args.jobs) as executor:
        list(executor.map(generate, slides))
    audio_list = target / "audio-concat.txt"
    audio_paths = [target / "slide-{:02}.mp3".format(slide["number"]) for slide in slides]
    audio_list.write_text("\n".join("file '{}'".format(path.resolve().as_posix()) for path in audio_paths), encoding="utf-8")
    command(["ffmpeg", "-y", "-v", "error", "-f", "concat", "-safe", "0", "-i", str(audio_list), "-c:a", "libmp3lame", "-b:a", "96k", str(target / "audio.mp3")])
    image_entries = []
    captions = ["WEBVTT", ""]
    timing = []
    current = 0.0
    for slide in slides:
        duration = probe(target / f"slide-{slide['number']:02}.mp3")
        image_path = target / "slide-{:02}.png".format(slide["number"])
        image_entries.extend(["file '{}'".format(image_path.resolve().as_posix()), f"duration {duration:.6f}"])
        sentences = re.split(r"(?<=[.!?])\s+", slide["narration"])
        chunks = []
        for sentence in sentences:
            chunks.extend(textwrap.wrap(sentence, width=100, break_long_words=False, break_on_hyphens=False))
        weight = sum(len(chunk) for chunk in chunks)
        position = current
        for chunk in chunks:
            ending = position + duration * len(chunk) / weight
            captions.extend([f"{timestamp(position)} --> {timestamp(ending)}", chunk, ""])
            position = ending
        timing.append({"slide": slide["number"], "start": current, "duration": duration, "title": slide["title"]})
        current += duration
    image_entries.append(image_entries[-2])
    (target / "images-concat.txt").write_text("\n".join(image_entries), encoding="utf-8")
    (target / "captions.vtt").write_text("\n".join(captions), encoding="utf-8")
    (target / "timings.json").write_text(json.dumps(timing, indent=2), encoding="utf-8")
    command(["ffmpeg", "-y", "-v", "error", "-f", "concat", "-safe", "0", "-i", str(target / "images-concat.txt"), "-i", str(target / "audio.mp3"), "-vf", "fps=10,format=yuv420p", "-c:v", "libx264", "-preset", "veryfast", "-crf", "23", "-c:a", "aac", "-b:a", "96k", "-movflags", "+faststart", "-t", str(probe(target / "audio.mp3")), str(target / "video.mp4")])
    video_duration = probe(target / "video.mp4")
    if abs(video_duration - probe(target / "audio.mp3")) > 1.0:
        raise RuntimeError("Video and narration durations differ")
    command(["ffmpeg", "-v", "error", "-i", str(target / "video.mp4"), "-f", "null", "-"])
    (target / "media.json").write_text(json.dumps({"unit": args.unit, "sourceSha256": hashlib.sha256(args.script.read_bytes()).hexdigest(), "voice": VOICE, "durationSeconds": video_duration, "slides": len(slides), "technicalDecodePassed": True, "captionTiming": "estimated from narration text; requires editorial review", "subjectiveAudioReview": "pending", "visualReview": "pending"}, indent=2), encoding="utf-8")
    print(f"Rendered and decoded: {args.unit}, {video_duration:.1f} seconds", flush=True)


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser()
    parser.add_argument("--script", type=Path, required=True)
    parser.add_argument("--unit", required=True)
    parser.add_argument("--course", required=True)
    parser.add_argument("--output", type=Path, default=Path("docs/iip-academy/media"))
    parser.add_argument("--max-cost", type=float, default=20.0)
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--jobs", type=int, choices=(1, 2, 3, 4), default=3)
    arguments = parser.parse_args()
    if arguments.max_cost <= 0 or arguments.max_cost > 20:
        parser.error("Narration limit must be greater than zero and at most USD 20")
    produce(arguments)
