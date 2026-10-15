import { test } from "@playwright/test";
import path from "node:path";
import { pathToFileURL } from "node:url";

// Screenshots the design's reference file and the built page, side by side, for looking at and for the parity test.
// Run with ACADEMY_SHOTS=dashboard,courses to choose screens. Output: test-results/academy-shots/{screen}-{project}-{ref|new}.png

const REFERENCE = pathToFileURL(path.resolve(__dirname, "../../../../docs/iip-academy/design/iip-academy-reference.html")).href;
const SCREENS: Record<string, { hash: string; url: string }> = {
  dashboard: { hash: "#/dashboard", url: "/academy" },
  courses: { hash: "#/courses/all", url: "/academy/courses" },
};

for (const name of (process.env.ACADEMY_SHOTS ?? "dashboard").split(",")) {
  test(`shots ${name}`, async ({ page }, info) => {
    const screen = SCREENS[name];
    const out = (suffix: string) => `test-results/academy-shots/${name}-${info.project.name}-${suffix}.png`;
    await page.goto(`${REFERENCE}${screen.hash}`);
    await page.waitForTimeout(1500);
    await page.screenshot({ path: out("ref"), fullPage: true });
    await page.goto(screen.url);
    await page.waitForTimeout(2500);
    await page.screenshot({ path: out("new"), fullPage: true });
  });
}
