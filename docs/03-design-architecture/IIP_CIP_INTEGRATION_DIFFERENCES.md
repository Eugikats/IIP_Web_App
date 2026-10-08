# IIP and CIP: how integrations differ

Written 2026-10-09 from the code in `Projects/iip_saas` (IIP) and `Projects/CIP_Web_App/apps/credit-api` (CIP, commit 1a4be27). It records what CIP connects to that IIP does not, what IIP has that CIP does not, and how CIP built each piece, so IIP can copy the proven design rather than invent one. Build order is at the end.

## 1. The two models

| | CIP | IIP |
|---|---|---|
| Organisation-level credentials | `TenantSettings` rows by category (whatsapp, teams, outlook, powerbi...), secrets as masked references resolved by `IntegrationSecretResolver` | `IntegrationConnection` per tenant and type: validated against the real provider before saving, encrypted with Data Protection, write-only from the browser (`ConnectionEndpoints`, `WhatsAppConfigurationEndpoints`) |
| Per-person connections | Yes: `UserConnectionService` lets each person connect their own Microsoft (work, school or personal) or Google account by OAuth; tokens in `UserOAuthConnectionStore`, refreshed by `UserTokenProvider` | None. Every connection belongs to the organisation. |
| Overview screen | `IntegrationCenterService` groups everything by category with honest status per item | `/app/integrations` and provider status endpoints |

IIP's organisation model is stricter and stays. What IIP lacks is CIP's second layer: a person's own Drive or OneDrive, used to pick files and to save exports. That layer is the prerequisite for most of the gaps below.

## 2. Integrations CIP has and IIP lacks

| Integration | What CIP does | How CIP built it | IIP today |
|---|---|---|---|
| **Google Drive (per person)** | Pick files with Google's own picker; ingest them as documents; save exports into Drive | `MeConnectionsController` (`/api/v1/me/connections/{provider}/start`, `/exchange`, `google/picker`), scope `drive.file` only (only files the person picks or CIP creates), `GoogleDriveFileDiscoveryService`, `ConnectorDocumentIngestionService` | Not connected. Import page shows Drive as "Not connected". |
| **Google Drive (organisation folder)** | Watch a shared folder and pull new files in | `GoogleDriveConnectorService`, `GoogleDriveNotificationsController` (push notifications), `ScheduledSyncService` | None |
| **OneDrive and SharePoint** | Pick files; browse SharePoint sites and libraries; save exports to a chosen folder | Microsoft Graph with the person's token; SharePoint uses `Sites.Selected` (admin consents per site); `GraphFileDiscoveryService`, `CloudExportController` pickers (`cloud/pickers/sharepoint/sites`, `.../libraries`, `onedrive/drives`, `.../folders`) | Graph is used for mail only (`GraphMailProvider`). No file access. |
| **Cloud export** | "Save to Drive / OneDrive / SharePoint" from any report or chart, with a folder picker | `CloudExportService`, `CloudExportDialog` and `ExportMenu` in the web app | Exports download to the computer only. |
| **Gmail** | Read a mailbox for documents | `GmailIntegrationService` | Outlook through Graph only. |
| **Teams bot** | Two-way: notifications with deep links, approvals from Teams, a bot people can ask | `Services/Teams/*` (bot connector, inbound, channel mapping, redaction, app installer), `teams-app/` manifest | One-way incoming-webhook notifications (`IncomingWebhookTeamsProvider`, adaptive cards). |
| **Power BI** | Embedded reports with row-level security | `powerbi` settings, embed mode | None |
| **Web chat widget** | A chat bubble for the bank's website, feeding the same conversation inbox | `Services/WebChat/*` | None |
| **Credit bureau** | CRB checks on applicants | `Services/Crb/*` | Not relevant to insurance; the equivalent for IIP would be an insurance claims database (for example IRA's), not built. |
| **Core system import** | Core banking export import | `CoreBankingImportService`, `DataImportService` | Policy system lookup by REST (`PolicySystem` connection) is read-only; no bulk import. |
| **SMS** | EgoSMS client, OTP | `Services/Sms/EgoSmsClient.cs` | SMS connection exists (`ConnectionType.Sms`). Different provider; equivalent. |

## 3. What IIP has that CIP does not

- Local-language translation and speech (Sunbird) for WhatsApp voice notes and replies.
- Outbound webhooks with signing (`SafeWebhookHttpSender`) and API keys for insurer systems.
- Microsoft Marketplace fulfilment (SaaS subscriptions) and the Managed Application edition.
- Policy administration system lookup behind `IPolicyLookupProvider`.

## 4. What to build in IIP, in order

1. **Customer list import, end to end (no OAuth needed).** `POST /api/v1/sales-leads/import` taking the mapped rows from the existing import page; creates prospects marked "from list", keeps consent as recorded, refuses rows without name and phone, de-duplicates by phone, audits the import, returns created, skipped and why. Excel (.xlsx) reading added on the server. The page's disabled Import button becomes real.
2. **Per-person connections, copied from CIP's `UserConnectionService`.** Same endpoints (`/api/v1/me/connections/...`), same minimal scopes (`drive.file` for Google; `Files.ReadWrite` for OneDrive; `Sites.Selected` for SharePoint with admin consent), tokens encrypted with IIP's existing Data Protection key ring. Needs: a Google Cloud OAuth client for Drive (the owner is creating it) and Graph delegated permissions added to the IIP portal app registration.
3. **Pick from Drive or OneDrive** on the import page and on a claim's documents, using Google Picker and Graph file browsing.
4. **Save to Drive, OneDrive or SharePoint** from the Command Center export menu, copying `CloudExportDialog` and `CloudExportService`.
5. **Organisation folder sync** (a watched Drive folder or SharePoint library feeding documents into claims), once 2 to 4 are proven.
6. Teams two-way bot, web chat widget and Power BI: after launch, on customer demand.

Until each item is real, its button says "Not connected" with the reason, as the import page does today. Nothing pretends to work.
