import { defineConfig, devices } from "@playwright/test";

// The claims-core end-to-end oracle. It boots the real API on Mock providers with seeded synthetic
// data, and the portal pointed at it, then runs tests/claims-core.e2e.spec.ts. No external service
// and no credential is used. Run with: pnpm test:claims-core
const apiPort = Number(process.env.IIP_E2E_API_PORT ?? 5170);
const portalPort = Number(process.env.IIP_E2E_PORTAL_PORT ?? 3105);
const apiUrl = `http://127.0.0.1:${apiPort}`;
const portalUrl = `http://127.0.0.1:${portalPort}`;

export default defineConfig({
  testDir: "./tests",
  testMatch: "claims-core.e2e.spec.ts",
  outputDir: "./test-results/claims-core",
  timeout: 180_000,
  expect: { timeout: 20_000 },
  fullyParallel: false,
  workers: 1,
  reporter: [["list"]],
  use: { baseURL: portalUrl, trace: "retain-on-failure" },
  projects: [{ name: "desktop", use: { ...devices["Desktop Chrome"], browserName: "chromium", viewport: { width: 1280, height: 900 } } }],
  webServer: [
    {
      command: "dotnet run --no-build --no-launch-profile -c Release --project ../../src/InsuranceIntelligence.Api/InsuranceIntelligence.Api.csproj",
      url: `${apiUrl}/health`,
      reuseExistingServer: !process.env.CI,
      timeout: 180_000,
      env: {
        ASPNETCORE_ENVIRONMENT: "Development",
        ASPNETCORE_URLS: apiUrl,
        Portal__CorsOrigin: portalUrl,
        INSURANCEINTELLIGENCE_AUTH_MODE: "Development",
        INSURANCEINTELLIGENCE_STORAGE_PROVIDER: "InMemory",
        INSURANCEINTELLIGENCE_BLOB_PROVIDER: "Mock",
        INSURANCEINTELLIGENCE_AI_PROVIDER: "Mock",
        INSURANCEINTELLIGENCE_DOCUMENT_PROVIDER: "Mock",
        INSURANCEINTELLIGENCE_RETRIEVAL_PROVIDER: "Mock",
        INSURANCEINTELLIGENCE_QUEUE_PROVIDER: "Mock",
        INSURANCEINTELLIGENCE_EMAIL_PROVIDER: "Mock",
        INSURANCEINTELLIGENCE_COMMUNICATION_PROVIDER: "Mock",
        INSURANCEINTELLIGENCE_NOTIFICATION_PROVIDER: "Mock",
        INSURANCEINTELLIGENCE_SMS_PROVIDER: "Mock",
        INSURANCEINTELLIGENCE_SEED_DEMO_DATA: "true",
        INSURANCEINTELLIGENCE_LOCAL_DOCUMENT_STORAGE_PATH: "./test-results/claims-core-storage",
      },
    },
    {
      command: `pnpm dev --hostname 127.0.0.1 --port ${portalPort}`,
      url: portalUrl,
      reuseExistingServer: !process.env.CI,
      timeout: 180_000,
      env: {
        NEXT_PUBLIC_API_BASE_URL: apiUrl,
        NEXT_PUBLIC_AUTH_MODE: "Development",
        NEXT_PUBLIC_RUNTIME_PROFILE: "Development",
        NEXT_PUBLIC_TENANT_ID: "tenant-demo",
        NEXT_PUBLIC_WORKSPACE_ID: "workspace-operations",
        NEXT_PUBLIC_LOCAL_USER_ID: "local-admin",
      },
    },
  ],
});
