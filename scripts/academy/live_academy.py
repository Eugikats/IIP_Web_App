import argparse
import json
import shutil
import subprocess
import time
from pathlib import Path

import requests


ROOT = Path(__file__).resolve().parents[2]
API = "https://app-api-dev-h5girbnzrayqe.azurewebsites.net"
SUBSCRIPTION = "5f5765ae-985f-42e4-a443-8e2447be33ca"
COURSES = ("ai-agentic-insurance", "ai-sales-distribution")
EUGENE = "6aac969e-d03e-4380-baa5-7e605b24949d"


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--configure", action="store_true")
    parser.add_argument("--verify-media", action="store_true")
    args = parser.parse_args()
    result = subprocess.run([shutil.which("az") or shutil.which("az.cmd"), "account", "get-access-token", "--resource",
                             "api://d55fe46b-1fba-4c5b-81e1-4ee1918ecf73", "--subscription", SUBSCRIPTION,
                             "--query", "accessToken", "-o", "tsv"], capture_output=True, text=True, check=True)
    token = result.stdout.strip()
    session = requests.Session()

    def request(path, method="GET", data=None, authenticated=True, byte_range=False):
        headers = {"Authorization": "Bearer "+token} if authenticated else {}
        body = None
        if data is not None:
            headers["Content-Type"] = "application/json"
            body = json.dumps(data).encode()
        if byte_range:
            headers["Range"] = "bytes=0-1023"
        for attempt in range(3):
            try:
                response = session.request(method, API+path, data=body, headers=headers, timeout=(15, 45))
                if method == "GET" and response.status_code in (429, 502, 503, 504) and attempt < 2:
                    time.sleep(min(60, max(5, int(response.headers.get("Retry-After", "5")))))
                    continue
                if not response.ok:
                    raise RuntimeError(f"{method} {path}: HTTP {response.status_code}; response body omitted")
                if byte_range:
                    return {"status": response.status_code, "bytes": len(response.content), "contentType": response.headers.get("Content-Type")}
                return response.json() if response.content else {}
            except (requests.Timeout, requests.ConnectionError):
                if method != "GET" or attempt == 2:
                    raise RuntimeError(f"{method} {path}: network timeout; no response or token logged") from None
                time.sleep(5)

    me = request("/api/v1/me")
    public = request("/api/v1/academy/public/courses", authenticated=False)
    selected = [course for course in public["courses"] if course["id"] in COURSES]
    report = {"subscription": SUBSCRIPTION, "currentOperator": me["userId"], "courses": selected, "testerGrants": [], "media": []}
    if args.configure:
        request("/api/v1/academy/pricing", "PUT", {"prices": [{"courseId": course, "amount": 120} for course in COURSES]})
        for course in COURSES:
            for user_id in (EUGENE, me["userId"]):
                grant = request("/api/v1/academy/operator/enrol", "POST", {"courseId": course, "userId": user_id})
                report["testerGrants"].append({"courseId": course, "userId": user_id, **grant})
    if args.verify_media:
        for course in COURSES:
            detail = request("/api/v1/academy/public/courses/"+course, authenticated=False)
            assert detail["price"] == 120
            assert detail.get("paidReleaseApproved") is False
            assert len(detail["modules"]) == 4
            for module in detail["modules"]:
                assert len(module["lessons"]) == 3
                for lesson in module["lessons"]:
                    data = request(f"/api/v1/academy/courses/{course}/lessons/{lesson['id']}")
                    assert data.get("mediaUnitId")
                    assert "answer" not in data
                    for file in ("video.mp4", "audio.mp3", "captions.vtt", "slides.pptx", "transcript.md"):
                        response = request(f"/api/v1/academy/courses/{course}/lessons/{lesson['id']}/media/{file}", byte_range=True)
                        assert response["status"] in (200, 206) and response["bytes"] > 0
                        report["media"].append({"courseId": course, "lessonId": lesson["id"], "file": file, **response})
                    print(f"Verified live media: {course}, {lesson['id']}", flush=True)
    output = ROOT / "docs/iip-academy/quality" / ("azure-media-verification.json" if args.verify_media else "azure-access-and-pricing.json")
    output.write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps({"courses": [{"id": item["id"], "lessons": item["lessons"], "price": item["price"]} for item in selected],
                      "configuredGrants": len(report["testerGrants"]), "verifiedMediaRequests": len(report["media"])}))


if __name__ == "__main__":
    main()
