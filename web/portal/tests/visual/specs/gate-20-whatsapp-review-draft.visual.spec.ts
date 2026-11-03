import { expect, test } from "@playwright/test";
import { installVisualApiFixtures } from "../fixtures/iip-fixtures";

const threadId = "review-draft-thread";
const draft = "Your claim documents have been received. A claims officer will review them and follow up.";

test.beforeEach(async ({ page }) => {
  await installVisualApiFixtures(page);
  await page.route("**/api/v1/channels/overview", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify([{
        id: threadId,
        customerName: "Visual Test Customer",
        channel: "whatsapp",
        detectedIntent: "claim_status",
        suggestedRouting: "Customer conversation",
        aiConfidence: 0.8,
        status: "AwaitingApproval",
        assignedTo: null,
        lastMessage: "Have you received my documents?",
        lastActivityAt: "2026-11-02T09:00:00+03:00",
        customerContact: "+256700000000",
        customerLanguage: "English",
        languageNote: null,
        controlMode: "Person"
      }])
    });
  });
  await page.route("**/api/v1/channels/conversations/whatsapp/review-draft-thread", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        channel: "whatsapp",
        threadId,
        recipientId: "256700000000",
        canReplyInInbox: true,
        pendingReplyDraft: draft,
        messages: [{
          messageId: "inbound-review-draft",
          direction: "Inbound",
          body: "Have you received my documents?",
          contentType: "Text",
          deliveryState: "Received",
          createdAt: "2026-11-02T09:00:00+03:00",
          caseId: null,
          sentByUserId: null,
          isPreview: false
        }]
      })
    });
  });
  await page.route("**/api/v1/channels/whatsapp/threads/review-draft-thread/prospect", async (route) => {
    await route.fulfill({ status: 404, contentType: "application/json", body: "{}" });
  });
});

test("WhatsApp review draft is clear, usable, and never sent by the draft action", async ({ page }, testInfo) => {
  let sends = 0;
  await page.route("**/api/v1/channels/whatsapp/threads/review-draft-thread/messages", async (route) => {
    sends += 1;
    await route.fulfill({ status: 202, contentType: "application/json", body: JSON.stringify({ deliveryState: "Queued" }) });
  });

  await page.goto(`/app/channels?thread=${threadId}`);

  const reviewDraft = page.getByRole("region", { name: "Assistant reply draft for review" });
  await expect(reviewDraft).toBeVisible();
  await expect(reviewDraft).toContainText("Review before sending");
  await expect(reviewDraft).toContainText(draft);
  await reviewDraft.getByRole("button", { name: "Use draft" }).click();
  await expect(page.getByRole("textbox", { name: "Message to the customer" })).toHaveValue(draft);
  expect(sends).toBe(0);
  expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(1);
  await page.screenshot({ path: testInfo.outputPath("whatsapp-review-draft.png"), fullPage: true });
});
