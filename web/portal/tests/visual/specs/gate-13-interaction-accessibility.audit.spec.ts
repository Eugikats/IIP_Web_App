import AxeBuilder from "@axe-core/playwright";
import { expect, test, type Page, type TestInfo } from "@playwright/test";
import { installVisualApiFixtures } from "../fixtures/iip-fixtures";

type AuditResult = Record<string, unknown>;

test.beforeEach(async ({ page }) => {
  await installVisualApiFixtures(page);
});

async function attachJson(testInfo: TestInfo, name: string, value: AuditResult) {
  await testInfo.attach(name, {
    body: Buffer.from(JSON.stringify(value, null, 2)),
    contentType: "application/json",
  });
}

async function targetAudit(page: Page) {
  return page.locator("button:visible, a:visible, input:visible, textarea:visible, select:visible").evaluateAll((nodes) => {
    const targets = nodes.map((node) => {
      const rect = node.getBoundingClientRect();
      const label = node.getAttribute("aria-label") || node.textContent?.trim() || node.getAttribute("name") || node.tagName;
      return { label: label?.slice(0, 100), width: Math.round(rect.width), height: Math.round(rect.height) };
    });
    return {
      total: targets.length,
      below24: targets.filter((target) => target.width < 24 || target.height < 24),
      below44: targets.filter((target) => target.width < 44 || target.height < 44),
    };
  });
}

async function semanticsAudit(page: Page) {
  return page.locator("body").evaluate(() => ({
    dialogs: document.querySelectorAll('[role="dialog"]').length,
    alerts: document.querySelectorAll('[role="alert"]').length,
    statuses: document.querySelectorAll('[role="status"]').length,
    liveRegions: document.querySelectorAll("[aria-live]").length,
    progressbars: document.querySelectorAll('[role="progressbar"]').length,
  }));
}

test("Gate 13 records keyboard, focus-return and dialog semantics evidence", async ({ page }, testInfo) => {
  await page.goto("/app/claims/CLM-2026-00491");
  await page.waitForLoadState("networkidle");

  // Exporting the pack is a menu item under "More actions" on the decision card. The menu button is the
  // control the person came from, so it is the control that must get focus back.
  const trigger = page.getByRole("button", { name: "More actions" });
  await expect(trigger).toBeVisible({ timeout: 15_000 });
  await trigger.focus();
  await trigger.press("Enter");
  const exportItem = page.getByRole("menuitem", { name: "Export claim pack" });
  await exportItem.focus();
  await exportItem.press("Enter");

  const dialog = page.getByRole("dialog", { name: "Export claim evidence pack" });
  await expect(dialog).toBeVisible();
  await expect(page.locator('[role="dialog"] :focus')).toHaveCount(1);
  const initialFocus = await page.evaluate(() => ({
    tag: document.activeElement?.tagName,
    name: document.activeElement?.getAttribute("aria-label") || document.activeElement?.textContent?.trim().slice(0, 100),
    insideDialog: Boolean(document.activeElement?.closest('[role="dialog"]')),
  }));
  expect(initialFocus.insideDialog).toBe(true);

  const tabTrail: Array<{ tag?: string; name?: string; insideDialog: boolean }> = [];
  for (let index = 0; index < 8; index += 1) {
    await page.keyboard.press("Tab");
    tabTrail.push(await page.evaluate(() => ({
      tag: document.activeElement?.tagName,
      name: (document.activeElement?.getAttribute("aria-label") || document.activeElement?.textContent?.trim().slice(0, 100)) ?? undefined,
      insideDialog: Boolean(document.activeElement?.closest('[role="dialog"]')),
    })));
  }
  expect(tabTrail.every((entry) => entry.insideDialog)).toBe(true);

  await page.keyboard.press("Escape");
  await expect(dialog).toBeHidden();
  await expect(trigger).toBeFocused();
  const focusAfterEscape = await page.evaluate(() => ({
    tag: document.activeElement?.tagName,
    name: document.activeElement?.getAttribute("aria-label") || document.activeElement?.textContent?.trim().slice(0, 100),
  }));

  const result = { initialFocus, tabTrail, focusAfterEscape, semantics: await semanticsAudit(page) };
  console.log(`A11Y_DIALOG_AUDIT ${JSON.stringify(result)}`);
  await attachJson(testInfo, "gate-13-dialog-focus-audit.json", result);
});

test("Gate 13 records target-size, semantic and automated-rule evidence", async ({ page }, testInfo) => {
  const routes = [
    "/app/work-items",
    "/app/claims/CLM-2026-00491",
    "/app/claims/CLM-2026-00491/extraction",
  ];
  const results: AuditResult = {};

  for (const route of routes) {
    await page.goto(route);
    await page.waitForLoadState("networkidle");
    const axe = await new AxeBuilder({ page }).analyze();
    results[route] = {
      targets: await targetAudit(page),
      semantics: await semanticsAudit(page),
      axe: axe.violations.map((violation) => ({
        id: violation.id,
        impact: violation.impact,
        nodes: violation.nodes.length,
      })),
    };
  }

  console.log(`A11Y_SURFACE_AUDIT ${JSON.stringify(results)}`);
  await attachJson(testInfo, "gate-13-surface-audit.json", results);
});

test("Gate 13 records reduced-motion and visible-focus behavior", async ({ page }, testInfo) => {
  await page.emulateMedia({ reducedMotion: "reduce" });
  await page.goto("/app/claims/CLM-2026-00491");
  await page.waitForLoadState("networkidle");

  const focusTrail: Array<Record<string, unknown>> = [];
  for (let index = 0; index < 12; index += 1) {
    await page.keyboard.press("Tab");
    focusTrail.push(await page.evaluate(() => {
      const active = document.activeElement as HTMLElement | null;
      const style = active ? getComputedStyle(active) : null;
      return {
        tag: active?.tagName,
        name: active?.getAttribute("aria-label") || active?.textContent?.trim().slice(0, 100),
        outline: style?.outline,
        boxShadow: style?.boxShadow,
      };
    }));
  }

  const trigger = page.getByRole("button", { name: "More actions" });
  await expect(trigger).toBeVisible({ timeout: 15_000 });
  await trigger.click();
  await page.getByRole("menuitem", { name: "Export claim pack" }).click();
  const motion = await page.getByRole("dialog", { name: "Export claim evidence pack" }).evaluate((node) => {
    const style = getComputedStyle(node);
    return { transitionDuration: style.transitionDuration, animationDuration: style.animationDuration };
  });
  const hoverLift = await page.locator(".hover-lift").first().evaluate((node) => {
    const style = getComputedStyle(node);
    return { transitionDuration: style.transitionDuration, transform: style.transform };
  });

  const result = { focusTrail, motion, hoverLift };
  console.log(`A11Y_MOTION_FOCUS_AUDIT ${JSON.stringify(result)}`);
  await attachJson(testInfo, "gate-13-motion-focus-audit.json", result);
});
