# Azure text-to-speech for Otic narration: agent guide

**Audience:** coding agents (Codex and similar) that need to turn narration text into audio using Otic's Azure subscriptions.
**Verified live:** 2026-10-19, by a human-supervised Claude session on the Otic Windows machine. Section 12 lists exactly what was and was not tested.
**Cost of everything in this guide's tests:** about 7 cents.

## 1. Read this first

These rules come from `agenticIip-v2` (`CLAUDE.md`, `docs/05-deployment-release-enablement/AZURE_DEPLOYMENT_AND_ENVIRONMENTS.md`, `docs/01-strategy-requirements-roadmap/azure_fixed_costs.md`) and from what went wrong in this session.

1. **Use only existing resource groups and existing resources.** Never create a resource group. Never delete, rename, or overwrite anything. `agenticIip-v2` is explicit: do not touch `rg-iip-dev`, `rg-iip-stg`, or any unrelated resource.
2. **Never print, log, commit, or paste a key or token.** Use keyless Microsoft Entra authentication (section 4). If you must use a key, read it into a shell variable at runtime and never echo it.
3. **Set the subscription explicitly** with `az account set --subscription <id>` before every session. The CLI default is often the wrong one, and the agenticIip docs say the same.
4. **Confirm live with `az`, do not trust this document blindly.** Azure resources in this tenant have been deleted and renamed out of band before. Run the checks in section 5 first.
5. **Do not assume anything here is free.** See section 3. Estimate the cost first (the script has `--dry-run`) and stay under the guard.
6. **Never use the Long Audio API.** It costs about 6.7 times more than the normal endpoint for the same speech (section 9). Send text in blocks of a few hundred to a few thousand characters instead.
7. **If something needs a new Azure resource, stop and ask a human.** Section 11 explains why.

## 2. Quick answer

| Question | Answer |
|---|---|
| Do we have text-to-speech on Azure? | Yes, three working resources, all in subscription `88c8b4ab-3510-4a8d-832c-73bf72088b5a`. |
| Which one should I use? | `oticgroup-8430-resource` in `rg-OticGroup-8430` (region `eastus2`). |
| How do I authenticate? | Keyless, with your `az` login. No key needed. |
| Most natural voice | `en-GB-Ada:DragonHDLatestNeural` (HD). |
| Cheapest good voice | `en-GB-SoniaNeural` (standard Neural). |
| East African accent | `en-KE-AsiliaNeural`, `en-KE-ChilembaNeural`, `en-TZ-ImaniNeural`, `en-TZ-ElimuNeural`. No Ugandan voice exists. |
| Cost per minute of audio | Neural about **1.4 US cents**. HD about **1.9 US cents**. |
| Cost of a 9 to 10 minute unit | Neural about **13 cents**. HD about **19 cents**. |

## 3. Subscriptions and resource groups

The tenant has four subscriptions. The signed-in identity is `OticGroup@oticgroupug.onmicrosoft.com` (home tenant `dcb0b132-fb9e-475c-a68e-b0671222e8d8`).

| Subscription | ID | Live offer (`quotaId`) | Role held | Use for TTS? |
|---|---|---|---|---|
| Microsoft Azure Sponsorship (second one) | `88c8b4ab-3510-4a8d-832c-73bf72088b5a` | `PayAsYouGo_2014-09-01`, spending limit **Off** | Owner | **Yes. Use this one.** |
| Microsoft Azure Sponsorship (first one, IIP) | `5f5765ae-985f-42e4-a443-8e2447be33ca` | `PayAsYouGo_2014-09-01`, spending limit **Off** | Owner | No Speech resources here. Holds IIP (`rg-iip-dev`). Do not touch IIP resources. |
| Azure subscription 1 | `2f3dc107-3c11-42ed-ae94-99e1a05c20e4` | `Sponsored_2016-01-01` | Managed Application Publisher Operator only | **No.** Its only groups are `mrg-cip-credit-intelligence-pl-*`, which are Marketplace managed-application groups. |
| Azure subscription 1 (older) | `f1f1efb6-ea97-4807-9757-197a41e292f5` | Disabled | none | Ignore. It does not resolve. |

### "Free subscription" is not safe to assume

