import { defineConfig } from "@playwright/test";

export default defineConfig({
  testDir: ".",
  testMatch: ["documentScan.spec.ts", "slowConnection.spec.ts"],
  workers: 1,
  reporter: "list",
});
