# How to test

Plain steps for each finished item are added here as items complete.

## Item 1: Mock providers
1. In a terminal at the repo root run `scripts/run-local-mock.sh` and wait until it prints "API is up". Open http://localhost:3000.
2. Open Claims. In the right-hand panel "Provider health", every provider should say Mock (or Not configured for Teams notifications). Nothing should say Operational.
3. Open any Motor claim, upload the file `web/portal/tests/fixtures/claims-core/police_letter.jpg`. It should be recognised as a police abstract with a report number `POL/KLA/2026/00471`.
4. Press Generate summary on the claim. The text starts with "MOCK AI OUTPUT, not produced by a real model."
5. Automatic check: `cd web/portal && pnpm test:claims-core` (builds nothing; run `dotnet build InsuranceIntelligence.sln -c Release` first).

## Item 2: Known defects
1. Run `scripts/run-local-mock.sh`. Use the claims-core fixtures folder `web/portal/tests/fixtures/claims-core/`.
2. Create a Motor claim (Claims, then New claim). Open it and upload a text file named `accident_report.txt` with any content. Its type must show as Unknown, not Customer ID.
3. As a customer: open Claim access, enter the claim's policy number and email. With the mock providers there is no email: open `http://localhost:5167/api/v1/dev/mock-outbox` is not browsable without headers, so instead use the sandbox helper in the e2e spec, or read the link from the API log. (The e2e test `Item 2` does this automatically.)
4. In the customer page upload a text file containing `IIP-MOCK-DOC:PoliceAbstract IIP-MOCK-FAIL-ONCE` into "Police report". It says "Could not be read" with a Try again button. Press it. It says "Received" and completeness is 33%.
5. In the staff app, a document that failed shows the action "Retry processing".
6. Sign in preview as Customer Service: Admin, Products and Onboarding must not appear.

## Item 3: Claim types
1. Run `scripts/run-local-mock.sh`, open http://localhost:3000, go to Admin, Products, open "Marine" (create one with New product if there is none).
2. In "Claim types" press Add claim type twice, name them "Cargo loss" and "Hull damage", move "Hull damage" up, press Save changes and enter a reason. Reload: the order is kept.
3. Open the seeded Motor Cover product: it already lists Accident, Theft, Fire, Windscreen damage, Third party liability.
4. Claims, New claim, choose a Motor policy: the "Claim type" box lists exactly those types. A made-up type is refused by the API with the allowed names.
5. Use "Import from existing claims" on a product with older free-text claims to turn them into types.

## Item 4: Documents and evidence per claim type
1. Run `scripts/run-local-mock.sh`; Admin, Products, open a product that has claim types (add some first, see item 3).
2. In "Document matrix" choose a claim type in "Show what a claim of this type is asked for". If it says nothing is set up, press Add document; the new document is for that claim type only. Pick "Photos of the damage" and set the kind to Evidence, file types "jpg, png", most files 6. Save with a reason.
3. Switch the filter to another claim type: that document is not listed for it.
4. As a customer (claim link) open a claim of the first type: the photos slot says "These photos are kept exactly as you take them." Add three photos: it says "3 of 6 sent"; you cannot add more than 6 and a PDF is refused with the allowed file types.
5. A paperwork slot holds one file; a second upload replaces it.

## Item 5: Claim questions per claim type
1. Run `scripts/run-local-mock.sh`; Admin, Products, open a product; find "Claim questions".
2. Add a question "Police report number" (short text, required), another "Damage level" (choose from a list, choices "Minor, Major, Total loss"), move it up, save with a reason.
3. Claims, New claim: step 4 shows these questions instead of the built-in vehicle form (for a product that has configured questions). Leaving the required one empty is refused with its name.
4. Open any claim: "Claim details" lists every question with the answer, "Needed, not given yet" for a missing required one. Press Edit details, change an answer, Save details.
5. A staff-only question never appears on the customer's page.

