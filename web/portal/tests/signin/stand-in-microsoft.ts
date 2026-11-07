import type { Page, Route } from "@playwright/test";

// A stand-in for Microsoft's sign-in service and for the IIP API, answered inside the browser. MSAL, the real library
// the portal uses, runs unchanged: it discovers the authority, is sent to the authorize address, comes back with a
// code, trades it for tokens and asks the API who the person is. Only the far end is pretend.

const TENANT = "11111111-1111-1111-1111-111111111111";
const CLIENT = "22222222-2222-2222-2222-222222222222";
const AUTHORITY = `https://login.microsoftonline.com/${TENANT}`;

function base64Url(value: string): string {
  return Buffer.from(value).toString("base64").replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}

function unsignedJwt(claims: Record<string, unknown>): string {
  return `${base64Url(JSON.stringify({ alg: "none", typ: "JWT" }))}.${base64Url(JSON.stringify(claims))}.sig`;
}

export type StandIn = { apiCalls: string[]; authorizeVisits: number; organisationsAskedFor: string[] };

export async function installStandIn(page: Page, options: { memberOfNoOrganisation?: boolean; memberOfSeveralOrganisations?: boolean } = {}): Promise<StandIn> {
  const seen: StandIn = { apiCalls: [], authorizeVisits: 0, organisationsAskedFor: [] };
  let nonce = "";

  await page.route(`${AUTHORITY}/v2.0/.well-known/openid-configuration`, (route) =>
    route.fulfill({
      json: {
        authorization_endpoint: `${AUTHORITY}/oauth2/v2.0/authorize`,
        token_endpoint: `${AUTHORITY}/oauth2/v2.0/token`,
        end_session_endpoint: `${AUTHORITY}/oauth2/v2.0/logout`,
        issuer: `${AUTHORITY}/v2.0`,
        jwks_uri: `${AUTHORITY}/discovery/v2.0/keys`,
        tenant_region_scope: "NA",
      },
    }));
  await page.route("https://login.microsoftonline.com/common/discovery/instance**", (route) =>
    route.fulfill({ json: { tenant_discovery_endpoint: `${AUTHORITY}/v2.0/.well-known/openid-configuration`, metadata: [] } }));

  await page.route(`${AUTHORITY}/oauth2/v2.0/authorize**`, async (route: Route) => {
    seen.authorizeVisits++;
    const url = new URL(route.request().url());
    nonce = url.searchParams.get("nonce") ?? "";
    const redirect = url.searchParams.get("redirect_uri") ?? "";
    const state = url.searchParams.get("state") ?? "";
    await route.fulfill({
      status: 302,
      headers: { location: `${redirect}#code=stand-in-code&state=${encodeURIComponent(state)}&client_info=${base64Url(JSON.stringify({ uid: "user-1", utid: TENANT }))}` },
    });
  });

  await page.route(`${AUTHORITY}/oauth2/v2.0/token**`, (route) => {
    const now = Math.floor(Date.now() / 1000);
    return route.fulfill({
      json: {
        token_type: "Bearer",
        scope: "api://33333333-3333-3333-3333-333333333333/access_as_user",
        expires_in: 3600,
        ext_expires_in: 3600,
        access_token: unsignedJwt({ aud: "api://33333333-3333-3333-3333-333333333333", iss: `${AUTHORITY}/v2.0`, exp: now + 3600, scp: "access_as_user" }),
        refresh_token: "stand-in-refresh",
        client_info: base64Url(JSON.stringify({ uid: "user-1", utid: TENANT })),
        id_token: unsignedJwt({
          aud: CLIENT, iss: `${AUTHORITY}/v2.0`, iat: now, nbf: now, exp: now + 3600, sub: "user-1", oid: "user-1", tid: TENANT,
          name: "Test Administrator", preferred_username: "admin@example.test", nonce, ver: "2.0",
        }),
      },
    });
  });

  // The IIP API, answered with what a signed-in administrator would see.
  await page.route("http://localhost:5199/**", async (route) => {
    const request = route.request();
    const cors = {
      "access-control-allow-origin": request.headers().origin ?? "*",
      "access-control-allow-headers": "*",
      "access-control-allow-methods": "GET,POST,PUT,DELETE,OPTIONS",
      "access-control-allow-credentials": "true",
    };
    if (request.method() === "OPTIONS") return route.fulfill({ status: 204, headers: cors });

    const path = new URL(request.url()).pathname;
    seen.apiCalls.push(`${request.method()} ${path}`);
    // A person who belongs to several organisations is refused until the request says which one, as the real API does.
    const askedFor = request.headers()["x-organisation-id"];
    if (path === "/api/v1/me" && options.memberOfSeveralOrganisations && !askedFor) {
      return route.fulfill({ headers: cors, json: { title: "no single membership" }, status: 403 });
    }
    if (path === "/api/v1/me") {
      seen.organisationsAskedFor.push(askedFor ?? "");
      return route.fulfill({
        headers: cors,
        json: options.memberOfNoOrganisation
          ? { title: "no membership" }
          : {
              userId: "user-1", displayName: "Test Administrator", role: "Admin", roleLabel: "Workspace Administrator",
              permissions: ["ViewCommandCenter", "ViewAllClaims", "ViewAssignedClaims", "ManageTenantSettings", "ViewChannels", "ConfigureIntegrations", "ManageKnowledgeBase"],
              tenantId: "tenant-a", workspaceId: "workspace-a", source: "Entra",
              environment: { name: "Demo", reviewMode: "Demo", syntheticData: true, dataClassification: "Synthetic" },
              availableWorkspaces: [{ workspaceId: "workspace-a", name: "Workspace A" }],
            },
        status: options.memberOfNoOrganisation ? 403 : 200,
      });
    }

    if (path === "/api/v1/me/organisations") {
      return route.fulfill({
        headers: cors,
        json: options.memberOfSeveralOrganisations
          ? [
              { organisationId: "org-bank", name: "Mavuno Bank", type: "Insurer", workspaceId: "workspace-bank", roles: ["Admin"], access: "Member" },
              { organisationId: "org-home", name: "Otic Group", type: "Insurer", workspaceId: "workspace-home", roles: ["Admin"], access: "Member" },
            ]
          : [],
      });
    }
    if (path === "/api/v1/security/mfa/status") {
      return route.fulfill({ headers: cors, json: { policyRequired: false, enrolledMethods: [], shouldPrompt: false, mustVerify: false, offeredMethods: [], credentials: [] } });
    }

    // Screens that list things get an empty list in the shape each expects.
    if (path.startsWith("/api/v1/command-center/conversations")) return route.fulfill({ headers: cors, json: { items: [] } });
    if (path === "/api/v1/provider-status") return route.fulfill({ headers: cors, json: { providers: [], summary: { total: 0, needAttention: 0 } } });
    return route.fulfill({ headers: cors, json: {} });
  });

  return seen;
}
