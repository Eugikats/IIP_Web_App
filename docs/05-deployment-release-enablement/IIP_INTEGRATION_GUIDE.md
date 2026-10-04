# IIP integration guide for an insurer's own systems

How an insurer's core system, finance tools or reporting connect to IIP without a custom connector. Three doors, all plain and documented: policies in, claims out, and events pushed to you as they happen. A fourth, API keys, lets your own systems call IIP safely.

## 1. API keys (your systems calling IIP)

An organisation administrator creates a key by calling the API as a signed-in administrator.

- `POST /api/v1/organisation/api-keys` with `{ "name": "Core system", "expiresInDays": 365 }` returns the key **once**. Only a hash is stored, so a lost key cannot be recovered: revoke it and make another.
- `GET /api/v1/organisation/api-keys` lists keys (name, first characters, created, last used, expiry, revoked). It never shows a key.
- `DELETE /api/v1/organisation/api-keys/{keyId}` revokes a key at once.
- An organisation can have 10 active keys.

Send the key on every call: `X-Api-Key: iip_...`. A key acts only inside the organisation it was made in, whatever other headers say. It has the narrow Integration role: file claims, upload documents, read claims and customers, take the claims register. It cannot change settings, people, connections or other keys, and it cannot create organisations or buy subscriptions. Treat it like a password: keep it in your secret store, not in source code.

API keys are accepted when the platform runs in multi-tenant mode with Entra External ID sign-in.

## 2. Policies in

Upload the policy export your core system already produces (CSV or spreadsheet) through the policy import: `POST /api/v1/policy-import/parse`, review, then `POST /api/v1/policy-import/commit`. Columns are matched by the names insurers normally use, ignoring case, spaces and punctuation, so most exports need no mapping. Examples:

| IIP field | Recognised headers (examples) |
|---|---|
| policyNumber | Policy No, POLICY_NO, Pol. No, Policy Ref, Certificate No |
| productLine | Class of Business, LOB, Product, Sub-Class |
| coverType | Cover, Coverage, Plan |
| customerId | Client Code, Insured ID, Member No |
| branchId | Branch, Branch Code, Agency |
| effectiveFrom | Inception Date, Start Date, Commencement Date |
| effectiveTo | Expiry Date, Renewal Date, End Date |
| annualPremium | Gross Premium, GWP, Premium |
| premiumCurrency | Currency, CCY |
| sumInsured | Sum Assured, TSI, Limit |

A header that is not recognised is left for the person to choose in the screen. Nothing is guessed silently.

## 3. Claims out: the claims register

`GET /api/v1/exports/claims-register?from=2026-01-01&to=2026-03-31&status=Settled` returns a CSV (UTF-8 with byte order mark so Excel reads names correctly). `from`, `to` (date reported, `yyyy-MM-dd`) and `status` are optional. `GET /api/v1/exports/claims-register/columns` lists the columns without data.

Columns, in order. They are a stable contract: new columns are only ever added at the end.

`claim_reference, claim_id, policy_number, product_line, claim_type, status, channel, date_of_loss, date_reported, date_last_updated, claimed_amount, risk_level, priority, assigned_team, customer_name, customer_phone, customer_email, sla_due`

Every export is recorded in the audit trail with the row count. Cells that a spreadsheet would run as a formula are made inert.

## 4. Events pushed to you (signed webhooks)

Create a subscription as an administrator: `POST /api/v1/organisation/webhooks` with `{ "url": "https://...", "eventTypes": ["claim.created"] }` (use `"*"` for all). The response shows the signing secret **once**. `GET /api/v1/events/catalogue` lists events. Other calls: list, `DELETE /api/v1/organisation/webhooks/{id}`, `POST .../test` to send a ping, `GET .../deliveries` for the delivery log.

Events: `claim.created`, `claim.status_changed`, `claim.assigned`, `claim.decision_recorded` (a human approved, rejected or overrode), `claim.document_added`. Only these leave IIP; everything else stays internal.

Your URL must be `https` and publicly reachable; addresses that resolve to private or internal ranges are refused. A failed delivery is retried with growing delays, and the log shows each attempt.

Each call carries:

- `X-IIP-Event`: the event type
- `X-IIP-Delivery`: a unique delivery id (use it to ignore repeats)
- `X-IIP-Timestamp`: unix seconds
- `X-IIP-Signature`: `sha256=` followed by the hex HMAC-SHA256, keyed with your secret, of the text `"{timestamp}.{raw body}"`

Verify it and reject anything older than five minutes.

Python:

```python
import hmac, hashlib, time

def verify(secret: str, timestamp: str, body: bytes, signature: str) -> bool:
    if abs(time.time() - int(timestamp)) > 300:
        return False
    expected = "sha256=" + hmac.new(secret.encode(), f"{timestamp}.".encode() + body, hashlib.sha256).hexdigest()
    return hmac.compare_digest(expected, signature.strip().lower())
```

C#:

```csharp
static bool Verify(string secret, long timestamp, string body, string signature)
{
    if (Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - timestamp) > 300) return false;
    var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}.{body}"));
    var expected = "sha256=" + Convert.ToHexString(mac).ToLowerInvariant();
    return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(signature.Trim().ToLowerInvariant()));
}
```

## What is not here yet

Direct adapters to specific core systems (for example TurnQuest or Premia) are not built; they need access to those systems and a sample of their data. Until then the doors above work with any system that can produce a spreadsheet or receive a web call.
