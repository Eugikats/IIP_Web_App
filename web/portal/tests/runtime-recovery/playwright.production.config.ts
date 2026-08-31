import { defineConfig, devices } from "@playwright/test";

const port = Number(process.env.IIP_RUNTIME_RECOVERY_PRODUCTION_PORT ?? 3103);
const baseURL = `http://127.0.0.1:${port}`;

export default defineConfig({
  testDir: ".",
  testMatch: "production-navigation.spec.ts",
  timeout: 90_000,
  workers: 1,
  reporter: "list",
  use: { ...devices["Desktop Chrome"], baseURL, trace: "retain-on-failure" },
  webServer: {
    command: "pnpm start",
    cwd: "../..",
    url: baseURL,
    reuseExistingServer: false,
    timeout: 120_000,
    env: {
      PORT: String(port),
      HOSTNAME: "127.0.0.1",
      NEXT_PUBLIC_API_BASE_URL: "http://127.0.0.1:5167",
      NEXT_PUBLIC_AUTH_MODE: "Development",
      NEXT_PUBLIC_RUNTIME_PROFILE: "Development",
    },
  },
});
