import { expect, test } from "@playwright/test";
import { installVisualApiFixtures } from "../fixtures/iip-fixtures";

test.beforeEach(async ({ page }) => {
  await installVisualApiFixtures(page);
});

test("Gate 16 conversations stay findable and distinguish changed evidence", async ({ page }) => {
  await page.goto("/app/conversations");
  await expect(page.getByRole("heading", { name: "All conversations" })).toBeVisible();
  const main = page.getByRole("main");
  await expect(main.getByText("Review motor claim evidence")).toBeVisible();
  await expect(main.getByText("Evidence changed since saved answer")).toBeVisible();

  await page.getByPlaceholder("Find by title, claim, work item, or owner").fill("no such work");
  await expect(page.getByText("No matching conversations")).toBeVisible();
  await page.getByRole("button", { name: "Clear filter" }).click();
  await main.getByText("Review motor claim evidence").click();

  await expect(page.getByText("Evidence changed since this answer")).toBeVisible();
  await expect(page.getByText(/Historical responses are shown for continuity/)).toBeVisible();
  await expect(page.getByText("The police report was missing when this answer was saved.")).toBeVisible();
});

test("Gate 16 archive is reversible and is not deletion", async ({ page }) => {
  await page.goto("/app/conversations");
  await page.getByRole("button", { name: "Archive Review motor claim evidence" }).click();
  await expect(page.getByText("No conversations yet")).toBeVisible();
  await page.getByRole("button", { name: "Archived" }).click();
  await expect(page.getByRole("main").getByText("Review motor claim evidence")).toBeVisible();
  await page.getByRole("button", { name: "Restore Review motor claim evidence" }).click();
  await expect(page.getByText("No archived conversations")).toBeVisible();
});

test("Gate 16 preserves a safe unsent draft through interruption", async ({ page }) => {
  const href = "/app/conversations/11111111-1111-4111-8111-111111111111";
  await page.goto(href);
  const composer = page.getByPlaceholder("Ask IIP anything — claims, documents, customers, policies, workflows…");
  await composer.fill("Check the updated police report before continuing");
  await page.reload();
  await expect(composer).toHaveValue("Check the updated police report before continuing");
});

test("Gate 16 invalid extraction links stay bounded", async ({ page }) => {
  await page.goto("/app/claims/%E2%80%94/extraction?documentId=%E2%80%94");
  await expect(page.getByText("Document not available for extraction review")).toBeVisible();
  await expect(page.getByText("This claim link is invalid.")).toBeVisible();
  await expect(page.getByRole("link", { name: "Return to claims" })).toHaveAttribute("href", "/app/claims");
});
