# Bank technical questionnaire response draft

**Status: internal draft for review.** The answers reflect the technical questionnaire and `SecurityStatements`. Deployment-dependent facts must be confirmed before use. Unanswered items remain unanswered deliberately.

| # | Question | Current answer | Status / evidence gap |
|---|---|---|---|
| 1 | Where is data stored and can it leave the country? | Current deployments may store data outside Uganda. IIP does not offer a country-residency guarantee today. | Confirm target environment's actual region. Legal review of cross-border processing is required. |
| 2 | How is one customer's data separated? | Records carry tenant and workspace identifiers. Application repositories scope each read and write, and automated tests cover isolation. | Database row-level security and independent penetration testing are not evidenced. |
| 3 | How is data encrypted and who holds keys? | Provider credentials are encrypted before storage. Azure services are described as encrypting data at rest with Microsoft-managed keys; HTTPS protects connections. | Confirm actual Azure settings and TLS policy. Customer-managed keys are not offered. Some application keys remain outside Key Vault. |
| 4 | How does bank data arrive and is there write-back? | IIP supports file and connected-folder workflows, including bank-event import. A bank data-lake integration is not established by this response. No silent write-back to core banking systems is part of the described workflow. | Confirm a specific customer's source, authentication, network path, fields and retention before design. |
| 5 | Who at IIP can access data and how is it audited? | Permissions are checked server-side. Important actions are recorded in audit events with safe metadata. | Customer-approved support access procedure and SIEM export are not available. Confirm telemetry coverage for the target deployment. |
| 6 | What does AI do and who is accountable? | AI prepares drafts, summaries and recommendations. People make consequential insurance decisions. Wording extraction retains exact source citations and requires human confirmation. | Confirm model region and current provider terms for the deployed model. AI training, retention and regulator guidance require review. |
| 7 | What certifications and independent assurance exist? | IIP does not hold ISO 27001 or SOC 2 certification according to the current security statement. | No independent penetration test is evidenced. Microsoft's certifications apply to its platform, not IIP's application. |
| 8 | How do staff sign in and how are permissions enforced? | Sign-in uses the configured identity provider. Roles and permissions are checked by the server. | Confirm the identity provider and MFA policy for this deployment. SSO beyond the configured provider and SCIM are not promised here. |
| 9 | What availability, backup and recovery commitments exist? | The environment is sized for demonstration and early pilots. IIP makes no uptime, recovery-time or recovery-point commitment. | No tested disaster recovery plan is evidenced. Production capacity and backup restoration need validation. |
| 10 | What happens on exit and which other parties process data? | An organisation administrator can export organisation-owned data and close the organisation. Azure, Meta, and configured SMS and email providers may process data for their service functions. | Confirm the sub-processor list, deletion including backups, breach terms, liability and continuity terms with counsel. |

## Required review before external use

- Confirm actual environment and deployment facts with operations.
- Have counsel review the data processing agreement, cross-border processing, WhatsApp wording, consent, breach terms and liability.
- Replace no answer with a specific answer only when a written decision or verified evidence exists.
- Keep planned controls labelled as planned and unavailable capabilities labelled as not offered.