`agenticIip-v2` describes `5f5765ae` as a "Sponsored subscription (spending-limit tier)". The live Azure API disagrees: both "Microsoft Azure Sponsorship" subscriptions report `PayAsYouGo_2014-09-01` with the spending limit **Off**, and `azure_fixed_costs.md` (2026-10-12) found an individual pay-as-you-go billing account billed at list price, with no negotiated rates. Credits can still be applied at billing level, and nothing in the CLI shows that. **Treat all spend as real money** until a human confirms the credit balance in the portal (Cost Management, Credits). The costs in this guide are small, so this is about discipline, not alarm.

### Text-to-speech resources, existing resource groups only

All three are in subscription `88c8b4ab-3510-4a8d-832c-73bf72088b5a`.

| Priority | Resource | Kind / SKU | Resource group | Region | Custom-domain endpoint |
|---|---|---|---|---|---|
| 1 | `oticgroup-8430-resource` | AIServices / S0 | `rg-OticGroup-8430` | eastus2 | `https://oticgroup-8430-resource.cognitiveservices.azure.com` |
| 2 | `ai-oticgroup1814ai117706710636` | AIServices / S0 | `rg-OticGroup-8977_ai` | eastus2 | `https://ai-oticgroup1814ai117706710636.cognitiveservices.azure.com` |
| 3 (last resort) | `pdip-dev-speech` | SpeechServices / S0 | `pdip-dev-rg` | eastus | `https://pdipdevspeecheqs6ngguumo4g.cognitiveservices.azure.com` |

Why that order: priority 1 and 2 are named for Otic Group. Priority 3 belongs to the PDIP project, so using it bills PDIP's resource and shares its quota. All three have public network access enabled.

Prices and HD voice availability were confirmed identical for East US and East US 2.

## 4. Sign in

```bash
az login --use-device-code
```

1. The command prints a URL and a one-time code. **Relay both to the human** and wait. You cannot complete this step yourself.
2. A code is single use and expires within minutes. If it expires, start a new login and use the new code. Entering an old code fails with `invalid_grant`.
3. Do not start a second login while one is waiting. The first fails with `AADSTS70016 authorization pending`.
4. Then pin the subscription and confirm:

```bash
az account set --subscription 88c8b4ab-3510-4a8d-832c-73bf72088b5a
az account show --query "{name:name,id:id,state:state}" -o json
```

Windows notes: in Git Bash, any argument starting with `/subscriptions/...` is silently rewritten as a Windows path and fails with `MissingSubscription`. Prefix the command with `MSYS_NO_PATHCONV=1`. Set `PYTHONIOENCODING=utf-8` before running Python, because the default Windows code page crashes on non-ASCII characters.

## 5. Find and check the resource

```bash
az cognitiveservices account list --query "[?kind=='SpeechServices' || kind=='AIServices'].{name:name,kind:kind,rg:resourceGroup,loc:location,sku:sku.name}" -o table

az cognitiveservices account show -n oticgroup-8430-resource -g rg-OticGroup-8430 \
  --query "{state:properties.provisioningState,endpoint:properties.endpoint,publicAccess:properties.publicNetworkAccess}" -o json
```

Expect `state: Succeeded`. If the resource is missing or renamed, fall to priority 2, then 3. If all are missing, stop and ask a human (section 11).

## 6. Synthesise one clip (smoke test)

Keyless. Use the **custom-domain** endpoint. The regional endpoint (`https://<region>.tts.speech.microsoft.com`) rejects Entra bearer tokens with HTTP 401, which was observed here.

```bash
TOKEN=$(az account get-access-token --resource https://cognitiveservices.azure.com \
  --subscription 88c8b4ab-3510-4a8d-832c-73bf72088b5a --query accessToken -o tsv)

curl -s -X POST "https://oticgroup-8430-resource.cognitiveservices.azure.com/tts/cognitiveservices/v1" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/ssml+xml; charset=utf-8" \
  -H "X-Microsoft-OutputFormat: audio-24khz-96kbitrate-mono-mp3" \
  -H "User-Agent: otic-tts" \
  -d "<speak version='1.0' xml:lang='en-GB'><voice name='en-GB-Ada:DragonHDLatestNeural'>This is a test.</voice></speak>" \
  -o test.mp3 -w "HTTP %{http_code}\n"
```

Pass criteria: `HTTP 200`, and `test.mp3` starts with the bytes `ff f3` (an MPEG frame). A 3 to 4 second clip is roughly 30 to 40 KB. Anything else is a failure, so read `test.mp3` as text for the error message.

List the voices a resource offers (also keyless):

```bash
curl -s "https://oticgroup-8430-resource.cognitiveservices.azure.com/tts/cognitiveservices/voices/list" \
  -H "Authorization: Bearer $TOKEN" -o voices.json
```

