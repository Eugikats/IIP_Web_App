import { expect, test } from "@playwright/test";

const documentId = "11111111-1111-4111-8111-111111111111";
const proposalId = "22222222-2222-4222-8222-222222222222";

test("a person reviews cited wording facts before creating a draft", async ({ page }) => {
  await page.route("**/api/v1/knowledge-documents", async (route) => route.fulfill({
    json: [{
      knowledgeDocumentId: documentId,
      documentName: "Test wording, browser fixture",
      productLine: "Motor",
      documentType: "PolicyWording",
      approvalStatus: "Approved",
      indexedStatus: "Indexed",
      sensitivity: "Internal",
      governanceReason: "Browser test fixture",
      updatedAt: "2026-10-27T08:00:00Z",
    }],
  }));
  await page.route("**/api/v1/products/wording-extractions**", async (route) => {
    if (route.request().method() === "GET") return route.fulfill({ json: [] });
    return route.fulfill({
      json: {
        proposalId,
        knowledgeDocumentId: documentId,
        sourceName: "Test wording, browser fixture",
        sourceVersion: "test-only",
        provider: "browser fixture",
        modelVersion: null,
        preparedAt: "2026-10-27T08:00:00Z",
        preparedByUserId: "browser-test",
        items: [
          { category: "product_name", label: "Product name", value: "Test Motor Cover", sourceChunkId: "chunk-1", sourceQuote: "Test Motor Cover provides collision cover.", confidence: 0.91 },
          { category: "product_class", label: "Product class", value: "Motor", sourceChunkId: "chunk-1", sourceQuote: "Test Motor Cover provides collision cover.", confidence: 0.88 },
          { category: "cover", label: "Collision cover", value: "Collision damage", sourceChunkId: "chunk-1", sourceQuote: "Test Motor Cover provides collision cover.", confidence: 0.84 },
        ],
      },
    });
  });
  await page.route("**/api/v1/products**", async (route) => {
    const url = new URL(route.request().url());
    if (route.request().method() !== "POST" || url.pathname.replace(/\/$/, "") !== "/api/v1/products") return route.fallback();
    const body = route.request().postDataJSON() ?? {};
    expect(body.wordingFacts).toHaveLength(3);
    expect(body.wordingFacts[0].sourceQuote).toBe("Test Motor Cover provides collision cover.");
    return route.fulfill({ json: { productId: "draft-test-id", ...body }, headers: { "x-iip-test-route": "mocked" } });
  });

  await page.goto("/app/admin/products/wordings");
  await expect(page.getByRole("heading", { name: "Prepare a product from its wording" })).toBeVisible();
  await page.getByLabel("Policy wording").selectOption(documentId);
  await page.getByRole("button", { name: "Read this wording" }).click();

  await expect(page.getByText("Test Motor Cover provides collision cover.").first()).toBeVisible();
  await expect(page.getByText("91% model estimate")).toBeVisible();
  await expect(page.getByText("AI prepares a draft only.")).toBeVisible();
  await page.screenshot({ path: `test-results/workspace/wording-review-${test.info().project.name}.png`, fullPage: true });
  const creation = page.waitForResponse((response) => response.request().method() === "POST" && response.url().endsWith("/api/v1/products"));
  await page.getByRole("button", { name: "Create product draft" }).click();
  const createdResponse = await creation;
  expect(createdResponse.status()).toBe(200);
  expect(createdResponse.headers()["x-iip-test-route"]).toBe("mocked");
  expect((await createdResponse.json()).productId).toBe("draft-test-id");
  await expect(page.getByRole("status")).toContainText("Product draft created.");
  await expect(page.getByRole("link", { name: "Open product draft" })).toHaveAttribute("href", "/app/admin/products/draft-test-id");
});
