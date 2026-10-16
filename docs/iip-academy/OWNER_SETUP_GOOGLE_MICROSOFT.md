# Google and Microsoft setup: a checklist you can follow

Written 2026-10-17. Do these in order. Each step says what it unlocks, where to click, and what to send back to me. **Never paste a secret into chat or into a document.** Secrets go straight into Key Vault (Part 5). You only ever send me IDs, which are not secret.

Addresses used below:

- Portal: `https://app.insuranceintelligenceplatform.com`
- API: `https://api.insuranceintelligenceplatform.com`
- Privacy policy: `https://app.insuranceintelligenceplatform.com/privacy`
- Terms: `https://app.insuranceintelligenceplatform.com/terms`
- Redirect address for file connections (the same for Google and Microsoft): `https://app.insuranceintelligenceplatform.com/app/connect/callback`

## What this unlocks

| Part | What it switches on | Needed for the Academy? |
|---|---|---|
| 1 Search Console | Proves you own the domain. Google needs it for the sign-in screen | No, but Part 2 needs it |
| 2 Google Cloud | Connecting Google Drive and picking files in the Data Hub and list imports | No |
| 3 Microsoft "IIP Files" | Connecting OneDrive and SharePoint | No |
| 4 External ID | People with Gmail or any email can sign in, not only Microsoft work accounts | **Yes, for public Academy learners** |
| 5 Key Vault | Where every secret lives | Yes, if you do any other part |

If you only do one thing for the Academy launch, do Part 4 and Part 5.

---

## Part 1. Prove you own the domain (Search Console)

You need access to wherever the DNS for `insuranceintelligenceplatform.com` is managed (the registrar or DNS host).

1. Go to https://search.google.com/search-console and sign in with the Google account that should own this (use a shared company account, not a personal one).
2. Choose **Add property**, then **Domain** (not "URL prefix"). Enter `insuranceintelligenceplatform.com`.
3. Google shows a **TXT record** (starts with `google-site-verification=`). Copy it.
4. In your DNS host, add a TXT record on the root of the domain with that value. Save.
5. Back in Search Console press **Verify**. If it says not found, wait 10 minutes and try again (DNS can be slow).

Done when: Search Console shows the property as verified.

## Part 2. Google Cloud (Google Drive)

1. Go to https://console.cloud.google.com with the same Google account. Create a project named **IIP**.
2. **APIs and services, Library**: enable **Google Drive API** and **Google Picker API**.
3. **Google Auth Platform** (older name: OAuth consent screen), set up:
   - App name: `Insurance Intelligence Platform`
   - User support email and developer contact: a company address
   - Audience: **External**
   - App domain: homepage `https://app.insuranceintelligenceplatform.com`, privacy policy and terms as listed at the top
   - Authorised domain: `insuranceintelligenceplatform.com` (this is why Part 1 comes first)
   - Scopes: `openid`, `email`, `https://www.googleapis.com/auth/drive.file`
