# IIP Academy: build notes

Built from `docs/iip-training-courses-brief.md`. Courses live in the portal at `/app/academy`, listed in the sidebar under Inbox.

## What was built

- Catalogue page with four course cards (claims first, because the bancassurance course is the priority), the "how they fit together" line and a certificate download for any completed course.
- A stepped course player at `/app/academy/{course}`: one page per module (goal, outcomes, the idea as a flow, a real IIP screen with numbered callouts, a worked example, an exercise, takeaways), then a check with questions, then the certificate.
- Four courses, five modules each, content in `web/portal/lib/academy/courses.ts`.
- A certificate of completion as a PDF generated in the browser (`web/portal/lib/academy/certificate.ts`), with the IIP mark, the Otic logo, the person's name, the course, the date and a certificate number.
- Completion is remembered in the browser against the signed-in user (`web/portal/lib/academy/progress.ts`) and shown on the catalogue.

## Screens

Pictures in `web/portal/public/academy/` were captured from the running portal with the demo (synthetic) data: Claims list and detail, claim document reading, Underwriting, Fraud, Audit, Insights, Settings, Scores, Outreach and a prospect record. Where IIP has no such screen the module shows a labelled "Screen not yet available in IIP" box. Those are:

- Insurer comparison view (claims course, module 5): proposed, not built.
- Identity or national ID check result (fraud course, module 2): not built.
- Referral queue for loan-linked cover (underwriting course, module 4): not built.

The underwriting course uses the prospect score in Acquisition to teach reading a score and its reasons. IIP does not yet have a loan-linked application intake form or a credit risk score, so the course says so and uses what exists.

## Choices made without asking

- A certificate is issued after the steps and the check questions. The brief's exercise-based criteria (for example "correctly order four of five claims") are not enforced in the product, because there is no exercise-marking feature yet.
- The certificate says "certificate of completion" and states it is not an accredited qualification.
- Completion is stored in the browser, not on the server, so it does not follow a person to another device. A server-side record on the person's profile is a follow-up.
- No named bank, person or customer appears in any course page.

## Open questions

- Legal and regulatory obligations (Uganda's data protection law, the IRA's expectations, Bank of Uganda requirements) are shown as "to be confirmed with legal and compliance". Nothing claims IIP is certified or compliant.
- Whether and how the bank and its insurers have access to NIRA identity verification is not confirmed.
- The IRA service-standard figures in the claims course come from the brief and should be checked against the current IRA publication before external use.
- Course durations are proposed values, not delivered figures.
