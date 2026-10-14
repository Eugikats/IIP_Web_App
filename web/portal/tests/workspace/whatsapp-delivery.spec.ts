import { expect, test, type Page } from "@playwright/test";

// Meta is not available to a test, so the three WhatsApp calls are answered with what the API sends. The facts match
// 12 October 2026: IIP's connection looked healthy while no message had arrived for days.

const CONNECTED = {
  connected: true, phoneNumberId: "1224011020803768", wabaId: "1008388325520579", businessDisplayName: "Test Insurer",
  configuredByUserId: "user-1", configuredAt: "2026-10-12T07:47:51Z", lastValidatedAt: "2026-10-12T07:47:55Z", lastValidationStatus: "Success",
  lastValidationError: null, webhookCallbackUrl: "https://api.example.test/api/v1/integrations/whatsapp/webhook",
  webhookRegistrationStatus: "Registered", webhookRegistrationError: null, channelProviderActive: true, requiresChannelProviderDeploymentChange: false,
};

async function openWhatsApp(page: Page, receipts: unknown) {
  await page.route("**/api/v1/integrations/whatsapp/configuration", (route) =>
    route.request().method() === "GET" ? route.fulfill({ json: CONNECTED }) : route.continue());
  await page.route("**/api/v1/integrations/whatsapp/receipts*", (route) => route.fulfill({ json: receipts }));
  await page.route("**/api/v1/integrations/whatsapp/configuration/meta-check", (route) =>
    route.fulfill({
      json: {
        checkedAt: new Date().toISOString(),
        findings: [
          { level: "ok", text: "The number is connected on Meta." },
          { level: "problem", text: "This number is set to send its messages to cashflow.example.com, not to IIP." },
          { level: "warning", text: "Meta only delivers real customer messages to an app that is published." },
        ],
      },
    }));

  await page.goto("/app");
  await page.getByRole("button", { name: "Settings", exact: true }).first().click();
  const dialog = page.getByRole("dialog", { name: "Settings" });
  await dialog.getByRole("button", { name: "Integrations" }).first().click();
  await dialog.locator("li", { hasText: "WhatsApp Business" }).first().getByRole("button").first().click();
  return dialog;
}

test("The WhatsApp screen says when the last message arrived and lists what Meta sent", async ({ page }) => {
  const twoDaysAgo = new Date(Date.now() - 2 * 24 * 3600 * 1000).toISOString();
  const dialog = await openWhatsApp(page, {
    lastCallAt: twoDaysAgo, lastMessageAt: twoDaysAgo,
    receipts: [
      { receivedAt: twoDaysAgo, phoneNumberId: "1224011020803768", outcome: "accepted", messagesInCall: 1, messagesNew: 1, deliveryUpdates: 0 },
      { receivedAt: twoDaysAgo, phoneNumberId: "1224011020803768", outcome: "refused_signature", messagesInCall: 0, messagesNew: 0, deliveryUpdates: 0 },
    ],
  });

  const panel = dialog.getByTestId("whatsapp-delivery");
  await expect(panel.getByTestId("whatsapp-last-message")).toContainText("2 days ago");
  await expect(panel.getByText("Message saved")).toBeVisible();
  await expect(panel.getByText("Refused: signature did not match")).toBeVisible();
  await expect(panel.getByRole("alert")).toContainText("app secret");
});

test("With no calls recorded it says so plainly, and Check with Meta lists what Meta reports", async ({ page }) => {
  const dialog = await openWhatsApp(page, { lastCallAt: null, lastMessageAt: null, receipts: [] });

  const panel = dialog.getByTestId("whatsapp-delivery");
  await expect(panel.getByTestId("whatsapp-last-message")).toContainText("never");
  await expect(panel.getByText("No call from Meta has been recorded yet")).toBeVisible();

  await panel.getByRole("button", { name: "Check with Meta" }).click();
  const findings = panel.getByTestId("whatsapp-findings").getByRole("listitem");
  await expect(findings).toHaveCount(3);
  await expect(findings.nth(1)).toContainText("cashflow.example.com");
  await expect(findings.nth(2)).toContainText("published");
});
