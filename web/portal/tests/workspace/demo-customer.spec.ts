import { expect, test } from "@playwright/test";

const API = "http://localhost:5167";
let organisationId = "";
const SUFFIX = String(Date.now()).slice(-6);
const phone = (lead: string) => `+256 78${lead} ${SUFFIX.slice(0, 3)} ${SUFFIX.slice(3)}`;

test.beforeAll(async ({ request }) => {
  const activated = await request.post(`${API}/api/v1/demo/scenarios/mavuno-bank/activate`, {
    headers: { "x-demo-role": "Admin", "X-User-Id": `spec-demo-customer-${Date.now()}`, "Content-Type": "application/json", "X-Tenant-Id": "tenant-demo", "X-Workspace-Id": "workspace-operations" },
  });
  expect(activated.ok()).toBeTruthy();
  organisationId = (await activated.json()).organisationId;
});

test.beforeEach(async ({ page }) => {
  await page.addInitScript(([org]) => {
    window.localStorage.setItem("iip.active-organisation-id", org);
    window.localStorage.setItem("iip.active-workspace-id", "main");
  }, [organisationId]);
});

async function addCustomer(page: import("@playwright/test").Page, name: string, number: string, consent: string) {
  await page.goto("/app/acquisition");
  await page.getByRole("tab", { name: "Bank events" }).click();
  await page.getByRole("button", { name: "Add a demonstration customer" }).click();
  const form = page.getByRole("form", { name: "Add a demonstration customer" });
  await form.getByRole("button", { name: /Fill in Eugene/ }).click();
  await form.getByLabel("Name").fill(name);
  await form.getByLabel("WhatsApp number").fill(number);
  await form.getByLabel("Agreement to be contacted about insurance").selectOption(consent);
  await form.getByRole("button", { name: "Add the customer" }).click();
  await expect(page.getByRole("status").filter({ hasText: `${name} was added` })).toBeVisible();
}

test("Eugene is added, the rule is approved, and the story explains why he was messaged", async ({ page }) => {
  await addCustomer(page, "Eugene Spec", phone("1"), "Agreed");
  await expect(page.getByRole("row").filter({ hasText: "Loan disbursed" }).filter({ hasText: "UGX 8,000,000 · 36 months · Ntinda branch" }).first()).toBeVisible();

  await page.getByRole("tab", { name: "Rules" }).click();
  const card = page.getByRole("listitem").filter({ hasText: "Loan Protector after a salary loan" });
  await expect(card.getByText("Draft, not approved")).toBeVisible();
  await card.getByRole("button", { name: "Approve this rule" }).click();
  await expect(card.getByText("Running")).toBeVisible();
  await page.getByRole("button", { name: "Run the rules now" }).click();
  await expect(page.getByRole("status").filter({ hasText: /Looked at \d+/ })).toBeVisible();

  const row = page.getByRole("table").last().getByRole("row").filter({ hasText: "Eugene Spec" }).first();
  await expect(row).toContainText("Shown as sent, not delivered");
  await row.getByRole("button", { name: "Why this person" }).click();

  const story = page.getByRole("dialog");
  await expect(story.getByText("Why Eugene Spec was messaged")).toBeVisible();
  const steps = story.getByRole("list", { name: "What happened, oldest first" }).getByRole("listitem");
  await expect(steps).toHaveCount(4);
  await expect(steps.nth(0)).toContainText("Agreed to be contacted");
  await expect(steps.nth(1)).toContainText("Loan disbursed");
  await expect(steps.nth(2)).toContainText("shown as sent");
  await expect(steps.nth(3)).toContainText("Hello Eugene, this is Kato from Mavuno Bank");
  await steps.nth(2).getByRole("button", { name: "Show every check" }).click();
  await expect(steps.nth(2)).toContainText("Agreed to be contacted: passed");
  await expect(steps.nth(2)).toContainText("Best-fit cover: passed");
  await page.screenshot({ path: `test-results/workspace/story-${test.info().project.name}.png` });
});

test("someone who never agreed is skipped, and the story says exactly that", async ({ page }) => {
  await addCustomer(page, "Never Asked Spec", phone("2"), "NotAsked");

  await page.getByRole("tab", { name: "Rules" }).click();
  // The rule may already be running from the other test; approve it only when it is still a draft.
  const card = page.getByRole("listitem").filter({ hasText: "Loan Protector after a salary loan" });
  if (await card.getByRole("button", { name: "Approve this rule" }).count()) {
    await card.getByRole("button", { name: "Approve this rule" }).click();
    await expect(card.getByText("Running")).toBeVisible();
  }
  await page.getByRole("button", { name: "Run the rules now" }).click();
  await expect(page.getByRole("status").filter({ hasText: /Looked at \d+/ })).toBeVisible();

  const row = page.getByRole("table").last().getByRole("row").filter({ hasText: "Never Asked Spec" }).first();
  await expect(row).toContainText("Not sent");
  await expect(row).toContainText("no recorded agreement");
});
