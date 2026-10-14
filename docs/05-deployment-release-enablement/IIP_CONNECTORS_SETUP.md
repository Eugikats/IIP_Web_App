# Connected folders: how they work and what is needed to switch them on

Written 2026-10-14. Covers the folder connectors on the Data Hub (workstream W2 of the channels and integrations build plan).

## What it does

An administrator links folders in their own OneDrive or SharePoint, or files they pick in Google Drive, to the Data Hub. A background job keeps the Data Hub in step with them. Everything that arrives is a pending Knowledge document with unknown sensitivity. Nothing is approved, indexed for customers or used to answer a question until a person classifies and approves it, exactly as for a file uploaded by hand.

## How it behaves

| Situation | What happens |
|---|---|
| A new file appears in a synced folder | Brought in as a pending document, filed under the product line and kind of document chosen for that folder |
| A file changes | A new pending document is made and the old copy is archived and removed from search. An approval never carries over to different content, so the new copy needs approving |
| A file leaves a folder that read properly | Its document is archived and removed from search at once |
| The sign-in the connector reads with has expired or was revoked | The run fails as "Needs attention". Nothing is removed. The person who set it up reconnects their account |
| A folder cannot be read (moved, no access) | That folder is left exactly as it was. The other folders still sync. The run reports it |
| A folder suddenly lists as empty | Nothing is removed, because that is as likely a permissions problem as an emptied folder. To empty it on purpose, remove the folder from the connector |
| A file is an unsupported type, empty, or over 10 MB | Skipped with the reason shown. Not retried every run |
| One file cannot be downloaded | The rest still sync. It is retried next run |
| A folder is removed from the connector, or the connector is deleted | Its files leave the Data Hub immediately, not at the next run |
| An administrator pauses it | Nothing syncs until resumed, even if "Sync now" is pressed |

Reads use the owner's own sign-in, so a connector can never see more than its owner can open.

## Google Drive reads picked files, not folders

IIP asks Google only for the narrow permission that covers files a person chooses. That permission cannot list a folder's contents. CIP found the same and now syncs picked files for the same reason. So for Google, a connector entry is one picked file (PDF, Word or text). A file Google says is gone or in the bin is removed. Asking Google for the broad read permission would let a folder be synced but needs Google's restricted-scope security review, so it is a decision for the owner, not a default.

OneDrive and SharePoint have no such limit: folders, with their subfolders to eight levels and up to 5,000 files, are synced.

## What is needed to switch it on (owner)

The code is built and tested against canned provider answers. It has not run against a real Google or Microsoft account, because that needs the registrations in `IIP_FILE_CONNECTIONS_SETUP.md`:

1. A Google Cloud OAuth client, the Picker API and an API key (Google Drive).
2. The Microsoft "IIP Files" app registration with delegated `Files.ReadWrite` (OneDrive). For SharePoint libraries, `Sites.Selected` needs each customer's administrator to approve.
3. The settings from that guide on the API. Until they exist the cards and the Connect dialog say so, and nothing can be connected.

Then, as the administrator who will own the connector: connect your own account in Settings, Integrations; open the Data Hub; Connected folders, Connect folders.

## Settings

| Setting | Default | Meaning |
|---|---|---|
| `Connectors:SyncEnabled` | true | Set false to stop the schedule. "Sync now" still works |
| `Connectors:SyncIntervalMinutes` | 60 (minimum 5) | How long after its last run a connector becomes due |

The worker runs one connector at a time, starts three minutes after the app does, and checks every five minutes. On a single small plan this keeps a large folder from starving the rest of the app.

## Verified and not verified

Verified: 15 sync-engine tests (every row in the table above), 18 provider-answer tests, 15 endpoint tests (permission, validation, workspace isolation, immediate removal), 16 browser tests at 1280 and 768 wide.

Not verified: a real Google or Microsoft account; a SharePoint library; a folder of thousands of files on the live B1 plan; Microsoft's `eTag` behaving as a change marker on every file type; Google Picker's multi-select in a real browser.