## Item 6: Operating settings
1. Run `scripts/run-local-mock.sh`; Admin, then "Operating settings".
2. Change "Remind the customer about missing documents after" to 7 and press Save settings; reload and see 7. Each field says what it usually is.
3. Enter 99 for the reminder days: it is refused with the allowed range and nothing changes.
4. Untick the IRA rule, create a product with no IRA reference and submit it for approval: it goes through (a second person still has to approve). Tick it again and the same submit is refused with the reason.

## Item 7: Set up from the policy wording
1. Run `scripts/run-local-mock.sh`; Knowledge, upload `web/portal/tests/fixtures/claims-core/sample_policy_wording.txt` as a Policy wording (document type PolicyWording).
2. Admin, Products, open a product (or create a Motor one); find "Set up from the policy wording", pick the wording, press "Read the wording".
3. Review the proposals: each shows the quoted clause and "Found in the wording: clause 1.2, page 1". Accept "Accident", "Police report" and "Fire", reject "Windscreen". The "Log book" document cannot be accepted until a document type is chosen.
4. Press "Add 3 accepted items to the product". The result lists what was added and anything left out with the reason. The product becomes a new draft version; "What the wording covers" appears on the page. Publishing still needs a second person.
5. In local runs the proposals come from a deterministic stand-in model (marked as such), not a real one.

## Item 8: Preview as customer
1. Run `scripts/run-local-mock.sh`; Admin, Products, open a product that has claim types, and press "Preview as customer" at the top right.
2. Step 1 shows the form a customer sees; change the claim type and the questions change. Submit is disabled and the page says nothing is saved.
3. Step 2 shows the documents for that claim type (photos say they are kept exactly as taken); upload buttons are locked.
4. Press Phone and Wide to compare widths. A draft product shows "Draft, customers do not see this yet".

## Item 9: Privacy notice and consent
1. Run `scripts/run-local-mock.sh`; in Claims, open a claim and send the customer link (or use a claim from the mock outbox) and open it.
2. On the customer's claim page the privacy notice shows first, and upload buttons are greyed with "Read and agree to the privacy notice above". Press "I agree"; the card shrinks to "You agreed to our privacy notice on ..." and uploads work.
3. Press "Withdraw permission", confirm: uploads are blocked again; "I agree" restores them.
4. As admin, Admin, Operating settings, "Privacy notice": change the words and save. The customer is asked to agree again on their next visit. "Use the default text" goes back to the default (a draft: have legal confirm it).
5. On the staff claim page a "Privacy notice" line shows Agreed, Not yet agreed, Withdrawn or Notice changed.

## Item 10: Photo or file per slot
1. Open the customer claim page on a phone (or Chrome device mode at 390px) after agreeing to the privacy notice.
2. Each document row shows "Take photo" and "Choose file". On a phone "Take photo" opens the camera; on a computer it opens the file chooser.
3. The "Photos of the damage" row accepts several: take a photo, then "Add more files"; the count updates.
4. In Admin, Products, set a document to accept PDF only and open "Preview as customer": that row has no "Take photo".

## Item 11: Evidence originals
1. Customer: upload a phone photo (JPEG with location, or HEIC) into "Photos of the damage".
2. Staff: open the claim's documents; the line "Evidence, kept exactly as sent. SHA-256 ..." shows the hash. Compare it with `sha256sum` of the file you uploaded: they match. The viewer shows a picture even for a HEIC original.
3. Download the original from the content route: it is identical to the file you uploaded, EXIF included. The preview picture has no EXIF.
4. Upload a large photo of a paper letter in a document slot on a phone: it is scaled down before sending; the same photo in the evidence slot is not.

## Item 12: Document photos to PDF
1. Admin: make a document (for example Police report) allow several pages (Products, document matrix: "Several pages").
2. Customer page: in that row press "Choose file" and pick a photo of a page, then "Add another page" and add a second one (or "Take photo" on a phone).
3. Use Up and Down to change the order and Remove to take a page out. The line under the list says how many pages the PDF has.
4. Staff: open the claim's document; "Page 1 of 2 in one PDF ... Open the PDF" opens the PDF in the order the customer chose; the pictures themselves are still downloadable exactly as sent.