`oticgroup-8430-resource` returned 953 voices, 125 of them HD. Note the colon in HD voice names (`en-GB-Ada:DragonHDLatestNeural`). It is part of the name, so quote it.

Other output formats exist (for example `riff-24khz-16bit-mono-pcm` for WAV). Only the MP3 format above was tested.

## 7. Batch script for narration files

The narration markdown files in this project use blocks like this:

```
NARRATION:
Text to be spoken goes here.
END NARRATION
```

Only the text inside the blocks is spoken. The rest of the file (on-screen text, visual notes) must not be sent to the service. In the unit 1.1 script, only 61% of the characters are narration, so sending the whole file would cost about 63% more and read out stage directions.

Save this as `tts_narration.py`. It uses only the Python standard library, authenticates keylessly through `az`, prints an estimate first, and refuses to run above `--max-cost` (default $1.00).

```python
#!/usr/bin/env python3
"""Render the NARRATION blocks of a narration markdown file to MP3 with Azure Speech.

Keyless: authenticates with the signed-in `az` identity (Microsoft Entra), so no
API key is ever read, printed, or stored. Standard library only.

    python tts_narration.py --file script.md --dry-run
    python tts_narration.py --file script.md --preset hd --out out/
    python tts_narration.py --file script.md --slides 1,2 --preset neural
"""
import argparse
import html
import pathlib
import re
import shutil
import subprocess
import sys
import time
import urllib.error
import urllib.request

SUB = "88c8b4ab-3510-4a8d-832c-73bf72088b5a"  # "Microsoft Azure Sponsorship" (second one)

# Existing resources only. Never create a resource group for this.
RESOURCES = {
    "otic-8430": "https://oticgroup-8430-resource.cognitiveservices.azure.com",       # rg-OticGroup-8430, eastus2
    "otic-8977": "https://ai-oticgroup1814ai117706710636.cognitiveservices.azure.com",  # rg-OticGroup-8977_ai, eastus2
    "pdip": "https://pdipdevspeecheqs6ngguumo4g.cognitiveservices.azure.com",         # pdip-dev-rg, eastus (last resort)
}

# USD per 1M characters, list price, East US and East US 2 (retail prices API, 2026-10-19).
PRICE_PER_M = {"neural": 15.0, "hd": 22.0}
# Measured on real narration (en-GB voices): 881 to 969 characters per minute of audio.
CHARS_PER_MIN = 900

PRESETS = {
    "hd": ("en-GB-Ada:DragonHDLatestNeural", "hd"),
    "hd-male": ("en-GB-Ollie:DragonHDLatestNeural", "hd"),
    "neural": ("en-GB-SoniaNeural", "neural"),
    "neural-male": ("en-GB-RyanNeural", "neural"),
    "kenya": ("en-KE-AsiliaNeural", "neural"),
    "kenya-male": ("en-KE-ChilembaNeural", "neural"),
}

BLOCK = re.compile(r"NARRATION:\s*\n(.*?)\nEND NARRATION", re.S)


def az_token() -> str:
    az = shutil.which("az")
    if not az:
        sys.exit("az CLI not found on PATH. Install it, then run: az login --use-device-code")
    out = subprocess.run(
        [az, "account", "get-access-token", "--resource", "https://cognitiveservices.azure.com",
         "--subscription", SUB, "--query", "accessToken", "-o", "tsv"],
        capture_output=True, text=True,
    )
    token = out.stdout.strip()
    if out.returncode != 0 or not token:
        sys.exit("Could not get a token. Run: az login --use-device-code\n" + out.stderr.strip())
    return token


def synth(endpoint: str, token: str, voice: str, text: str, fmt: str) -> bytes:
    ssml = (
        "<speak version='1.0' xml:lang='en-GB' xmlns='http://www.w3.org/2001/10/synthesis'>"
        f"<voice name='{voice}'>{html.escape(text)}</voice></speak>"
    )
    req = urllib.request.Request(
        f"{endpoint}/tts/cognitiveservices/v1",
        data=ssml.encode("utf-8"),
        headers={
            "Authorization": f"Bearer {token}",
            "Content-Type": "application/ssml+xml; charset=utf-8",
            "X-Microsoft-OutputFormat": fmt,
            "User-Agent": "otic-tts-narration",
        },
        method="POST",
    )
    for attempt in range(5):
        try:
            with urllib.request.urlopen(req, timeout=120) as resp:
                return resp.read()
        except urllib.error.HTTPError as e:
            if e.code in (429, 500, 502, 503, 504) and attempt < 4:
                time.sleep(2 ** attempt)  # backoff: 1, 2, 4, 8 seconds
                continue
            sys.exit(f"Speech request failed: HTTP {e.code} {e.read().decode('utf-8', 'replace')[:300]}")
    raise RuntimeError("unreachable")


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--file", required=True, help="narration markdown with NARRATION: ... END NARRATION blocks")
    ap.add_argument("--preset", choices=sorted(PRESETS), default="neural")
    ap.add_argument("--voice", help="override the voice name, e.g. en-GB-SoniaNeural")
    ap.add_argument("--tier", choices=sorted(PRICE_PER_M), help="price tier when --voice is used")
    ap.add_argument("--resource", choices=sorted(RESOURCES), default="otic-8430")
    ap.add_argument("--out", default="narration_out")
    ap.add_argument("--slides", help="comma list of 1-based slide numbers, e.g. 1,2,5")
    ap.add_argument("--format", default="audio-24khz-96kbitrate-mono-mp3")
    ap.add_argument("--max-cost", type=float, default=1.00, help="refuse to run above this estimate (USD)")
    ap.add_argument("--dry-run", action="store_true", help="print the estimate and stop; nothing is synthesised")
    args = ap.parse_args()

    voice, tier = PRESETS[args.preset]
    if args.voice:
        voice = args.voice
        tier = args.tier or tier

    text = pathlib.Path(args.file).read_text(encoding="utf-8", errors="replace")
    blocks = [b.strip() for b in BLOCK.findall(text)]
    if not blocks:
        sys.exit("No NARRATION: ... END NARRATION blocks found.")

    wanted = range(1, len(blocks) + 1)
    if args.slides:
        wanted = [int(n) for n in args.slides.split(",")]
    chosen = [(n, blocks[n - 1]) for n in wanted if 1 <= n <= len(blocks)]

    chars = sum(len(b) for _, b in chosen)
    cost = chars / 1_000_000 * PRICE_PER_M[tier]
    print(f"slides: {len(chosen)} of {len(blocks)}   characters: {chars:,}   "
          f"~{chars / CHARS_PER_MIN:.1f} min of audio")
    print(f"voice: {voice} ({tier})   estimated cost: ${cost:.4f}  (list price, USD)")
    print(f"resource: {args.resource}   longest block: {max(len(b) for _, b in chosen):,} chars")
    if args.dry_run:
        return
    if cost > args.max_cost:
        sys.exit(f"Estimate ${cost:.2f} is above --max-cost ${args.max_cost:.2f}. Raise it deliberately to continue.")

    out = pathlib.Path(args.out)
    out.mkdir(parents=True, exist_ok=True)
    token = az_token()
    started = time.time()
    for n, block in chosen:
        if time.time() - started > 45 * 60:  # tokens last about an hour
            token, started = az_token(), time.time()
        audio = synth(RESOURCES[args.resource], token, voice, block, args.format)
        path = out / f"slide_{n:02d}.mp3"
        path.write_bytes(audio)
        print(f"  slide {n:02d}: {len(block):4d} chars -> {len(audio):7,d} bytes  {path}")
    print("done. Join with ffmpeg if needed: ffmpeg -f concat -safe 0 -i list.txt -c copy unit.mp3")


if __name__ == "__main__":
    main()
```

