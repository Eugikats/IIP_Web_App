---
name: "🚀 Feature Request"
about: Propose a new feature, enhancement, or capability for the InsuranceIntelligence Platform
title: "[FEAT] "
labels: ["enhancement", "triage"]
assignees: []
---

## Problem Statement
Is your feature request related to a problem? Please describe. (e.g. "Insurers need to see the SLA breach countdown in minutes rather than hours...")

## Proposed Solution
A clear and concise description of what you want to happen.

## Alternate Approaches Considered
Describe any alternative solutions or features you've considered.

## Enterprise & Security Compliance Alignment
- **Human-in-the-Loop Supervision**: How does this feature ensure that consequential decisions (e.g. claim denials, payments, etc.) remain in human hands?
- **Tenant Isolation**: Does this introduce any new tables or queries that require logical tenant/workspace partitioning?
- **Grounding/Auditing**: Will this AI capability need to record retrieved context citations, model output, or confidence scores in the audit logs?
- **Azure Native Design**: How does this utilize or align with the Azure resources (Azure SQL, Blob Storage, Key Vault, Azure OpenAI)?
