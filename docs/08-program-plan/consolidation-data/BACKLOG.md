# Backlog found during consolidation (not part of the merge)

- TikTok inbound messages: confirmed missing in the v2 social channels work (outbound sender exists).
- Social provider proof, media and delivery work, review and Autopilot dispatch, external claim and document sync: assess against the merged `main`.
- Worker/API version skew: the live worker is revision `1872a14` (2026-09-14), the live API is `4818bb1`; phase B3 records compatibility, a redeploy is a separate release step.
- Academy media behaviour that was live before `76733b6` was committed: verify the authenticated media endpoints on the merged branch.
- Decide whether the 69 MB of synthetic fixtures and reference images stay in `main` or move to storage.
- Remove the reachable injected blobs from history only if the owner wants a history rewrite; it needs a force-push and is not authorised.
- Academy insurer-sponsored one-time access codes: the live Academy already provides learner sign-in, enrollment, course progress, human marking and certificates; item 33's parallel `/learn` learner store was omitted. Add sponsor codes to the existing Academy store and enrollment flow before offering insurer-sponsored access.