Usage:

```bash
export PYTHONIOENCODING=utf-8
python tts_narration.py --file "C01_U1-1_Narration_Script.md" --preset hd --dry-run   # estimate only
python tts_narration.py --file "C01_U1-1_Narration_Script.md" --preset hd --slides 1,2 --out out/   # try two slides
python tts_narration.py --file "C01_U1-1_Narration_Script.md" --preset hd --out out/               # whole unit
```

Presets: `hd`, `hd-male`, `neural`, `neural-male`, `kenya`, `kenya-male`. Override with `--voice` and `--tier`. Output is one MP3 per slide, `slide_01.mp3` and so on. Join them with `ffmpeg` if wanted. `ffmpeg` was not tested.

Tested here: the dry-run and cost-guard paths, and a real two-slide HD run through `oticgroup-8430-resource` that produced valid MP3s (1,013 characters, 67.7 seconds of audio).

## 8. Voices

| Voice | Tier | Notes |
|---|---|---|
| `en-GB-Ada:DragonHDLatestNeural` | HD | Female. Recommended for the best naturalness. |
| `en-GB-Ollie:DragonHDLatestNeural` | HD | Male. |
| `en-GB-Ryan:DragonHDLatestNeural`, `en-GB-Sonia:DragonHDLatestNeural` | HD | Also available. |
| `en-GB-SoniaNeural` | Neural | Female. The standard cheap choice. |
| `en-GB-RyanNeural` | Neural | Male. |
| `en-KE-AsiliaNeural`, `en-KE-ChilembaNeural` | Neural | Kenyan English, female and male. |
| `en-TZ-ImaniNeural`, `en-TZ-ElimuNeural` | Neural | Tanzanian English, female and male. |

