# Release Notes Template

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | Release Notes Template |
| Version | 1.0 |
| Status | Draft |
| Audience | Product, Engineering, QA, Support, Client Success, Client Stakeholders |
| Date | 2026-06-30 |


## 1. Purpose

This template standardizes release communication for IIP across internal teams and client stakeholders. It should be reused for demo, staging, UAT, pilot, and production releases.

This document is part of the deployment, release, and client enablement documentation set for Insurance Intelligence Platform (IIP). IIP is an Azure-first, human-reviewed insurance operations platform for claims, documents, channels, customer operations, approvals, auditability, and AI-assisted decision support. Customers interact through external channels such as WhatsApp, email, web forms, SMS, and broker/call-center routes; internal insurer staff use the IIP staff workspace. The MVP is centered on motor claims and document intelligence, with AI assisting through summarization, extraction, classification, routing, and evidence preparation while humans retain authority over consequential insurance decisions.

The expected technical foundation is Next.js/React/TypeScript/Tailwind on the frontend, .NET 8/ASP.NET Core on the backend, Azure SQL, Azure Blob Storage, Azure Key Vault, Azure AI Search, Azure OpenAI, Azure AI Document Intelligence, Azure Service Bus/Event Grid, Azure Functions or Durable Functions, Application Insights/Log Analytics, Microsoft Entra ID/MSAL, and Microsoft Graph where configured. Deployment options include an Otic-hosted SaaS path and a customer-tenant or Azure Managed Application path. Production and pilot environments must not rely on fake endpoints or mock production data; unconfigured providers must show clear Not configured states.

## 2. Release Header

| Field | Value |
|---|---|
| Release Version |  |
| Release Name |  |
| Environment | Dev / Staging / UAT / Pilot / Production |
| Release Date |  |
| Release Owner |  |
| Prepared By |  |
| Approved By |  |
| Related Change Request(s) |  |

## 3. Executive Summary

Provide a short summary of what changed, why it matters, and who is affected.

## 4. New Features

| Feature | Description | Impacted Roles | Notes |
|---|---|---|---|
|  |  |  |  |

## 5. Changed Behavior

| Area | Previous Behavior | New Behavior | User Impact |
|---|---|---|---|
|  |  |  |  |

## 6. Fixed Issues

| Issue ID | Summary | Severity | Resolution |
|---|---|---|---|
|  |  |  |  |

## 7. Security, Compliance, and AI Safety Notes

| Area | Note | User Action Required |
|---|---|---|
| Security |  |  |
| Compliance |  |  |
| AI behavior |  |  |
| Data handling |  |  |

## 8. Known Issues

| Issue | Impact | Workaround | Target Resolution |
|---|---|---|---|
|  |  |  |  |

## 9. Deployment Notes

| Item | Detail |
|---|---|
| Deployment window |  |
| Expected downtime |  |
| Rollback plan |  |
| Post-deployment validation owner |  |
| Support contact |  |

## 10. Client Action Required

| Action | Owner | Deadline |
|---|---|---|
|  |  |  |

## 11. Approval

| Approver | Role | Decision | Date |
|---|---|---|---|
|  |  | Approved / Not Approved |  |


## Assumptions and Open Questions

| Type | Item | Action Required |
|---|---|---|
| Assumption | The pilot MVP is focused on motor claims, document intelligence, internal staff workflows, and human-reviewed AI support. | Confirm during pilot kickoff. |
| Assumption | Azure is the preferred hosting foundation, with SaaS and Managed Application deployment paths both preserved until commercial packaging is finalized. | Confirm per client. |
| Open Question | Final Azure region, private networking requirements, and customer data residency constraints may vary by client. | Confirm with the insurer and implementation team. |
| Open Question | Exact client support contacts, escalation contacts, and approval signatories are not yet fixed. | Capture in the Client Configuration Workbook or pilot kickoff pack. |
| Requires Confirmation | Any pricing, SLA, or regulatory commitment must be confirmed by business, legal, security, and delivery leads before client use. | Review before external distribution. |

