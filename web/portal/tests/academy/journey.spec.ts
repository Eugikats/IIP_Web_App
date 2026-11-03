import { expect, test, type APIRequestContext, type Page } from "@playwright/test";
import fs from "node:fs";
import path from "node:path";

// One learner takes Course 1 from enrolment to a verified certificate, in a real browser against the offline API.
// The browser parts are what a person does; the long middle (every unit quiz and module assessment) goes through the API
// with the answers read from the course content, so the journey stays quick. Restart the offline API before each spec and viewport to reset learner state.

const API = "http://localhost:5167/api/v1/academy";
const HEADERS = { "X-Tenant-Id": "tenant-demo", "X-Workspace-Id": "workspace-operations", "x-demo-role": "Admin", "x-demo-user-id": "user-1", "X-User-Id": "user-1" };

type Q = { id: string; q: string; options: string[]; answer: number };
type Assessment = { id: string; questions: Q[] };
type Lesson = { id: string; quiz: Assessment | null };
type Course = {
  id: string;
  modules: { id: string; lessons: Lesson[]; assessment: Assessment | null }[];
  finalTask: { canvasBoxes: string[]; minWords: number; rubric: { criterion: string; marks: number }[] };
};

const content = JSON.parse(fs.readFileSync(path.resolve(__dirname, "../../../../src/InsuranceIntelligence.Api/Academy/Content/academy-content.json"), "utf8")) as { courses: Course[] };
const course = content.courses.find((item) => item.id === "ai-agentic-insurance")!;
const allQuestions = new Map<string, Q>();
for (const part of course.modules) {
  for (const lesson of part.lessons) lesson.quiz?.questions.forEach((question) => allQuestions.set(question.q, question));
  part.assessment?.questions.forEach((question) => allQuestions.set(question.q, question));
}

async function answerInBrowser(page: Page) {
  const cards = page.getByTestId("question");
  const total = await cards.count();
  for (let index = 0; index < total; index++) {
    const card = cards.nth(index);
    const text = (await card.locator(".qq").innerText()).replace(/^\d+\s*/, "").trim();
    const question = allQuestions.get(text);
    expect(question, `question text: ${text}`).toBeTruthy();
    await card.getByRole("radio").filter({ hasText: question!.options[question!.answer] }).first().click();
  }
}

async function api(request: APIRequestContext, method: "get" | "post" | "put", url: string, data?: unknown) {
  const response = await request[method](`${API}${url}`, { headers: HEADERS, data: data ?? {} });
  const body = await response.json().catch(() => ({}));
  return { status: response.status(), body };
}

async function passAssessment(request: APIRequestContext, assessment: Assessment) {
  const answers = Object.fromEntries(assessment.questions.map((question) => [question.id, question.answer]));
  const result = await api(request, "post", `/courses/${course.id}/assessments/${assessment.id}/submit`, { answers });
  expect(result.status, JSON.stringify(result.body)).toBe(200);
  expect(result.body.passed).toBe(true);
}

