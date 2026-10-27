# IIP security overview for insurer and bank discussions

**Status: internal discussion copy, not counsel-approved.** This overview reflects the current local code and the security statements shown in organisation setup. Confirm each deployment-specific fact before sharing externally.

## What IIP does

IIP is an insurance operations platform. AI can prepare summaries, drafts and recommendations. Accountable people make insurance decisions. The platform does not let AI approve or reject claims, approve payments, bind or price policies, make adverse underwriting or fraud decisions, or issue final complaint responses.

## Customer data and access

Records carry an organisation and workspace scope. Application repositories apply that scope to reads and writes, and automated tests check isolation. Database row-level security is not configured, so isolation currently depends on application enforcement. Sign-in uses the configured identity provider. Server endpoints enforce permissions; hiding a control in the portal is not treated as authorization.

## Encryption and external services

The security statement says Azure services encrypt stored data at rest with Microsoft-managed keys. Deployment settings have not been independently confirmed for this pack. Provider credentials are encrypted before storage and are not returned to the browser. Some application keys remain in application configuration; the Key Vault move is incomplete. Customer-managed keys are not offered.

WhatsApp messages pass through Meta's business platform, which receives and processes them. They are not end-to-end encrypted to IIP. The secure link is the preferred route for sensitive documents. Azure, Meta, and configured SMS and email providers may process data for their stated service functions. Confirm the current sub-processor list for the target deployment.

## Audit, retention and oversight

Important configuration, approval and customer-data actions are audited with safe metadata. Audit events do not copy message text or documents. Sending audit events to a customer's SIEM is not available. AI outputs remain drafts for people to review. Product wording drafts retain source citations and require human confirmation before a product draft is created.

Organisation administrators can export organisation-owned data and close the organisation. Closure deletes its data and cannot be undone. Confirm retention and backup deletion timing contractually; this pack does not make a backup purge-time promise.

## Current limits to disclose

- No promised data residency country. Current deployments may hold data outside Uganda.
- No database row-level security or independent penetration test is evidenced here.
- No ISO 27001 or SOC 2 certification is held by IIP.
- No uptime, recovery-time or recovery-point commitment, and no tested disaster recovery plan.
- A customer-approved support access procedure is not yet in place.
- The data processing agreement and these statements have not been reviewed by counsel.

## Before sending this to a bank

Counsel should review the data processing agreement, WhatsApp wording, consent and marketing language, cross-border processing, and liability terms. Operations should confirm the deployment region, encryption settings, identity configuration, sub-processors, retention, and the actual audit path. Do not remove the limits above unless verified evidence changes.
