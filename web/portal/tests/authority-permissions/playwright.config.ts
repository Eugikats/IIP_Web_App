import { defineConfig } from "@playwright/test";

export default defineConfig({
  testDir: ".",
  testMatch: "authority-permissions.spec.ts",
  workers: 1,
  reporter: "list",
});