## Item 13: Camera document scanner
1. On a phone open the customer claim page, agree to the notice, and find a paperwork row (for example Police abstract). Press "Scan document" and allow the camera.
2. Lay a page on a table, press Capture. Drag any corner onto the page edge if needed, press "Looks right".
3. Pick Clean, Grey or Black and white, Turn if the page is sideways, then "Use this page". It appears as the next page; staff get it in the one PDF.
4. Refuse the camera permission or use a desktop without a camera: the scanner closes with an explanation and the normal camera/file chooser opens.

## Item 14: Recognise and rename every file
1. Customer page: send a photo of a police letter into the "Police abstract" row. As staff open the claim's documents: it is shown as `PoliceAbstract_CLM-XXXXXXXX.jpg`, "(sent as police_letter.jpg)".
2. As staff upload a file in Documents with no slot whose text says it is a police report: it is filed into the Police abstract slot automatically and the customer's page shows it received.
3. Upload something unreadable: it is marked "Needs a person" with the reason; choose "Put it in" a document and press "Put it there"; it leaves review and the customer's page shows that document received.
4. Send a claim form into the police report slot: the customer sees it received; staff see a warning that it reads as a claim form.
5. Admin, Document types: add "Vehicle log book"; files that read as it are recognised and filed too.

## Item 15: Extracted data into the claim
1. Admin sets the reading rules once (API): POST `/api/v1/document-types/PoliceAbstract` with `extractionFields` such as `[{"extractedKey":"ReportNumber","claimFieldKey":"policeReportNumber"}]` and an `X-Audit-Reason` header. There is no screen for this yet.
2. Upload a police report to a claim whose product has a "Police report number" question.
3. Open the claim: "Suggested from the documents" lists the value with its document, page and confidence. The question itself is still empty.
4. Type a correction in the box and press Accept: the claim shows the answer with "Read from a document ... confirmed by <you>". Press Reject on another suggestion: it stays rejected, even if the document is processed again.
5. Put a different answer on the question by hand, upload another document: the new suggestion says the claim already has an answer, and the button reads Replace.

## Item 16: Picture checks
1. Customer sends a photo into "Photos of the damage" (a phone photo with its details is best).
2. Staff open the claim: "Picture checks" lists the photo. Each finding has a severity word and a reason. A photo dated before the incident, or saved by editing software, says so.
3. Send the same photo for a second claim: its card says it looks like one sent for another claim, naming that claim's reference.
4. Choose "Needs follow-up" with no note: refused with a plain message. Add a note and save: the card shows your reading, who gave it and your note. Process the document again: the reading stays.

## Item 17: Background pipeline
1. `scripts/run-local-mock.sh` runs the API with the background worker on. Customer: send a document that fails once (a text file containing `IIP-MOCK-DOC:PoliceAbstract IIP-MOCK-FAIL-ONCE`). The row says "Being checked" for a couple of seconds and then "Received" by itself.
2. Send one containing `IIP-MOCK-FAIL-ALWAYS`: after three tries it says "Could not be read" with "Try again".
3. Admin, "Background work": choose "Gave up". The job is listed with its claim reference, "attempt 3 of 3" and a plain reason. Press "Send back"; it moves to waiting.
4. Stop the API while a job is waiting, start it again: the job still runs.

