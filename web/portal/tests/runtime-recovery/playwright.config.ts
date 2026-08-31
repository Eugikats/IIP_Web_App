import { defineConfig, devices } from "@playwright/test";

const port = Number(process.env.IIP_RUNTIME_RECOVERY_PORT ?? 3102);
const baseURL = `http://127.0.0.1:${port}`;

export default defineConfig({
  testDir: ".",
  testMatch: "parity-claim-detail.spec.ts",
  timeout: 90_000,
  workers: 1,
  reporter: "list",
  use: {
    ...devices["Desktop Chrome"],
    baseURL,
    trace: "retain-on-failure",
  },
  webServer: {
    command: `pnpm dev --hostname 127.0.0.1 --port ${port}`,
    cwd: "../..",
    url: baseURL,
    reuseExistingServer: false,
    timeout: 120_000,
    gracefulShutdown: { signal: "SIGINT", timeout: 5_000 },
    env: {
      NEXT_TELEMETRY_DISABLED: "1",
      NEXT_PUBLIC_API_BASE_URL: "http://127.0.0.1:5167",
      NEXT_PUBLIC_AUTH_MODE: "Development",
      NEXT_PUBLIC_RUNTIME_PROFILE: "Development",
      NEXT_PUBLIC_VISUAL_PARITY_MODE: "true",
      NEXT_PUBLIC_TENANT_ID: "tenant-runtime-recovery",
      NEXT_PUBLIC_WORKSPACE_ID: "workspace-runtime-recovery",
      NEXT_PUBLIC_LOCAL_USER_ID: "runtime-recovery-user",
    },
  },
});
