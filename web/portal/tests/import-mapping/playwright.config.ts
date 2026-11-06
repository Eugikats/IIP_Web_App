import { defineConfig } from "@playwright/test";

export default defineConfig({
  testDir: ".",
  testMatch: ["guessMapping.spec.ts"],
  workers: 1,
  reporter: "list",
});