There are only two voices per East African locale, and none of them are HD. HD voices were found for en-GB. If the audience should hear an East African accent, that forces the cheaper Neural tier. 37 English voices on the `pdip-dev-speech` resource also support speaking styles (cheerful, calm and similar), which were not tested.

## 9. Cost

All prices are **USD, list price, per 1 million characters**, from the public Azure Retail Prices API (`https://prices.azure.com/api/retail/prices`, service `Foundry Tools`, product `Azure Speech`), read on 2026-10-19. The Azure pricing web page renders `$-` for every price when fetched without JavaScript, so use the API. Azure bills by **characters sent**, not by audio length.

### Measured speaking rate

I synthesised the same 552-character slide in three voices and computed duration from the file size (constant 96 kbps MP3). A guess was not used.

| Voice | Audio length | Characters per minute |
|---|---:|---:|
| `en-GB-SoniaNeural` (Neural) | 34.2 s | 969 |
| `en-KE-AsiliaNeural` (Neural) | 36.7 s | 902 |
| `en-GB-Ada:DragonHDLatestNeural` (HD) | 37.6 s | 881 |

Planning figure: **900 characters per minute**. Narration at 9 to 10 minutes for unit 1.1 (8,439 characters) matches this.

### Cost per minute of audio

| Option | Price per 1M chars | Per minute of audio | Whole unit 1.1 (8,439 chars, about 9.4 min) |
|---|---:|---:|---:|
| Azure **Neural** (e.g. Sonia, Asilia) | $15.00 | **about $0.0135 to $0.0145** (1.4 cents) | $0.127 |
| Azure **Neural HD** (Dragon HD) | $22.00 | **about $0.0194 to $0.0198** (1.9 to 2.0 cents) | $0.186 |
| Azure Neural, **Long Audio API** (avoid) | $100.00 | about $0.09 | $0.844 |
| Windows built-in voices (SAPI) | $0 | $0 | $0 |

### Cost at scale (900 chars per minute, list price)

| Audio produced | Neural | Neural HD |
|---|---:|---:|
| 10 minutes (one unit) | $0.14 | $0.20 |
| 60 minutes | $0.81 | $1.19 |
| 1,000 minutes | $13.50 | $19.80 |

HD costs about 47% more than Neural, which is roughly half a US cent more per minute. That gap is small compared to the time spent re-recording a poor-sounding narration.

### Free tier

Microsoft's pricing page lists **Neural: 0.5 million characters free per month** on the free (F0) Speech tier. That is about **9 hours of audio per month**. The page does not say HD is included, so assume HD is billed. None of the existing Otic resources is an F0 resource (all are S0), so no free allowance applies to them today. See section 11 before creating an F0 resource.

### Natural versus non-natural voices

| Option | Naturalness (expectation, not tested by ear) | Cost per minute | Available to us? | Notes |
|---|---|---:|---|---|
| Azure Neural HD (Dragon HD) | Highest. Microsoft's top tier, context-aware. | ~$0.019 | Yes, 125 HD voices in eastus2 | en-GB only for the voices we want. Not covered by the free tier. |
| Azure Neural | High. Consistent and clear, less expressive than HD. | ~$0.014 | Yes | Only tier with East African accents. Free tier covers 0.5M chars per month on F0. |
| Azure Standard (the old non-neural voices) | Robotic. | n/a | **No** | **Retired 31 August 2024.** Resources created after September 2021 never had them. |
| Windows SAPI (David, Zira, Mark) | Robotic, flat. | $0 | Yes, on the Otic Windows machine | en-US only, offline, no East African accent. |

Slide 1 (552 characters) took 38.0 s with the Windows voice "Microsoft David Desktop", versus 34 to 38 s for Azure voices. Speed is comparable. Quality is the difference. Naturalness ratings above are Microsoft's positioning plus general experience. **Listen to a sample before committing a whole course to a voice.**

