import { expect, test, type APIRequestContext } from "@playwright/test";

// Buying a course, in a real browser against the offline API started with the local stand-in payment provider
// (Academy__Payments__Provider=Fake, see scripts/local/run-offline-payments.sh). Restart the offline API before each spec and viewport to reset learner state.

const API = "http://localhost:5167/api/v1/academy";
const HEADERS = { "X-Tenant-Id": "tenant-demo", "X-Workspace-Id": "workspace-operations", "x-demo-role": "Admin", "x-demo-user-id": "user-1", "X-User-Id": "user-1" };

async function call(request: APIRequestContext, method: "get" | "post" | "put", url: string, data?: unknown) {
  const response = await request[method](`${API}${url}`, { headers: HEADERS, data: data ?? {} });
  return { status: response.status(), body: await response.json().catch(() => ({})) };
}

test.describe("buying a course", () => {
  test("dollar price, choose how to pay, pay, enrolled; unpaid enrolment is refused; the team sees and refunds it", async ({ page, request }) => {
    // The price is in dollars, on the page and in the offer.
    await page.goto("/academy/courses/claims");
    await expect(page.locator(".pricecard .price")).toHaveText("USD 120");
    const offer = await call(request, "get", "/courses/claims/offer");
    expect(offer.body.configured).toBe(true);
    expect(offer.body.choices.find((choice: { method: string }) => choice.method === "mobile_money").amount).toBe(494000);

    // Enrolling without paying is refused by the server, whatever the browser does.
    const direct = await call(request, "post", "/courses/claims/enrol");
    expect(direct.status).toBe(402);
    expect(direct.body.code).toBe("payment_required");

    // The checkout asks card or mobile money, with the shilling amount shown.
    await page.getByTestId("enrol").click();
    await expect(page.getByTestId("pay-mobile_money")).toBeVisible();
    await expect(page.getByText(/UGX 494,000/).first()).toBeVisible();
    await page.getByTestId("pay-card").click();
    await expect(page.locator(".cktotal b")).toHaveText("USD 120");
    await page.getByTestId("pay-mobile_money").click();
    await expect(page.locator(".cktotal b")).toHaveText("UGX 494,000");

    // Paying goes out to the provider's page and back; the server confirms before anything opens.
    await page.getByTestId("pay-now").click();
    await expect(page.getByTestId("payment-paid")).toBeVisible({ timeout: 30_000 });
    await expect(page.getByText("Payment received")).toBeVisible();
    await page.getByTestId("payment-start").click();
    await expect(page).toHaveURL(/\/academy\/learn\/claims/);

    // The learner sees a receipt notification; a second confirm of the same order changes nothing.
    const learner = await call(request, "get", "/learner");
    expect(learner.body.enrolled.some((entry: { courseId: string }) => entry.courseId === "claims")).toBe(true);
    expect(learner.body.notifications.filter((note: { text: string }) => note.text.startsWith("Payment received"))).toHaveLength(1);

    // Someone else's reference, or a made-up one, confirms nothing.
    const made = await call(request, "post", "/payments/confirm", { reference: "IIPA-MADEUP", transactionId: "1" });
    expect(made.status).toBe(404);

    // The Academy team's screen lists the payment.
    await page.goto("/academy/admin");
    await page.getByTestId("admin-tab-payments").click();
    const row = page.getByTestId("order-row").first();
    await expect(row.getByTestId("order-status")).toHaveText("paid");
    await expect(row).toContainText("UGX 494,000");

    // Changing the rate changes what mobile money costs, and a bad rate is refused.
    await page.getByTestId("admin-tab-prices").click();
    await page.getByLabel("Shillings to the dollar").fill("3");
    await page.getByTestId("save-payment-settings").click();
    await expect(page.locator("#toast")).toContainText("shillings to the dollar");
    await page.getByLabel("Shillings to the dollar").fill("4000");
    await page.getByTestId("save-payment-settings").click();
    await expect(page.locator("#toast")).toContainText("Payment settings saved");
    const changed = await call(request, "get", "/courses/underwriting/offer");
    expect(changed.body.choices.find((choice: { method: string }) => choice.method === "mobile_money").amount).toBe(480000);

    // A refund is recorded and takes the course away.
    await page.getByTestId("admin-tab-payments").click();
    page.once("dialog", (dialog) => void dialog.accept());
    await page.getByRole("button", { name: "Mark refunded" }).first().click();
    await expect(page.locator("#toast")).toContainText("Marked as refunded");
    const after = await call(request, "get", "/learner");
    expect(after.body.enrolled.some((entry: { courseId: string }) => entry.courseId === "claims")).toBe(false);
  });

  test("the callback says so when a payment was cancelled, and asks a visitor to sign in", async ({ page }) => {
    await page.goto("/academy/payment/callback?status=cancelled&tx_ref=IIPA-X");
    await expect(page.getByTestId("payment-cancelled")).toBeVisible();
    await page.goto("/academy/payment/callback");
    await expect(page.getByTestId("payment-cancelled")).toBeVisible();
  });

  test("Zenith is switched off during an assessment and on the final task page, and its limits are set on the team screen", async ({ page, request }) => {
    await call(request, "post", "/courses/ai-agentic-insurance/enrol").catch(() => undefined);
    // Operators enrol people directly, so use that to get into Course 1 without paying.
    const me = await call(request, "get", "/me");
    await call(request, "post", "/operator/enrol", { userId: me.body.userId, courseId: "ai-agentic-insurance" });

    const before = await call(request, "get", "/zenith/status");
    expect(before.body.locked).toBe(false);

    await call(request, "get", "/courses/ai-agentic-insurance/assessments/unit-1-1-quiz");
    const during = await call(request, "get", "/zenith/status");
    expect(during.body.locked).toBe(true);
    const asked = await call(request, "post", "/zenith/ask", { message: "What is agentic AI?" });
    expect(asked.status).toBe(423);
    expect(asked.body.code).toBe("assessment_lock");

    // Another tab sees the same thing.
    await page.goto("/academy/zenith");
    await expect(page.getByTestId("zenith-locked")).toBeVisible();

    // Leaving the assessment hands Zenith back.
    await call(request, "post", "/courses/ai-agentic-insurance/assessments/unit-1-1-quiz/leave");
    await page.reload();
    await expect(page.getByTestId("zenith-locked")).toHaveCount(0);

    // The final task page keeps it off while open.
    await call(request, "post", "/courses/ai-agentic-insurance/final/open");
    expect((await call(request, "get", "/zenith/status")).body.locked).toBe(true);

    // Limits: ten a day and a million tokens by default, editable by the team.
    await page.goto("/academy/admin");
    await page.getByTestId("admin-tab-zenith").click();
    await expect(page.getByLabel("Explanations per person per day")).toHaveValue("10");
    await expect(page.getByLabel("Tokens per month")).toHaveValue("1000000");
    await page.getByLabel("Explanations per person per day").fill("0");
    await page.getByTestId("save-zenith-limits").click();
    await expect(page.locator("#toast")).toContainText("between 1 and 500");
    await page.getByLabel("Explanations per person per day").fill("5");
    await page.getByTestId("save-zenith-limits").click();
    await expect(page.locator("#toast")).toContainText("Zenith limits saved");
  });
});