## Item 18: Review score and recommendation
1. `scripts/run-local-mock.sh`. As a supervisor (the default local admin works), open a claim. Under the summary there is "Review score and recommendation" with two boxes: "Prepared for you" and "Your response".
2. A brand new claim shows 0 out of 100, Low, "Nothing has been flagged on this claim.", and the advice "Ask the customer for what is missing before deciding." with a line per missing document.
3. Have the customer send a photo with a camera date from last month (any photo whose date is long before the incident): a Strong picture-check reason appears, the score rises, the advice becomes "Look at the flagged items first". Open "Picture checks" below, mark it No concern: the score returns to 0 and the reason moves to "set aside". Mark it Concern raised with a note: the advice becomes "Consider referring this claim to the investigations route".
4. Create two claims with the same email: each shows a Moderate "Fixed rule" reason. On the Fraud page triage the signal: the claim page now says "Being worked" and the advice says to wait for it.
5. Press "Prepare the explanation": a short paragraph appears marked as test output. Choose "I disagree" and press Record response with no note: it asks why. Add a note and record: it shows who disagreed and why. Add another photo: the earlier response no longer shows.
6. Sign in as a claims officer: the box is not on the claim page, and the endpoint answers 403.

## Item 19: Claimant status wording and timeline
1. `scripts/run-local-mock.sh`. Admin, "Claim status wording": ten statuses, each with the staff name, the default title and explanation. Change "Received" to "Thank you, we have your claim" and save.
2. Open a customer link (see Item 9 for how). At phone width the claim page opens on that title with its explanation, and below "What happens next" is a list of steps: the current one says Now, finished ones say Done.
3. As staff, assign the claim to an officer: the customer page changes to "Someone is looking after your claim" and the second step becomes the current one.
4. On Kato (WhatsApp mock), ask for the claim status: the reply uses the same words.
5. Back on the wording page press "Use the default" and save: the customer page returns to the default words.

## Item 20: Change status
1. `scripts/run-local-mock.sh`. Open a claim as a claims officer. In the right-hand column, "Change status" says "Now: Received".
2. The list offers only Under review and Waiting on documents. Open "How the other statuses are reached": Assigned says to use Assign, Awaiting approval says it is decided in the Approvals queue, Settled and Closed say to use Resolve, Escalated says to use Escalate.
3. Choose Under review, leave the reason empty: the button stays off. Type a reason and press the button: "The claim is now under review." and the Activity timeline records who and why.
4. Open the customer link: the title is "We are reviewing your claim" and "Assessment" is the current step with its date.
5. Escalate the claim, then try as a claims officer to move it back: the option is listed as needing a supervisor and is off. Sign in as a supervisor and it works.

## Item 21: Customer notifications on status change
1. `scripts/run-local-mock.sh`. Admin, "Claim status wording": the top box "Telling customers when the status changes". Tick "Tell customers when their claim's status changes" and "Send them without a person approving each one"; type an approved template name against "Under review"; save.
2. Move a claim to Under review (Change status). Read `GET /api/v1/dev/mock-outbox`: a text message to the claim's phone saying "Hello <name>. An update on your claim CLM-...: We are reviewing your claim. ..." is there. Nothing left the machine.
3. Move the claim to Ready for review: nothing goes out; the claim has a customer update waiting for approval. Approve and send it from the updates queue.
4. Escalate the claim and then Resolve it as Settled: the "settled" message is waiting for a person, even with automatic sending on.
5. For a claim that came in on WhatsApp, without the customer's recorded consent the update is kept with "ConsentRequired"; with consent and a customer message from the last 24 hours it is sent as plain text; older than that it is sent as the template (the outbox line starts with "[template ...]"), or kept as "TemplateRequired" if none is set.

## Item 22: Send claim link
1. `scripts/run-local-mock.sh`. Open a claim as a claims officer. In the right-hand column, "Send claim link".
2. Press "Send claim link" with the default channel: "Link sent by text message to +256*******11. It works once and for 24 hours." (the address is half hidden and there is no link on screen).
3. Read `GET /api/v1/dev/mock-outbox`: the text message to the customer's phone has the link. Open it in a phone sized window: the customer lands on their claim. Open it again: it says the link is invalid or has expired.
4. Choose "WhatsApp" for a customer with no recorded WhatsApp consent: "There is no recorded consent from the customer to message them on WhatsApp."
5. The Audit log has a `claim.link.sent` event for each try with the channel and outcome.

