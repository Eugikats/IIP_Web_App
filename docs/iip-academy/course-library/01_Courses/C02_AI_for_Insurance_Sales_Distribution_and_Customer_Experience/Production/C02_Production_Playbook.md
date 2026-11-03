# C02 — Production playbook

Version 1 · 18 October 2026

## 1. Review the lesson

Use the outline and Unit 1.1 script as editorial drafts. Check the audience, outcomes, example and source map. Preserve the fictional labels. Product statements must be supported by the supplied training extract or the applicable approved insurer source. Use the shared artifact playbook for layout and brevity.

## 2. Build the slides

Create one slide per numbered heading. Put the on-screen text on the slide and the narration in speaker notes. Use the visual direction to draw a simple diagram; do not fabricate a platform screenshot. Use the project's established blue, yellow and orange palette, readable text and restrained layout. Label proposed workflows and fictional products clearly. Keep source references available in notes.

## 3. Generate narration

Open `C02_TTS_Narration_Generator.ipynb` in Google Colab. Configure Cloud Text-to-Speech using its instructions and your own restricted key stored in Colab Secrets as `GOOGLE_TTS_KEY`. Do not paste keys into scripts or files. Review the current provider pricing and account usage before running paid requests. The notebook does not enforce a spending cap.

Upload `C02_U1-1_Narration_Script.md`, confirm the parsed slide count and audition voices. Keep `UNIT_CODE` as `C02_U1-1`. Generate slide audio, review it, then assemble the full audio and timing outputs. Listen especially for “A-I”, delivery use and referral wording. Edit pronunciation in the script if needed and regenerate only affected slides. For a later unit, change the unit code and upload that unit's script.

Expected outputs after execution: per-slide MP3s, full-unit MP3, SRT captions, slide-timings CSV, usage log and a download ZIP. No outputs are pre-generated in this pack. The SRT timings are estimated by sentence length; review synchronisation manually.

## 4. Assemble and inspect the video

Insert the matching audio on each slide, start automatically and set transitions from the timings CSV. Export a full-HD video. Check beginning, middle and end for audio/slide alignment, then review the complete lesson. Make sure captions remain readable on a phone. Keep the editable script and slides with the final output so revisions are traceable.

## 5. Prepare assessment access

Import the learner quiz separately from the instructor key. Proposed scoring is one mark per question and a pass at eight. Disable the AI assistant for an active quiz attempt and prevent access through alternate assistant endpoints. Do not include the instructor key in learner files, public manifests or assistant retrieval. Verify these restrictions with a learner account; the files themselves cannot enforce them.

## 6. Publish through the actual Academy workflow

The editorial manifest is a content inventory, not a verified LMS import format. Match it to the Academy's actual course/unit identifiers and supported schema before importing. The portal's older sample-course catalogue uses different course identifiers; do not overwrite a course merely because its display number is two.

Confirm titles, ordering, learner permissions, playback, captions and quiz behaviour. Release only the unit that has been reviewed. The remaining eleven units are outlines. Keep them unavailable to learners until their scripts and assessments exist. Record the release version and date.

Completion check: source review; fourteen slides; reviewed audio; synchronised captions; ten quiz questions; restricted answers; enforced assessment assistant restrictions; correct course mapping; learner smoke check. This pack has not been imported or published to the live Academy.