Free robotic fallback (PowerShell, tested):

```powershell
Add-Type -AssemblyName System.Speech
$s = New-Object System.Speech.Synthesis.SpeechSynthesizer
$s.SelectVoice("Microsoft Zira Desktop")
$s.SetOutputToWaveFile("$PWD\out.wav")
$s.Speak((Get-Content narration.txt -Raw -Encoding UTF8))
$s.Dispose()
```

Use it for draft timing only, not for learners.

### Cost caveats

- The pricing page does not say whether SSML tags count as billable characters. The script sends only the text plus a `<voice>` wrapper. Budget conservatively as if the wrapper counts (about 100 extra characters per block, under a cent in total).
- Prices are list prices. Credits, if any exist, are not visible from the CLI.
- Region matters. East US and East US 2 matched. Other regions were not checked.

## 10. Limits, errors, and gotchas

| Symptom | Cause and fix |
|---|---|
| HTTP 401 on `https://<region>.tts.speech.microsoft.com` with a bearer token | Expected. Use the custom-domain endpoint for tokens, or use a key against the regional endpoint. |
| HTTP 401 or 403 on the custom-domain endpoint | The identity lacks data-plane access. It worked for the Otic Group identity (Owner plus Foundry User at subscription scope), but the exact role that grants it was not isolated. Ask a human to assign **Cognitive Services Speech User** on the resource. |
| HTTP 429 | Throttled. The script retries with backoff (1, 2, 4, 8 seconds). Reduce parallelism. Concurrency limits were not measured. |
| `MissingSubscription` from `az` | Git Bash rewrote a `/subscriptions/...` argument. Use `MSYS_NO_PATHCONV=1`. |
| `UnicodeEncodeError` in Python | Set `PYTHONIOENCODING=utf-8`. |
| `invalid_grant` or `AADSTS70016` during login | Stale or reused device code. Section 4. |
| Audio reads out stage directions | The whole file was sent. Send only `NARRATION:` blocks. |
| Quality surprise | Voices were not auditioned by ear. Test two slides first. |

The longest narration block in unit 1.1 is 735 characters, which is far below any single-request limit. Keep blocks per slide or per paragraph.

## 11. If you believe a new resource is required

Do not create one. These are the reasons:

- `AZURE_DEPLOYMENT_AND_ENVIRONMENTS.md` records that a sponsored subscription refused to create a new App Service Plan (`ServerFarmCreationNotAllowed`) and a new Azure OpenAI resource (`SpecialFeatureOrQuotaIdRequired`) in any region. Whether a new Speech resource is allowed was **not tested**.
- Only one F0 Speech resource is allowed per subscription per region, and creating one in a shared group affects other projects.
- Three working resources already exist.

If a human explicitly approves, the untested command shape is below. It must use an **existing** resource group and a name that is unique across Azure. Record the creation in the PR or the chat, and tell the human the delete command so they can remove it.

```bash
az cognitiveservices account create \
  --name <globally-unique-name> --resource-group rg-OticGroup-8430 \
  --kind SpeechServices --sku F0 --location eastus2 \
  --custom-domain <globally-unique-name> --yes
```

## 12. What was verified, and what was not

**Verified on 2026-10-19:**

- `az` login and subscription list (four subscriptions, live offer types and roles).
- Resource groups in the three usable subscriptions.
- Voice lists from `pdip-dev-speech` (key) and `oticgroup-8430-resource` (Entra token).
- Real synthesis: Neural, HD, and Kenyan voices on `pdip-dev-speech` with a key; HD on `oticgroup-8430-resource` keyless; keyless token on both Otic Group AIServices accounts and on `pdip-dev-speech`.
- Speaking-rate measurement, prices (East US and East US 2), the script's cost guard, and Windows SAPI.
- Standard voice retirement and the 0.5M free-character figure, from Microsoft's migration notice and a pricing snapshot.

**Not verified:**

- Anyone listening to the audio. Quality comments are expectations.
- Creating an F0 resource, and whether HD is free on F0.
- Whether SSML tags are billed.
- Credit balances. The CLI cannot show them.
- Concurrency and throughput limits, WAV output, speaking styles, and `ffmpeg` joining.
- Whether `Cognitive Services Speech User` is the exact role needed for a different identity.

When any of this becomes relevant, test it with a one-slide run and update this document.
