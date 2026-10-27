import { expect, test } from "@playwright/test";

// A phone of its own for this run, so repeated runs record a new event instead of meeting yesterday's.
const SUFFIX = String(Date.now()).slice(-6);
const PHONE = `+256 79${SUFFIX.slice(0, 1)} ${SUFFIX.slice(1, 4)} ${SUFFIX.slice(4)}`;

async function openEvents(page: import("@playwright/test").Page) {
  await page.goto("/app/acquisition");
  await page.getByRole("tab", { name: "Bank events" }).click();
  await expect(page.getByRole("heading", { name: "Bank events" })).toBeVisible();
}

test("a person records a bank event and it appears waiting for the rules", async ({ page }) => {
  await openEvents(page);
  await page.getByRole("button", { name: "Record an event" }).click();

  const form = page.getByRole("form", { name: "Record a bank event" });
  await form.getByLabel("What happened").selectOption("loan_disbursed");
  await form.getByLabel("Customer's phone").fill(PHONE);
  await form.getByLabel("When it happened").selectOption("2");
  await form.getByLabel("Amount (UGX)").fill("8,000,000");
  await form.getByLabel("Term in months").fill("36");
  await form.getByLabel("Branch").fill("Ntinda");
  await form.getByRole("button", { name: "Save the event" }).click();

  await expect(page.getByText("Event recorded. It is waiting for the rules to look at it.")).toBeVisible();
  const row = page.getByRole("row", { name: /Loan disbursed/ }).filter({ hasText: "UGX 8,000,000 · 36 months · Ntinda" }).first();
  await expect(row).toBeVisible();
  await expect(row.getByText("Waiting")).toBeVisible();
  // The list shows a masked number, never the whole one.
  await expect(row.getByText(/\*\*\*/)).toBeVisible();
  await page.screenshot({ path: `test-results/workspace/bank-events-${test.info().project.name}.png`, fullPage: true });
});

test("recording the same event twice says it was already known", async ({ page }) => {
  await openEvents(page);
  for (let attempt = 0; attempt < 2; attempt++) {
    await page.getByRole("button", { name: "Record an event" }).click();
    const form = page.getByRole("form", { name: "Record a bank event" });
    await form.getByLabel("What happened").selectOption("salary_account_opened");
    await form.getByLabel("Customer's phone").fill(PHONE);
    await form.getByLabel("When it happened").selectOption("3");
    await form.getByRole("button", { name: "Save the event" }).click();
    await expect(page.getByRole("status").filter({ hasText: /recorded|already recorded/ })).toBeVisible();
  }
  await expect(page.getByText("That event was already recorded, so nothing changed.")).toBeVisible();
});

test("a bad phone number is refused with a reason a person can act on", async ({ page }) => {
  await openEvents(page);
  await page.getByRole("button", { name: "Record an event" }).click();
  const form = page.getByRole("form", { name: "Record a bank event" });
  await form.getByLabel("Customer's phone").fill("12345");
  await form.getByRole("button", { name: "Save the event" }).click();
  await expect(form.getByRole("alert")).toContainText("phone number is not usable");
});

test("a file is read row by row, and balances and account numbers are named as left out", async ({ page }) => {
  await openEvents(page);
  const csv = [
    "external_id,phone,event_type,occurred_at,amount,term_months,balance,account_number",
    `F-${SUFFIX},+256 78${SUFFIX.slice(0, 1)} ${SUFFIX.slice(1, 4)} ${SUFFIX.slice(4)},loan_disbursed,${new Date(Date.now() - 86400000).toISOString().slice(0, 10)},5000000,24,9000000,0123`,
    "F-bad,12,loan_disbursed,2026-10-01,,,,",
  ].join("\n");
  await page.getByLabel("Load a file of events").setInputFiles({ name: "daily.csv", mimeType: "text/csv", buffer: Buffer.from(csv) });

  const result = page.getByLabel("File result");
  await expect(result).toContainText("daily.csv: 2 rows read, 1 new, 0 already known, 1 refused.");
  await expect(result).toContainText("Left out on purpose: balance, account_number");
  await expect(result).toContainText("Row 3");
  await page.screenshot({ path: `test-results/workspace/bank-events-file-${test.info().project.name}.png`, fullPage: true });
});
