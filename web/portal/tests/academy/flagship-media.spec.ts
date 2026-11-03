import { expect, test } from "@playwright/test";

const api = "http://localhost:5167/api/v1/academy";
const headers = { "X-Tenant-Id": "tenant-demo", "X-Workspace-Id": "workspace-operations", "x-demo-role": "Admin", "x-demo-user-id": "user-1", "X-User-Id": "user-1" };

for (const [courseId, lessonId] of [["ai-agentic-insurance", "unit-1-1"], ["ai-sales-distribution", "sales-unit-1-1"]]) {
  test(`${courseId}: private video, captions, downloads and independent quiz`, async ({ page, request }, info) => {
    const userId = `media-review-${info.project.name}-${courseId}-${Date.now()}`;
    const learnerHeaders = { ...headers, "x-demo-user-id": userId, "X-User-Id": userId };
    await page.route("http://localhost:5167/**", (route) => route.continue({ headers: { ...route.request().headers(), "x-demo-user-id": userId, "x-user-id": userId } }));
    const otherHeaders = { ...headers, "x-demo-user-id": "unenrolled-reviewer", "X-User-Id": "unenrolled-reviewer" };
    const denied = await request.get(`${api}/courses/${courseId}/lessons/${lessonId}/media/video.mp4`, { headers: otherHeaders });
    expect(denied.status()).toBe(403);
    const enrolment = await request.post(`${api}/operator/enrol`, { headers, data: { courseId, userId } });
    expect(enrolment.ok()).toBeTruthy();
    const catalogue = await request.get(`${api}/public/courses/${courseId}`);
    const course = await catalogue.json();
    expect(course.paidReleaseApproved).toBe(false);
    expect(course.price).toBe(120);
    expect(course.modules).toHaveLength(4);
    expect(course.modules.flatMap((module: { lessons: unknown[] }) => module.lessons)).toHaveLength(12);
    const lesson = await request.get(`${api}/courses/${courseId}/lessons/${lessonId}`, { headers: learnerHeaders });
    expect(JSON.stringify(await lesson.json())).not.toContain('"answer"');
    await page.goto(`/academy/learn/${courseId}/${lessonId}`, { waitUntil: "networkidle" });
    const video = page.getByLabel("Lesson video", { exact: true });
    await expect(video).toBeVisible({ timeout: 90_000 });
    await expect.poll(() => video.evaluate((element) => (element as HTMLVideoElement).readyState), { timeout: 30_000 }).toBeGreaterThanOrEqual(2);
    expect(await video.evaluate((element) => (element as HTMLVideoElement).duration)).toBeGreaterThan(300);
    await video.evaluate(async (element) => {
      const player = element as HTMLVideoElement;
      player.muted = true;
      await player.play();
    });
    await expect.poll(() => video.evaluate((element) => (element as HTMLVideoElement).currentTime)).toBeGreaterThan(0.1);
    await video.evaluate((element) => (element as HTMLVideoElement).pause());
    await expect(video.locator("track")).toHaveAttribute("kind", "captions");
    const contentBox = await page.locator(".lcontent").boundingBox();
    expect(contentBox).not.toBeNull();
    expect(contentBox!.x + contentBox!.width).toBeLessThanOrEqual(page.viewportSize()!.width + 1);
    await page.screenshot({ path: info.outputPath(`${courseId}-lesson.png`), fullPage: true });
    for (const label of ["Download audio", "Download slides", "Download transcript"]) {
      await page.getByRole("button", { name: label, exact: true }).click();
      const waiting = page.waitForEvent("download");
      await page.getByRole("link", { name: label.replace("Download", "Save"), exact: true }).click();
      const download = await waiting;
      expect(await download.failure()).toBeNull();
    }
    await page.getByTestId("take-quiz").click();
    await expect(page.getByTestId("question")).toHaveCount(10);
    await expect(page.getByTestId("submit-assessment")).toBeDisabled();
    const status = await request.get(`${api}/zenith/status`, { headers: learnerHeaders });
    expect((await status.json()).locked).toBe(true);
    await page.locator(`a[href="/academy/courses/${courseId}"]`).first().click();
    await expect(page).toHaveURL(new RegExp(`/academy/courses/${courseId}$`));
    await expect.poll(async () => (await (await request.get(`${api}/zenith/status`, { headers: learnerHeaders })).json()).locked).toBe(false);
  });
}
