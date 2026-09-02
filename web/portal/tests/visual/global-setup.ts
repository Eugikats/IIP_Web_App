import { startNextTestServer } from "../support/next-test-server";

export default async function globalSetup() {
  return startNextTestServer({
    baseUrlEnv: "IIP_VISUAL_BASE_URL",
    portEnv: "IIP_VISUAL_PORT",
    defaultPort: 3100,
    runtimeEnv: {
      NEXT_PUBLIC_API_BASE_URL: "http://127.0.0.1:5167",
      NEXT_PUBLIC_AUTH_MODE: process.env.IIP_VISUAL_AUTH_MODE ?? "Development",
      NEXT_PUBLIC_RUNTIME_PROFILE: process.env.IIP_VISUAL_RUNTIME_PROFILE ?? "Development",
      NEXT_PUBLIC_TENANT_ID: "tenant-visual-fixture",
      NEXT_PUBLIC_WORKSPACE_ID: "workspace-visual-fixture",
    },
  });
}
