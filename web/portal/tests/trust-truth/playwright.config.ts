import { defineConfig } from "@playwright/test";

export default defineConfig({
  testDir: ".",
  testMatch: "synthetic-workspace.spec.ts",
  workers: 1,
  reporter: "list",
});