## Item 23: Slow connections
1. `scripts/run-local-mock.sh`. Open a customer link on a phone sized window and agree to the notice.
2. In the browser's developer tools set the network to "Slow 3G" (or "Fast 3G"). Under "Photos of the damage" choose a photo of a few megabytes. A bar and "Sending IMG_...: 12%" appear and move.
3. Switch the network to "Offline" while it sends: the line changes to "You are offline. Sending carries on as soon as you are back online." Switch it back: the bar moves again by itself and the row ends at "1 of 10 sent".
4. Start a send, then reload the page half way: the row says "Sending IMG_... was interrupted. Choose that same file again and it carries on from where it stopped." Choose the same file: the bar starts part way along.
5. Staff can open the claim: the stored photo is exactly the one chosen (its SHA-256 on the evidence list matches).
6. Under "Police abstract" take a large photo of a page: it is sent as a page of about 450 KB, still readable.

## Item 24: Claims pack
1. `scripts/run-local-mock.sh`. Have a customer send a police abstract and a photo. As a supervisor open the claim: the "Claims pack" box in the right-hand column.
2. Press "Download claims pack". A file `ClaimsPack_CLM-XXXXXXXX.zip` is saved and the box says how many files and how big.
3. Open it: `00_ClaimSummary_CLM-....pdf`, `01_Documents/PoliceAbstract_CLM-....pdf`, `02_Evidence/PhotosOfTheDamage_CLM-....jpg`, `manifest.json`.
4. Open the summary PDF. Read it top to bottom: the claim, the customer, the policy, the details given, the documents asked for (Received or Missing), the files with their fingerprints, what happened in order, then the parts the platform prepared, each starting with "ADVISORY: prepared by the platform to help a person. It does not decide, approve, reject or settle anything."
5. Check the evidence: the photo in `02_Evidence` has the same SHA-256 as the manifest says (and as the claim's evidence list). Sign in as a claims officer: the box is not shown and the API answers 403.

## Item 25: Pack emailed automatically
1. `scripts/run-local-mock.sh`. Admin, "Claims pack email": type two addresses, choose "When a claim's documents are complete", Save.
2. Open a claim as a supervisor: "Claims pack" has "Send pack now". Press it: "Sent to 2 addresses with the pack attached." The history line shows the addresses half hidden. `GET /api/v1/dev/mock-outbox` shows the two emails with subject "Claims pack CLM-..." and a zip attached.
3. Have a customer send a photo of 4 MB or more, press "Send pack now" again: "as a secure link". The email holds a link ending `/shared/claims-pack?token=...`. Open it in a phone sized window with no sign in: a page with "Download the pack"; press it and the zip downloads. After 10 downloads, or a week, the page says the link has expired.
4. Have a customer complete every required document (Claim form, Police report, Photos): within seconds Admin, "Background work" shows "Emailing a claims pack" done, the outbox has the emails, and the claim's history says "the documents for the claim are complete". Send another document: nothing more is sent.
5. Change the trigger to "When a claim reaches a status" and choose Ready for review; move a claim to Under review (nothing) and then to Ready for review (the pack goes).

## Item 26: Customer memory
1. `scripts/run-local-mock.sh`. Open a customer link and, under "Your ID", send any picture. Wait for "Received".
2. Create another claim for the same customer (same phone number) and open its link: a card "What we already have" shows "Your ID, national_id.jpg, from claim CLM-...". Nothing is stored on the new claim yet.
3. Press "Use the one on file": the "Your ID" row shows Received. As staff, the document list shows it was reused from the earlier claim.
4. On the first claim answer "Driving permit number"; on the second claim the card also offers "Driving permit number: ... from claim CLM-...". Tick it and press "Use these details": the answer appears in the claim's questions.
5. Start a new claim from a "start a claim" link for the same policy: the form already has the remembered details and a line saying where from.
6. Admin, Operating settings, "Returning customers": untick the box and Save. A new customer link now shows no such card. Tick it again.
7. On WhatsApp, file a claim as a customer who has an ID on file: the reply says "we already have Your ID (from claim ...)" and points to "Use the one on file" on the page.

## Item 27: Multi-tenant WhatsApp
Real Meta delivery cannot be tried locally; the checks below use the API and the Mock providers.
1. `scripts/run-local-mock.sh`. Admin, Operating settings: under "Kato, the WhatsApp assistant" enter an organisation (for example Mavuno Bank) and short name and save. Reload: they are still there. Another workspace's settings are empty.
2. `GET /api/v1/integrations/whatsapp/health` with a workspace that has no number: `Not configured`.
3. `POST /api/v1/integrations/whatsapp/webhook` with a body naming an unknown `phone_number_id`: refused (401, or 503 when nothing at all is connected).
4. Product description for WhatsApp: `PUT /api/v1/products/{id}` with an `offer` object (insurer, summary, whoItIsFor, waitingPeriod, papersForAClaim, levels). The product returns to Draft for approval like any change; once published, Kato explains it on WhatsApp.
5. With real Meta credentials (see SUMMARY.md): connect two numbers in two workspaces, send "hello" to each, and check each answers as its own organisation and appears only in its own inbox; Admin, Background work shows the answering jobs.

## Item 28: Multi-tenant email
1. `scripts/run-local-mock.sh`. Admin, Email Mailboxes, "Add mailbox". Choose "Another mail host (IMAP)", host `imap.demo.test`, any username, password `wrong-password`: refused with "The mail host refused the username or password." Use any other password: the mailbox is added and shows "IMAP, imap.demo.test". The password is not shown anywhere.
2. Put mail in the pretend host: `POST /api/v1/dev/mock-mailbox/deliver` with `{"host":"imap.demo.test","username":"<your username>","from":"customer@example.test","subject":"Accident","body":"A taxi hit my car","attachments":[{"fileName":"police_report.pdf","contentType":"application/pdf","text":"police letter"}]}`.
3. `POST /api/v1/integrations/email/ingest` (or wait for the schedule): the message appears in the inbox. Run it again: nothing is duplicated.
4. Process the message into a claim; the attachment is on the claim, and collecting again finds nothing because the host has it marked read.
5. Switch to another workspace: its inbox and mailboxes are empty. A host name such as `localhost` or `10.0.0.5` is refused outside a developer runtime.
6. With real credentials (see SUMMARY.md): an IMAP mailbox on a real host, and a Microsoft 365 mailbox in a second Entra directory.

## Item 29: Multi-tenant claimant portal and channels
1. `scripts/run-local-mock.sh`. Admin, Operating settings, "Customer portal": enter a link name (say `acme-insurance`), a name, a button colour such as `#1F5EFF`, a phone and email. Save. "A customer link looks like" shows `...&t=acme-insurance`.
2. Open the customer link from the mock outbox on a phone-sized window (390 px wide): the header shows the insurer's name, the buttons use its colour, and the bottom shows "Need help? Call ... or write to ...". Open the same link with `t=nobody-here`: "This claim link is not valid."
3. Try a colour that is too light (`#FFCC00`): refused. Try `http://` for the logo: refused.
4. Enter your own web address (for example `claims.example.com`): the card shows the text to publish and where. Pressing "Verify the address" before it is published says why it failed. Customers keep being sent to the shared portal until it passes.
5. Another workspace cannot take your link name or address (409).
6. "Make a callback address" (needs the platform's public address configured): shown once; a delivery report sent to it updates only your messages; making a new one stops the old.

## Item 30: Self-serve sign-up and non-Entra sign-in
1. `scripts/run-local-mock.sh`, open `/signup`. Enter a company name, your name, an email and a password of 12 or more characters; tick the terms box (the link opens the draft terms, marked "to be confirmed by legal"). "Create workspace" says to check your email.
2. Try signing in now: refused with the same plain sentence a wrong password gets. Nothing exists yet.
3. Open the Mock outbox (`/api/v1/dev/mock-outbox` with the platform scope `iip-platform` / `identity`, or the Admin mock outbox screen) and open the link: "Your workspace is ready". Sign in on `/signin` under "Sign in with email": you land in the workspace as Administrator. Products shows Motor, Medical and Property as drafts.
4. Admin, Users and roles, "Invite a colleague": enter an email and a role, "Send invitation". It appears in the table as Pending. Open the link from the outbox in a private window: choose a name and a password, "Join the workspace". Open the same link again: "This invitation cannot be used". Withdraw an invitation: its link stops working.
5. `/forgot-password`: enter your email, open the emailed link, try "short" (refused with the reason), then a long password; sign in with the new one. The old one no longer works.
6. Five wrong passwords: the account pauses for 15 minutes and the screen still gives the same sentence. A phone-width window (390 px) shows each of these pages without sideways scrolling.

## Item 31: Token metering
1. `scripts/run-local-mock.sh`. Open a claim and press "Summarise" (or use Policy intelligence). Then Admin, Billing: "AI calls this month" and "AI tokens this month" are above zero; "Estimated AI cost" shows US$ 0.00 because the mock model is free. Storage and documents show what the workspace holds.
2. Sign in to a second workspace (or create one at `/signup`): its Billing page shows zeros; the first workspace's calls do not appear there.
3. Set `INSURANCEINTELLIGENCE_AI_PRICING='{"your-deployment-name":{"promptPer1K":0.0025,"completionPer1K":0.01}}'` against a real Azure OpenAI deployment and make a few calls: the cost line fills in. Remove the setting: the cost card says no price is set for the AI model in use; tokens still count.
4. The assistant's activity list and a claim's AI history show only real actions, never a bare "Model call".
5. Upload a document through the customer portal: "Documents received this month" goes up by one and "Document storage" by its size (rounded down to whole MB, so small files show 0 MB until they add up).

## Item 32: Plans, limits and payments
1. `scripts/run-local-mock.sh` (it sets the pretend payment operator). Sign up a new company at `/signup`, confirm the email from the mock outbox, sign in. Admin, Billing shows "Free" with its allowances, usage against them, and the four plans in UGX (switch to USD with the buttons).
2. Choose "Agent", leave "MTN Mobile Money", enter `256700123456`, "Pay UGX 75,000". The page says to approve on the phone. Answer as the operator would: `POST /api/v1/dev/mock-payments/{reference}/settle` with `{"outcome":"succeeded"}` (list references at `GET /api/v1/dev/mock-payments`). Within a few seconds the page shows the Agent plan, a paid invoice, and "View" opens the receipt. The receipt email is in the mock outbox.
3. Try a number ending in `99`: it is refused at once and the page says there was not enough money; the plan stays Free and the invoice stays unpaid. Choosing "Card" sends you to a pretend card page where "Approve the payment" or "Decline" answers.
4. Open claims until the Free allowance (10 a month) is used: the Billing page shows the upgrade prompt and the next claim is refused with a payment-required message naming the plan. Another company is not affected.
5. `POST /api/v1/dev/billing/run` runs the renewal pass and reconciliation by hand. With a paid plan near its end it issues a renewal invoice and sends the reminder; run it past the end and the plan goes overdue, then after a week lapses to the Free limits. Paying the open invoice restores it. "Do not renew" on the plan card stops the renewal and the workspace moves to Free at the end of the paid month.
6. Set `INSURANCEINTELLIGENCE_PAYMENT_PROVIDER=Disabled`: the plan cards say payments are not configured and there is no pay button.

## Item 33: Academy for real learners
1. `scripts/run-local-mock.sh`. Open `/learn`: four courses with prices; switch UGX and USD. Enter an email and "Email me a sign-in link". Open the link from the mock outbox (`/api/v1/dev/mock-outbox` with scope `iip-academy` / `learners`): you land on "Your courses".
2. Admin, Academy access codes: pick a course, "Issue codes". Copy the code from the box (it is shown once; reloading the page never shows it again). The table shows "ending XXXX" and Unused.
3. As the learner, open the course, type a wrong code (a plain refusal), then the real one: the course opens at step 1. Use it again as another learner: refused.
4. Work through the steps ("Done, next step"). Send an answer in the box under a step: it appears in the insurer's "Written work to mark"; mark it Satisfactory or Try again with feedback, and the learner sees it on reload.
5. On the check questions pick wrong answers first: "Not quite", no answers revealed. Then the right ones: "Right" with the reason. "On to your certificate", enter a name, "Get my certificate": a number appears and the PDF downloads. Open `/learn/certificate/<number>` in a private window: name, course and date only.
6. Without a code: on a course page choose mobile money, enter `256700123456`, "Pay". Settle it as the operator would (`POST /api/v1/dev/mock-payments/{reference}/settle`): the course opens by itself. A number ending in `99` is declined.
7. A phone-width window (390 px) shows each learner page without sideways scrolling.

## Item 34: Retention, deletion and erasure
1. `scripts/run-local-mock.sh`. Create a claim for a test customer, and have that customer upload a document through the customer link (or use a seeded one). Admin, Data protection.
2. "How long records are kept": set "Claim documents" to 1 month and Save. "Delete expired records now" says nothing was past its period (the document is new). Set it back to 0.
3. "Legal holds": enter the claim's reference (CLM-XXXXXXXX from the claims list) and a reason; it appears in the table.
4. "Erase one person's data": enter the customer's phone (or email or policy number) and "See what would be erased". The preview lists the documents, messages and chats that would go and says the held claim will be left alone. Release the hold, preview again: the claim is now counted.
5. Enter a reason and type ERASE; "Erase now". The claim in Claims now shows "Erased" and no contact details; its documents are gone and the stored file is gone from the storage folder (`.local/document-storage/...`). "Evidence of deletion" shows the record with counts and no phone or email.
6. To see a time-based deletion, set a period, change the machine clock or the claim's date in a test database, and run "Delete expired records now"; the automated tests do this with a controllable clock.
7. `az bicep build --file infra/mainTemplate.bicep` builds. To use your own key, give `customerManagedKeyVaultUri`, `customerManagedKeyName`, `customerManagedKeyIdentityResourceId` (and `customerManagedKeyVersion` for SQL) when deploying; see the decision for the one manual step.

## Item 35: Scale
1. Nothing changes on screen. Create a few claims, then ask for a login link on one claim's customer page: it works as before. Run `POST /api/v1/cases/follow-through/run`: it works as before.
2. To see the speed difference: seed several thousand claims (the demo seeder or a script posting to `/api/v1/cases`), then time `POST /api/v1/client-portal/magic-links`; it no longer grows with the number of claims.
3. Old data: start the API against a database that has claims from before this release. They still appear in every lookup straight away; the log shows "Lookup backfill: filled the lookup columns on N existing row(s)" about 20 seconds after start.

## Item 36: Design direction
1. Open `/signin` and `/signup` at phone width and at desktop width: one blue accent, flat borders, nothing runs off the page.
2. On a phone-sized window with a clean browser profile, open `/app`: the sidebar is the narrow icon rail; tap Claims once and it opens.
3. Sign in as a role without admin rights (the role switcher in a development runtime) and open Billing, Academy and Data protection under Admin: each says you do not have permission.
4. Open a claim, choose More actions, then Export claim pack, then Cancel: focus is back on More actions.
5. Visual suite: `cd web/portal && npx playwright test -c tests/visual/playwright.config.ts` (about 17 minutes).

## Item 37: Final pass
1. Run everything in the order of "How it was verified" in `docs/build-loop/SUMMARY.md`.
2. Two-company journey: `cd web/portal && npx playwright test --config playwright.claims-core.config.ts -g "Item 37"` (about 1 minute), or do it by hand as step 11 of the walkthrough in SUMMARY.md.