test.describe("learner journey", () => {
  test("enrol, take a quiz, edit the profile, finish the course, earn and verify a certificate", async ({ page, request }) => {
    // Enrol from the course page.
    await page.goto(`/academy/courses/${course.id}`);
    await expect(page.getByTestId("reviewer-edition")).toBeVisible();
    const access = await request.post(`${API}/operator/enrol`, { headers: HEADERS, data: { courseId: course.id, userId: "user-1" } });
    expect(access.ok()).toBeTruthy();
    await page.reload();
    await page.getByTestId("continue-course").click();

    // The first lesson opens, and finishing it needs its quiz.
    await expect(page.getByRole("heading", { level: 1 })).toContainText("What AI, machine learning");
    await expect(page.getByTestId("complete-lesson")).toHaveCount(0);
    await page.getByTestId("take-quiz").click();
    await expect(page.getByTestId("question")).toHaveCount(10);
    await expect(page.getByTestId("submit-assessment")).toBeDisabled();

    // A wrong first try is marked and can be retaken.
    const cards = page.getByTestId("question");
    for (let index = 0; index < 10; index++) {
      const text = (await cards.nth(index).locator(".qq").innerText()).replace(/^\d+\s*/, "").trim();
      const question = allQuestions.get(text)!;
      const wrong = question.options.find((_, position) => position !== question.answer)!;
      await cards.nth(index).getByRole("radio").filter({ hasText: wrong }).first().click();
    }
    await page.getByTestId("submit-assessment").click();
    await expect(page.getByTestId("assessment-result")).toContainText("Not passed yet");
    await page.getByTestId("assessment-retake").click();
    await expect(page.getByTestId("question")).toHaveCount(10);

    // A right second try passes, completes the lesson and moves on.
    await answerInBrowser(page);
    await page.getByTestId("submit-assessment").click();
    await expect(page.getByTestId("assessment-result")).toContainText("You passed");
    await page.getByTestId("assessment-continue").click();
    await expect(page.getByRole("heading", { level: 1 })).toContainText("How large language models work");

    // A note is saved and comes back.
    await page.getByLabel("Your note for this lesson").fill("Remember the human gate.");
    await page.getByRole("button", { name: "Save note" }).click();
    await page.reload();
    await expect(page.getByLabel("Your note for this lesson")).toHaveValue("Remember the human gate.");

    // The profile can be changed and the change shows on the profile page.
    await page.goto("/academy/settings");
    await page.getByLabel("Full name").fill("Aisha Namutebi");
    await page.getByLabel("Job title").fill("Claims Lead");
    await page.getByLabel("Organisation").fill("Equator General");
    await page.getByLabel("About you").fill("Learning how to use AI safely in claims.");
    await page.getByLabel("Daily goal").selectOption("45");
    await page.locator("label.tog", { has: page.getByLabel("Weekly progress summary") }).click();
    await page.getByTestId("save-settings").click();
    await expect(page.locator("#toast")).toContainText("Settings saved");
    await page.goto("/academy/profile");
    await expect(page.getByRole("heading", { name: "Aisha Namutebi" })).toBeVisible();
    await expect(page.getByText("Claims Lead, Equator General")).toBeVisible();
    await expect(page.getByText("Learning how to use AI safely in claims.")).toBeVisible();
    await page.goto("/academy/settings");
    await expect(page.getByLabel("Daily goal")).toHaveValue("45");
    await expect(page.getByLabel("Weekly progress summary")).not.toBeChecked();

    // Notifications: the enrolment is there, unread, and can be marked read.
    await page.goto("/academy/notifications");
    await expect(page.getByText("You are enrolled in")).toBeVisible();
    await page.getByTestId("read-all").click();
    await expect(page.getByTestId("read-all")).toHaveCount(0);

    // The rest of the course goes through the API: every unit quiz and module assessment.
    for (const part of course.modules) {
      for (const lesson of part.lessons) await passAssessment(request, lesson.quiz!);
      await passAssessment(request, part.assessment!);
    }
    const progress = await api(request, "get", "/learner");
    expect(progress.body.certificates).toHaveLength(0);

    // The final task: submit in the browser, get marked by an operator, certificate follows.
    await page.goto(`/academy/final/${course.id}`);
    await expect(page.getByTestId("final-locked")).toHaveCount(0);
    const boxes = page.locator("textarea[aria-label^='Canvas box']");
    await expect(boxes).toHaveCount(course.finalTask.canvasBoxes.length);
    await page.getByTestId("submit-final").click();
    await expect(page.locator(".notice[role=alert]")).toContainText("Fill every box");
    for (let index = 0; index < course.finalTask.canvasBoxes.length; index++) await boxes.nth(index).fill(`Answer ${index + 1}: a specific, plain answer.`);
    await page.getByLabel("Proposal").fill("short");
    await page.getByTestId("submit-final").click();
    await expect(page.locator(".notice[role=alert]")).toContainText("needs at least");
    await page.getByLabel("Proposal").fill(Array.from({ length: course.finalTask.minWords + 20 }, (_, index) => `word${index}`).join(" "));
    await page.getByTestId("submit-final").click();
    await expect(page.getByTestId("final-submitted")).toBeVisible();

    // The Academy team marks it on their own screen.
    await page.goto("/academy/admin");
    await expect(page.getByTestId("submissions")).toContainText("Aisha Namutebi");
    await page.getByTestId("submissions").getByRole("button", { name: "Open" }).click();
    await page.getByTestId("save-marks").click();
    await expect(page.locator("#toast")).toContainText("Give");
    for (const row of course.finalTask.rubric) await page.getByLabel(`Marks for ${row.criterion}`).fill(String(row.marks));
    await page.getByLabel("Feedback for the learner").fill("Strong, specific work.");
    await page.getByTestId("save-marks").click();
    await expect(page.locator("#toast")).toContainText("passed");

    await page.goto(`/academy/final/${course.id}`);
    await page.reload();
    await expect(page.getByTestId("final-marked")).toContainText("You passed the final task");

    // The certificate: listed, shown with the learner's name, and verifiable by its public code.
    await page.goto("/academy/certificates");
    await expect(page.getByTestId("certificate-card")).toHaveCount(1);
    await page.getByRole("link", { name: "View certificate" }).click();
    await expect(page.getByTestId("certificate")).toContainText("Aisha Namutebi");
    await expect(page.getByTestId("certificate")).toContainText(/IIPA-[A-Z0-9]{4}-[A-Z0-9]{4}-[A-Z0-9]{4}/);
    const code = (await page.getByTestId("certificate").innerText()).match(/IIPA-[A-Z0-9]{4}-[A-Z0-9]{4}-[A-Z0-9]{4}/)![0];

    await page.goto(`/academy/verify/${code}`);
    await expect(page.getByTestId("verify-valid")).toContainText("Aisha Namutebi");
    await page.goto("/academy/verify/IIPA-AAAA-BBBB-CCCC");
    await expect(page.getByTestId("verify-invalid")).toBeVisible();
  });
});

test.describe("every screen fits every width", () => {
  const SCREENS = ["/academy", "/academy/courses", `/academy/courses/${course.id}`, "/academy/profile", "/academy/progress", "/academy/settings", "/academy/notifications", "/academy/certificates", "/academy/verify", "/academy/does-not-exist"];
  for (const url of SCREENS) {
    test(`no sideways scroll on ${url}`, async ({ page }) => {
      await page.goto(url);
      await page.waitForTimeout(1200);
      const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
      expect(overflow).toBeLessThanOrEqual(1);
    });
  }
});