4. Set the publishing status to **In production**. (While it says "Testing", only people you list can connect.) `drive.file` is a non-sensitive scope, so Google does not ask for a security review. If it asks for brand verification, follow its prompts.
5. **Clients**, create an **OAuth client ID**, type **Web application**, name `IIP web`. Add the authorised redirect address from the top (and `http://localhost:3000/app/connect/callback` if you test on your machine). Create it. Copy the **client ID** and **client secret**.
6. **Credentials**, create an **API key** (this is the Picker's developer key). Restrict it: API restrictions, Google Picker API only; website restrictions, `https://app.insuranceintelligenceplatform.com/*`.
7. On the project **Dashboard**, copy the **project number**.

Send me: the client ID, the project number. Do **not** send the client secret or the API key. They go into Key Vault.

## Part 3. Microsoft "IIP Files" (OneDrive and SharePoint)

1. Go to https://entra.microsoft.com, sign in as an admin of the Otic directory. **App registrations, New registration**.
2. Name: `IIP Files`. Supported account types: **Accounts in any organizational directory and personal Microsoft accounts**.
3. Redirect URI: platform **Web** (not single-page), the redirect address from the top. Register.
4. Copy the **Application (client) ID** from the Overview page.
5. **API permissions, Add a permission, Microsoft Graph, Delegated**: `User.Read`, `Files.ReadWrite`, `offline_access`, `openid`, `email`. You do not need to grant admin consent for these. (SharePoint libraries also need `Sites.Selected`, and each customer's administrator must approve it in their own directory. Skip it unless a customer asks.)
6. **Certificates and secrets, New client secret**. Set the shortest expiry you can manage, and **put a reminder in your calendar for a month before it expires**, because connections break the day it does. Copy the secret **Value** (shown once).
7. Optional but recommended: **Branding and properties**, set the logo, home page, privacy and terms addresses so the consent screen does not look anonymous. A verified publisher (needs a Microsoft Partner Network account) removes the "unverified" warning for customers; it can wait.

Send me: the Application (client) ID. Do **not** send the secret.

## Part 4. External ID: sign-in for people without a Microsoft work account

This is the one the Academy needs, because a learner might only have Gmail.

1. In the Microsoft Entra admin centre, create or open an **external tenant** (Microsoft Entra External ID). If CIP already has one, reuse it, so people have one sign-in for both products. You need to be an admin of it.
2. In that external tenant, **App registrations, New registration**, name `IIP`:
   - Account type: this organisation directory only.
   - Redirect URI: platform **Single-page application**, `https://app.insuranceintelligenceplatform.com/app`. Add `http://localhost:3000/app` for local testing.
3. **Expose an API**: set the application ID URI, add a scope named `access_as_user`. Under **Authentication**, add the portal address as a SPA redirect and tick access tokens and ID tokens if asked.
4. **User flows, New user flow**, type sign-up and sign-in. Choose these identity providers: **Email one-time passcode** and **Google**.
5. For **Google**: you need a **second Google OAuth client** (do this in the same Google Cloud project from Part 2).
   - Create an OAuth client ID, type Web application, name `IIP sign-in`.
   - Authorised redirect URIs: add the addresses Entra shows on its Google provider screen. They look like `https://<tenant-name>.ciamlogin.com/<tenant-name>.onmicrosoft.com/federation/oauth2`. Copy them exactly from Entra rather than typing them.
   - Copy its client ID and secret into the Google provider on the Entra side.
6. Add the `IIP` application to the user flow so it is used when someone signs in from the portal.
7. Collect these values: the **authority address** (looks like `https://<name>.ciamlogin.com/<name>.onmicrosoft.com`), the **application (client) ID** of `IIP`, and the **API scope** (`api://<id>/access_as_user`).

Send me: the authority address, the client ID and the scope. These are not secret. I then change the API to accept tokens from this directory and rebuild the portal with them, and the "Sign in" button offers Google and email.

## Part 5. Where secrets go (Key Vault)

The vault is `kv-iip-dev-zcxolwov` in resource group `rg-iip-dev`. Store a secret like this so the value is never shown on screen, never in your shell history and never sent to me. Open PowerShell or Git Bash, signed in with `az login`:

```
read -s -p "Paste the secret, then press Enter: " S; echo
az keyvault secret set --vault-name kv-iip-dev-zcxolwov --name "Files--Google--ClientSecret" --value "$S" --query id -o tsv
unset S
```

Use these secret names (a double dash in a name becomes a colon in the API's settings):

| Secret name | What it is |
|---|---|
| `Files--Google--ClientSecret` | Part 2, the OAuth client secret |
| `Files--Google--DeveloperKey` | Part 2, the Picker API key |
| `Files--Microsoft--ClientSecret` | Part 3, the client secret value |
| `Flutterwave--SecretKey`, `Flutterwave--SecretHash` | Payments. The secret key is from Flutterwave, Settings, API keys. The secret hash is any long random string you make up; it is only needed if you also register the webhook. Use the **test** key first |

If you get "forbidden", ask for the role **Key Vault Secrets Officer** on that vault.

Then set the matching app settings on the API app `app-api-dev-h5girbnzrayqe`. The IDs (not secret) go in as plain values. The secrets go in as Key Vault references. Replace `<version-free secret URI>` with the address the command above printed, without the trailing version number:

| Setting | Value |
|---|---|
| `Files__RedirectUri` | `https://app.insuranceintelligenceplatform.com/app/connect/callback` |
| `Files__Google__ClientId` | Part 2 client ID |
| `Files__Google__AppId` | Part 2 project number |
| `Files__Google__ClientSecret` | `@Microsoft.KeyVault(SecretUri=<version-free secret URI>)` |
| `Files__Google__DeveloperKey` | `@Microsoft.KeyVault(SecretUri=<version-free secret URI>)` |
| `Files__Microsoft__ClientId` | Part 3 client ID |
| `Files__Microsoft__ClientSecret` | `@Microsoft.KeyVault(SecretUri=<version-free secret URI>)` |
| `Flutterwave__SecretKey` | `@Microsoft.KeyVault(SecretUri=<version-free secret URI>)` |
| `Flutterwave__SecretHash` | `@Microsoft.KeyVault(SecretUri=<version-free secret URI>)` |
| `Academy__PortalBaseUrl` | `https://app.insuranceintelligenceplatform.com` |

The API's identity already reads other secrets from this vault, so it should already have permission. After saving the settings, restart the API.

If you would rather not do this yourself, tell me and I will do Part 5 once the values are in the vault. I can set app settings and restart the API, but I cannot create the Google or Microsoft registrations for you, and I will not handle the secret values.

## Checks once it is set

1. Settings, Integrations: Google Drive and OneDrive show **Not connected** with a Connect button, not "has not been set up".
2. Connect with a test account. The card shows Connected and the account address.
3. Data Hub, Connected folders: link a folder or file and press Sync now.
4. Academy: open `https://app.insuranceintelligenceplatform.com/academy` in a private window, press Sign in, and sign in with a Gmail address. You should land back in the Academy signed in.
5. Disconnect an account. It returns to Not connected.

## Still waiting on you from before

- A passkey trial on a real phone (two-factor).
- Deciding who recovers an account if the only administrator is locked out.
- Moving the encryption key ring into Key Vault (tell me when you want it done).
