import { startNextTestServer } from "../support/next-test-server";

export default async function globalSetup() {
  return startNextTestServer({
    baseUrlEnv: "IIP_DEPLOYED_PROFILE_BASE_URL",
    portEnv: "IIP_DEPLOYED_PROFILE_PORT",
    defaultPort: 3102,
    runtimeEnv: {
      NEXT_PUBLIC_API_BASE_URL: "http://127.0.0.1:5167",
      NEXT_PUBLIC_AUTH_MODE: "EntraId",
      NEXT_PUBLIC_RUNTIME_PROFILE: "Staging",
      NEXT_PUBLIC_TENANT_ID: "tenant-deployed",
      NEXT_PUBLIC_WORKSPACE_ID: "workspace-deployed",
      NEXT_PUBLIC_LOCAL_USER_ID: "",
    },
  });
}
