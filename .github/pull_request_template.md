## Description
Please include a summary of the changes and the related issue/feature. Mention any dependencies that are required for this change.

Fixes / Closes # (issue number)

## Type of Change
- [ ] `feat:` New feature (non-breaking change which adds functionality)
- [ ] `fix:` Bug fix (non-breaking change which fixes an issue)
- [ ] `refactor:` Code restructuring with no behavior changes
- [ ] `docs:` Documentation updates
- [ ] `test:` Adding or updating tests
- [ ] `ci:` CI/CD pipeline changes

## Enterprise & Azure Guardrails Checklist
- [ ] **No Mocks in Production**: Checked that simulated/mock providers (`InsuranceIntelligence.Infrastructure.Simulated`) are NOT registered or used in production configurations.
- [ ] **Tenant Isolation**: Verified all database queries and service calls filter by `TenantScope` (TenantId + WorkspaceId).
- [ ] **Evidence & Grounding**: Checked that AI/LLM responses include source document references/citations and confidence scores where applicable.
- [ ] **Human-in-the-Loop**: Verified that no claims denial, repudiation, or final decisions are automated without human supervisor approval.
- [ ] **No Secrets Exposed**: Assured no API keys, client secrets, or PII are committed or printed in logs (secrets fetched via Key Vault or Managed Identity).
- [ ] **Frontend Runtime Data Hygiene**: Verified runtime frontend routes do not contain hardcoded operational records, fake provider success, seeded demo business data, or simulated customer communications.
- [ ] **Customer-Safe Boundaries**: Verified customer-facing routes use dedicated customer-safe API contracts and do not expose staff DTOs, provider health, prompts, internal notes, fraud flags, reserves, or audit internals.
- [ ] **Linear Git History**: I have rebased my branch onto `main` and verified a linear history.

## Verification & Testing
### Automated Tests
- [ ] Unit tests added/updated and passing
- [ ] Integration tests added/updated and passing
- [ ] Dependency audits checked where dependencies changed: `dotnet list InsuranceIntelligence.sln package --vulnerable --include-transitive`, `pnpm audit --audit-level moderate`
- [ ] Portal static gates passing: `pnpm check:runtime-data`, `pnpm check:role-routes`, `pnpm check:ui-release-gates`
- [ ] Portal UI gates passing where affected: `pnpm build-storybook`, `pnpm test:deployed-dom`, `pnpm test:visual`

### Manual Verification
Describe the steps you took to manually verify your changes:
1. ...

## Database & Infrastructure Updates
- [ ] This PR contains EF Core migrations (SQL script generated and verified).
- [ ] This PR updates Bicep templates or Azure environment configurations.
- [ ] This PR requires new Azure environment variables / App Settings (documented in `CONTRIBUTING.md`).
