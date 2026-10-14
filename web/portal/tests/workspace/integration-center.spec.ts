import { expect, test, type Page } from "@playwright/test";

// The Integration Center shows one of four states per card, each earned by a check the server made. The first test
// runs against the real offline API; the others answer the catalogue call with the states a live organisation can be in.

async function openIntegrations(page: Page) {
  await page.goto("/app");
  await page.getByRole("button", { name: "Settings", exact: true }).first().click();
  const dialog = page.getByRole("dialog", { name: "Settings" });
  await dialog.getByRole("button", { name: "Integrations" }).first().click();
  return dialog;
}

const STATES = ["Connected", "Needs attention", "Not connected", "Not configured"];

test("Every integration card on the real API shows one honest state, and Teams is listed", async ({ page }) => {
  const dialog = await openIntegrations(page);

  const teams = dialog.locator("li", { hasText: "Microsoft Teams" }).first();
  await expect(teams).toBeVisible();
  for (const name of ["WhatsApp Business", "SMS", "Email", "Microsoft Teams"]) {
    const card = dialog.locator("li", { hasText: name }).first();
    await expect(card).toBeVisible();
    await expect(card.getByText(new RegExp(`^(${STATES.join("|")})$`)).first()).toBeVisible();
  }
  // Nothing in the local setup is connected to a real provider, so nothing may claim it is.
  await expect(dialog.getByText("Connected", { exact: true })).toHaveCount(0);
});

test("A WhatsApp number whose calls are being refused needs attention, and says why", async ({ page }) => {
  await page.route("**/api/v1/integrations/catalogue", (route) =>
    route.fulfill({
      json: {
        checkedAt: new Date().toISOString(),
        cards: [
          {
            key: "whatsapp", name: "WhatsApp", group: "Channels", state: "NeedsAttention", stateLabel: "Needs attention",
            summary: "4 call(s) from Meta were refused in the last day and none was accepted since. Check the app secret.",
            setupHint: "Settings, Connections, WhatsApp",
            checks: [
              { name: "Messages from customers", passed: false, note: "No customer message has arrived yet.", blocking: false },
              { name: "Meta's calls are accepted", passed: false, note: "4 call(s) from Meta were refused in the last day and none was accepted since. Check the app secret.", blocking: true },
            ],
          },
          { key: "email", name: "Email", group: "Channels", state: "Connected", stateLabel: "Connected", summary: "Connected through Graph.", setupHint: null, checks: [{ name: "Last test with the provider", passed: true, note: "The provider accepted the saved settings.", blocking: true }] },
          { key: "teams", name: "Microsoft Teams", group: "Channels", state: "NotConfigured", stateLabel: "Not configured", summary: "Teams alerts are not set up on this platform yet.", setupHint: null, checks: [] },
        ],
      },
    }));

  const dialog = await openIntegrations(page);

  const whatsapp = dialog.locator("li", { hasText: "WhatsApp Business" }).first();
  await expect(whatsapp.getByText("Needs attention", { exact: true })).toBeVisible();
  await expect(whatsapp.getByText("Check the app secret.").first()).toBeVisible();
  await expect(whatsapp.getByRole("list", { name: "WhatsApp Business checks" })).toBeVisible();

  const email = dialog.locator("li", { hasText: "Email" }).first();
  await expect(email.getByText("Connected", { exact: true })).toBeVisible();

  const teams = dialog.locator("li", { hasText: "Microsoft Teams" }).first();
  await expect(teams.getByText("Not configured", { exact: true })).toBeVisible();
});

test("The Yours tab lists only what is connected or needs attention", async ({ page }) => {
  await page.route("**/api/v1/integrations/catalogue", (route) =>
    route.fulfill({
      json: {
        checkedAt: new Date().toISOString(),
        cards: [
          { key: "whatsapp", name: "WhatsApp", group: "Channels", state: "NeedsAttention", stateLabel: "Needs attention", summary: "Refused.", setupHint: null, checks: [] },
          { key: "email", name: "Email", group: "Channels", state: "Connected", stateLabel: "Connected", summary: "Connected through Graph.", setupHint: null, checks: [] },
          { key: "sms", name: "SMS", group: "Channels", state: "NotConnected", stateLabel: "Not connected", summary: "Not connected.", setupHint: null, checks: [] },
          { key: "teams", name: "Microsoft Teams", group: "Channels", state: "NotConfigured", stateLabel: "Not configured", summary: "Not set up.", setupHint: null, checks: [] },
        ],
      },
    }));

  const dialog = await openIntegrations(page);
  await dialog.getByRole("tab", { name: "Yours" }).click();

  await expect(dialog.locator("li", { hasText: "WhatsApp Business" })).toHaveCount(1);
  await expect(dialog.locator("li", { hasText: "Send customer email" })).toHaveCount(1);
  await expect(dialog.locator("li", { hasText: "SMS" })).toHaveCount(0);
  await expect(dialog.locator("li", { hasText: "Microsoft Teams" })).toHaveCount(0);
});

test("When the catalogue cannot be read, Teams says it could not check and nothing claims to be connected", async ({ page }) => {
  await page.route("**/api/v1/integrations/catalogue", (route) => route.fulfill({ status: 500, json: { title: "failed" } }));

  const dialog = await openIntegrations(page);

  const teams = dialog.locator("li", { hasText: "Microsoft Teams" }).first();
  await expect(teams.getByText("Could not check", { exact: true })).toBeVisible();
  await expect(dialog.getByText("Connected", { exact: true })).toHaveCount(0);
});
