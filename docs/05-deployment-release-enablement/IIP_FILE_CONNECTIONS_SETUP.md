# IIP file connections: what to set up

Written 2026-10-09. The code for connecting Google Drive and OneDrive is built and tested. Until the two app registrations below exist, Settings and the list import page say "Not connected" and give the reason. Nothing is faked.

## What it does

Each person connects their **own** Google or Microsoft account from Settings, Integrations. IIP can then read the customer list they pick (CSV, Excel, or a Google Sheet) on the import page. It acts as that person only: it can open what they can open, and one colleague's connection never reaches another's files.

- Google asks only for the `drive.file` permission, so IIP sees only the files the person chooses in Google's own picker.
- Microsoft asks for `Files.ReadWrite` and `User.Read` on the person's own OneDrive.
- The refresh token is encrypted with the same key ring as the other saved connections and never reaches a browser. Disconnecting deletes it.
- Every connect, disconnect and file read is written to the audit trail (no file contents).

Saving exports back to Drive or OneDrive uses the same connection and is the next step; it is not built yet.

## One redirect address for both

`https://app.insuranceintelligenceplatform.com/app/connect/callback`

For local testing, also add `http://localhost:3000/app/connect/callback`.

## 1. Google (needed for Google Drive)

In Google Cloud Console, in the project you create for IIP:

1. Enable **Google Drive API** and **Google Picker API**.
2. OAuth consent screen: app name "Insurance Intelligence Platform", user type External, add the scopes `openid`, `email`, `.../auth/drive.file`. Add the domain `insuranceintelligenceplatform.com` as an authorised domain. (`drive.file` is a non-sensitive scope, so Google does not require a security review for it.)
3. Credentials, create an **OAuth client ID**, type **Web application**, with the redirect address above.
4. Credentials, create an **API key** (this is the picker's developer key). Restrict it to the Picker API and to the HTTP referrer `https://app.insuranceintelligenceplatform.com/*`.
5. Note the **project number** (Dashboard). The picker shares the chosen files with this project.

## 2. Microsoft (needed for OneDrive)

In the Otic Microsoft Entra directory, create a new app registration "IIP Files":

1. Supported account types: accounts in any organisational directory and personal Microsoft accounts.
2. Platform: **Web** (not single-page), redirect address as above.
3. API permissions, delegated: `User.Read`, `Files.ReadWrite`, `offline_access`, `openid`, `email`. (`Sites.Selected` is only for SharePoint and needs an administrator's approval in each customer's directory; not needed for OneDrive.)
4. Certificates and secrets: create a client secret.

## 3. Where the values go

They are API settings on `app-api-dev-h5girbnzrayqe` (later the production API). **Put secrets in Key Vault and reference them; never paste a secret into chat, a document or the repository.**

| Setting | Value |
|---|---|
| `Files__RedirectUri` | the redirect address above |
| `Files__Google__ClientId` | the OAuth client ID |
| `Files__Google__ClientSecret` | the OAuth client secret (Key Vault reference) |
| `Files__Google__AppId` | the Google Cloud project number |
| `Files__Google__DeveloperKey` | the API key |
| `Files__Microsoft__ClientId` | the "IIP Files" application (client) ID |
| `Files__Microsoft__ClientSecret` | its client secret (Key Vault reference) |

When both a client ID and secret are present for a provider, its card in Settings offers Connect. No redeploy is needed beyond restarting the API after changing settings.

## 4. Checks once set

1. Settings, Integrations: Google Drive shows "Not connected" with a Connect button, not the "has not been set up" message.
2. Connect with a test account; the card shows "Connected" and the account address.
3. Acquisition, Import a list, Google Drive: the picker opens; choosing a sheet loads its columns.
4. Disconnect; the card returns to "Not connected".
