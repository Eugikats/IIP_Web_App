import { expect, test, type APIRequestContext } from "@playwright/test";

// The community and the Academy team's screen, in a real browser against the offline API. user-1 is the browser's person and
// an Academy operator; other people are driven through the API. Restart the offline API before each spec and viewport to reset learner state.

const API = "http://localhost:5167/api/v1/academy";
const base = { "X-Tenant-Id": "tenant-demo", "X-Workspace-Id": "workspace-operations" };
const asUser = (id: string, role = "Learner") => ({ ...base, "x-demo-role": role, "x-demo-user-id": id, "X-User-Id": id });

async function call(request: APIRequestContext, user: string, method: "get" | "post" | "put", url: string, data?: unknown) {
  const response = await request[method](`${API}${url}`, { headers: asUser(user), data: data ?? {} });
  return { status: response.status(), body: await response.json().catch(() => ({})) };
}

test.describe("community and the Academy team", () => {
  test("join a group, post after the notice, hear back, manage the Academy", async ({ page, request }) => {
    await page.goto("/academy/community");
    await expect(page.getByRole("heading", { name: /Welcome to the community/ })).toBeVisible();
    await expect(page.getByText("Join us on Discord")).toHaveCount(0);

    // Joining a group opens the composer.
    await page.getByTestId("join-group").click();
    await page.getByRole("button", { name: /Claims Professionals/ }).click();
    await page.getByTestId("save-groups").click();
    await expect(page.getByLabel("Write a post")).toBeVisible();

    // The first post asks for the community notice, then goes through.
    await page.getByLabel("Write a post").fill("We call every motor claimant within a day of lodging.");
    await page.getByTestId("post").click();
    await page.getByTestId("accept-consent").click();
    await expect(page.getByTestId("post-card")).toHaveCount(1);
    await expect(page.getByTestId("post-card")).toContainText("We call every motor claimant");

    // Personal data is refused with a reason.
    await page.getByLabel("Write a post").fill("Please call the claimant on 0772 123 456");
    await page.getByTestId("post").click();
    await expect(page.locator("#toast")).toContainText("Remove phone, policy or claim numbers");
    await expect(page.getByTestId("post-card")).toHaveCount(1);

    // Another person likes and replies; the author sees both and is notified.
    const community = await call(request, "user-1", "get", "/community");
    const postId = community.body.posts[0].id as string;
    await call(request, "user-2", "post", "/community/consent");
    await call(request, "user-2", "post", "/community/groups/g-claims/join");
    expect((await call(request, "user-2", "post", `/community/posts/${postId}/like`)).status).toBe(200);
    expect((await call(request, "user-2", "post", `/community/posts/${postId}/comments`, { text: "Same results for us." })).status).toBe(200);
    await page.reload();
    await expect(page.getByTestId("post-card")).toContainText("1 comment");
    await page.getByTestId("toggle-comments").click();
    await expect(page.getByText("Same results for us.")).toBeVisible();
    await page.getByLabel("Write a comment").fill("Thanks for sharing.");
    await page.getByRole("button", { name: "Send comment" }).click();
    await expect(page.getByTestId("post-card")).toContainText("2 comments");

    // Tabs: nothing is a question yet.
    await page.getByRole("button", { name: "Questions" }).click();
    await expect(page.getByTestId("no-posts")).toBeVisible();
    await page.getByRole("button", { name: "Latest" }).click();

    // A person who is not on the Academy team cannot use its controls.
    expect((await call(request, "user-2", "put", "/operator/community/discord", { url: "https://discord.gg/abc" })).status).toBe(403);
    expect((await call(request, "user-2", "get", "/operator/finals")).status).toBe(403);

    // The Academy team: a Discord link, an event and a group.
    await page.goto("/academy");
    await page.locator(".user").click();
    await page.getByTestId("admin-link").click();
    await expect(page.getByRole("heading", { name: "Manage the Academy" })).toBeVisible();
    await page.getByTestId("admin-tab-community").click();
    await page.getByLabel("Discord invite link").fill("https://example.com/not-discord");
    await page.getByTestId("save-discord").click();
    await expect(page.locator("#toast")).toContainText("Enter a Discord invite link");
    await page.getByLabel("Discord invite link").fill("https://discord.gg/iip-academy");
    await page.getByTestId("save-discord").click();
    await expect(page.locator("#toast")).toContainText("Discord link saved");

    await page.getByLabel("Event title").fill("Claims CX roundtable");
    const when = new Date(Date.now() + 3 * 86_400_000);
    const stamp = `${when.getFullYear()}-${String(when.getMonth() + 1).padStart(2, "0")}-${String(when.getDate()).padStart(2, "0")}T10:00`;
    await page.getByLabel("Starts at (East Africa Time)").fill(stamp);
    await page.getByTestId("add-event").click();
    await expect(page.locator("#toast")).toContainText("Event added");
    await page.getByLabel("Group name").fill("Microinsurance");
    await page.getByLabel("Group description").fill("Mobile-first cover for low-income customers");
    await page.getByRole("button", { name: "Add group" }).click();
    await expect(page.locator("#toast")).toContainText("Group added");

    // The community now shows all three.
    await page.goto("/academy/community");
    await expect(page.getByRole("link", { name: /Join us on Discord/ })).toHaveAttribute("href", "https://discord.gg/iip-academy");
    await expect(page.getByText("Claims CX roundtable")).toBeVisible();
    await page.getByRole("button", { name: "RSVP" }).click();
    await expect(page.getByRole("button", { name: "Going" })).toBeVisible();
    await page.getByRole("button", { name: "Manage groups" }).click();
    await expect(page.getByRole("button", { name: /Microinsurance/ })).toBeVisible();
    await page.keyboard.press("Escape");

    // Three reports hide a post; the team can see and restore it.
    for (const reporter of ["user-3", "user-4", "user-5"]) {
      expect((await call(request, reporter, "post", `/community/posts/${postId}/report`, { reason: "Spam" })).status).toBe(200);
    }
    await page.reload();
    await expect(page.getByTestId("no-posts")).toBeVisible();
    await page.goto("/academy/admin");
    await page.getByTestId("admin-tab-community").click();
    await expect(page.getByText("We call every motor claimant")).toBeVisible();
    await page.getByRole("button", { name: "Restore" }).click();
    await expect(page.locator("#toast")).toContainText("Post restored");
    await page.goto("/academy/community");
    await expect(page.getByTestId("post-card")).toHaveCount(1);
  });
});
