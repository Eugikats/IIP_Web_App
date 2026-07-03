# Azure Managed Application Private Plan Recommendation for IIP

This recommendation is framed to extend the current IIP architecture rather than reopen it: the existing IIP documents already fix the Bicep/IaC direction, protected runtime handling including `ManagedApplicationReviewer`, durable audit discipline, zero-simulation production rules, and the broader application/service boundary model. fileciteturn0file0 fileciteturn0file1 fileciteturn0file2 fileciteturn0file3 fileciteturn0file4 fileciteturn0file5 fileciteturn0file6

## Recommended direction

For IIP’s near-term go-to-market, the best fit is an **Azure Application offer with a Managed Application plan**, published through Partner Center and exposed initially through **private plan visibility** to named insurer customer Azure subscriptions. Keep your existing Bicep-based IaC as the engineering source of truth, but add a dedicated Marketplace packaging step that compiles Bicep to `mainTemplate.json`, keeps `createUiDefinition.json` as a separate artifact, and uploads a deployment `.zip` through Partner Center. This gives you the “customer deploys in their own subscription, publisher retains controlled management access” operating model that Managed Applications are designed for. citeturn5view2turn11view0turn13view0turn9view0

For IIP specifically, I would **not** start with a public listing and I would **not** wait for private offers. Private plans are the right initial commercial vehicle because they let you ship the same Azure Application offer only to specified customer subscription IDs, while keeping the package transactable and deployable through Marketplace. Microsoft’s current documentation also makes clear that **private offers are a different mechanism**: they are primarily for negotiated commercial terms and require the underlying offer to already be published and publicly transactable. That makes private offers a later-stage commercial tool, not the first step for your current private-plan strategy. citeturn11view0turn11view1turn21view0

My practical recommendation is therefore:

1. **Build one Azure Application offer** for IIP.
2. **Use Managed Application plan type**, not Solution Template.
3. **Ship private plans first**, targeted to specific insurer subscription IDs.
4. **Enable publisher management access**, but prefer **JIT access with Contributor** for support/operations instead of permanent Owner wherever operationally possible.
5. Treat **app authentication / insurer user onboarding** as a separate workstream from Managed Application packaging; Managed Applications solve Azure resource deployment and support access, not your Entra sign-in architecture. citeturn11view0turn13view0turn14view0

## What your package actually needs to look like

There are two different but related publication paths in Microsoft’s documentation:

- the **service catalog** path, where a managed application definition is created in Azure directly; and
- the **Marketplace / Partner Center** path, where a managed application plan is uploaded as a package in Partner Center.

For your scenario, the Marketplace path is the relevant one. Microsoft’s current Partner Center documentation for Azure managed application plans says the **deployment package is a `.zip`**, and **the root of that zip must contain**:
- `mainTemplate.json`
- `createUiDefinition.json`

Microsoft also says the package can include additional template resources, but **must not include binaries such as VM images**. Partner Center’s Technical configuration page is where you upload this package and assign a semantic package version. citeturn11view0turn13view0

Your current Bicep plan is therefore usable, but **not directly shippable**. Microsoft’s managed application docs are explicit that you can use **Bicep to author**, but for managed application publication you must **convert Bicep to ARM JSON** before publishing. They are also explicit that Azure Managed Applications currently support **ARM template `languageVersion` 1.0 only**, not 2.0. In practice, that means your delivery pipeline needs a packaging build that:
- compiles your marketplace-facing `mainTemplate.bicep` to `mainTemplate.json`,
- verifies the compiled JSON remains compatible with managed app constraints,
- retains `createUiDefinition.json` as JSON,
- zips both files at the **root** of the package,
- runs ARM Template Test Toolkit checks before upload. citeturn9view0turn5view1turn11view0

That leads to an engineering change in how I would structure your IaC repository:

- Keep your current modular Bicep tree for normal engineering and validation.
- Add a marketplace-specific top-level Bicep entrypoint, for example `main.managedapp.bicep`.
- Compile that to a single `mainTemplate.json` artifact for Partner Center upload.
- Keep `createUiDefinition.json` under source control as a first-class artifact; there is no Bicep replacement for it in the marketplace package flow. citeturn9view0turn13view0

One important nuance: Microsoft’s separate service-catalog quickstarts still describe package URIs in storage and ARM deployment of `Microsoft.Solutions/applicationDefinitions`, which is useful for understanding how managed applications work under the hood. But for **Partner Center submission**, the operative flow is the Technical configuration upload in Partner Center, not “point Partner Center at your Bicep files.” citeturn20view0turn13view0

## What changes in Managed Application mode and what does not

The core Managed Application model is that the customer deploys the solution into their own subscription, but the solution resources sit in a **managed resource group** that can be controlled by the publisher, the customer, both, or neither, depending on the permission mode you choose. Microsoft documents four permission scenarios: publisher managed, publisher-and-customer access, locked mode, and customer managed. The default is publisher managed. citeturn5view2

For Marketplace managed application plans, the current knobs that matter most to IIP are:

- **Publisher Management Access**  
  If enabled, the publisher gets access to the managed resource group in the customer tenant. This is configured by supplying the publisher Microsoft Entra tenant ID plus one or more principal IDs and role definitions. Microsoft recommends using a group where possible, and the current docs allow up to 100 authorizations per Azure region. Microsoft also states that publisher management access **cannot be changed after the plan is live**. citeturn11view0turn13view0

- **Customer Access / Deny Assignment**  
  You can either give the customer full access to the managed resource group or restrict access with deny assignments. Microsoft is explicit that deny assignments are Azure-managed and system-protected, not something you directly create yourself. Customer access settings also **cannot be changed after the offer is live**. citeturn11view0turn13view0turn14view2

- **JIT Access**  
  Microsoft supports just-in-time access for Managed Applications. With JIT enabled, the publisher has read-only visibility by default and requests time-bound elevated access for troubleshooting or maintenance, which the customer approves. That is the best fit for an insurer-facing product where “support access” must be explicit and governable. However, Microsoft documents a current limitation: the principal requesting JIT must be **explicitly included** in the managed application definition; group-only inclusion is not enough at the moment. citeturn14view0

For IIP, that means the cleanest operating model is:

- **Managed Application plan**
- **Publisher Management Access enabled**
- **Customer Access restricted with deny assignments** unless a customer explicitly insists on customer-managed mode
- **JIT enabled**
- a **dedicated support group and/or service principal** in your publisher tenant, with **Contributor** rather than Owner unless you have a documented operational need for Owner. This is a recommendation, but it follows the spirit of Microsoft’s JIT pattern and least-privilege guidance. citeturn11view0turn13view0turn14view0

The most important thing that **does not** change is your user-auth and app-authorization design. Managed Applications handle Azure resource deployment and resource-group management. They do **not** replace the need for your own Entra app registration strategy, multi-tenant sign-in model, or server-side tenant/workspace authorization. That remains a separate identity and application-layer concern. This is an inference from the current Microsoft docs, which scope Managed Application permissions around Azure subscription/resource-group access, not your application’s end-user auth model. citeturn5view2turn11view0turn13view0

## Support access, service compatibility, and practical caveats

The Managed Application support model is real, but it is narrower than many ISVs first assume.

At the **control plane** level, publisher access works exactly as Microsoft describes: your authorized principal can manage resources in the customer’s managed resource group according to the RBAC role granted in the plan. That is what enables support and operations. citeturn5view2turn11view0turn13view0

At the **data plane** level, Microsoft explicitly warns that restricting access for data operations is **not supported for all data providers in Azure**. That matters for IIP because services like Azure SQL, Blob Storage, Key Vault, AI Search, Azure OpenAI, and Document Intelligence all have important data-plane behaviors in addition to ARM control plane. In plain terms: Managed Application RBAC and deny assignments do **not automatically solve** who can read SQL data, blob contents, secrets, or service payloads. Those need to be designed separately through managed identities, service configuration, firewall/private networking, and data-plane RBAC or access policies. citeturn5view2

That is the key caveat for your planned Azure stack. Based on current Microsoft documentation, I did **not** find a Marketplace Managed Application restriction that explicitly blocks Azure SQL, Storage, Service Bus, Container Apps, Azure AI Search, Azure OpenAI, or Document Intelligence purely because they are inside a managed application. Microsoft’s guidance instead describes managed applications as deploying ordinary ARM resources via `mainTemplate.json`, and says both Azure application plan types can automate deployment of multiple Azure resources. The one explicitly documented resource caveat I found is for AKS: the AKS node resource group does not inherit the managed application deny assignment, so customers keep access there. AKS is not your current hosting target, but it is a useful warning sign that managed-resource-group behavior is not perfectly uniform across every Azure service. citeturn11view0turn5view1turn5view2

So my service-compatibility recommendation for IIP is:

- **Assume your planned stack is viable inside Managed Application packaging**, because it is all ARM/Bicep-deployable infrastructure.
- **Do not assume Managed Application permissions are enough for data-plane control**.
- **Do not assume Azure OpenAI / AI service availability or quota becomes easier because of Managed Application packaging**; those remain service-specific prerequisites and should be live-verified in the target customer subscription and region before committing a submission date. This last point is partly an inference from the way Azure services are provisioned, so I would treat it as a live-verification item. citeturn5view2turn11view0turn13view0

On public networking, I did **not** find a current Microsoft Marketplace rule that says Managed Applications must avoid public endpoints. The docs instead point to your own template choices and optional Azure Policy settings for compliance. For IIP, that means you should treat networking posture as **your design choice**, not as something Managed Applications enforce for you. If your insurer customers require private endpoints, disabled public access, or customer-managed network boundaries, those must be encoded in the template and, where appropriate, surfaced in `createUiDefinition.json`. This is an inference from current docs rather than an explicit Microsoft statement saying “public exposure is allowed,” so I would also keep it on the live-verification list. citeturn11view0turn13view0

## Private plan versus public listing and what certification really changes

For your business case, the crucial distinction is that a **private plan is a visibility setting on a plan**, not a different packaging model. Microsoft’s current Azure managed application docs say you can configure each plan as **public or private**, and private access is granted by **Azure subscription ID**, not by customer Entra tenant ID. Microsoft allows up to 10 subscription IDs manually or up to 10,000 via CSV for private plans. Microsoft also notes that private plans are **not supported** for Azure subscriptions established through a reseller in the CSP program. citeturn11view0turn13view0

That matters a lot for your onboarding flow. If you pursue private plans, your commercial/onboarding team must capture the insurer’s **target Azure subscription ID(s)** early. Knowing only the insurer’s Entra tenant or company name is not enough to target the plan. citeturn11view0turn13view0

On review and certification, I do **not** see a documented lower technical certification bar for private plans. Microsoft’s review/publish flow for Azure application offers is the normal Marketplace flow: automated validation, certification, preview creation, publisher sign-off, and publish. Their certification documentation describes business eligibility checks, content validation, and technical validation including malware scanning, network-call monitoring, package analysis, and functionality scanning. Separately, Microsoft’s Azure application planning docs state that offers must meet the general Marketplace certification policies. Because private-plan visibility is configured under the plan’s availability settings rather than through a different submission path, the safest current conclusion is that **private plans go through the same fundamental package validation and certification flow as public plans**; the difference is audience/visibility, not a relaxed technical bar. I would treat that as the operative assumption unless Microsoft Partner Center support tells you otherwise in writing. citeturn10view0turn10view3turn13view0turn15view0turn16view0turn11view3

A second distinction that matters operationally:

- **Preview audience** is for pre-live testing and review.
- **Private plan audience** is for who can see/acquire a live private plan.
- **Private offers** are for negotiated commercial deals and require a published public transactable offer underneath. citeturn10view0turn16view0turn21view0

For IIP, I would therefore use:
- **Preview audience** = your own test subscriptions plus any internal validation subscriptions.
- **Private plan audience** = named insurer customer subscription IDs.
- **Private offers** = optional later, only if a specific insurer needs custom pricing/terms beyond your standard private plan. citeturn10view0turn11view0turn13view0turn21view0

## Timeline, prerequisites, and the two parallel workstreams

The first lead-time item is **Partner Center account readiness**, not template authoring. Microsoft’s current documentation says you need a Microsoft Marketplace account in Partner Center, Marketplace program enrollment, a work account linked to your company, and authority to sign the Microsoft Publisher Agreement. Microsoft also verifies business name, address, and primary contact information when you enroll or change legal details. Microsoft’s current verification guidance says this **usually takes 3–5 business days**, and if it runs longer than five days you should contact support. Microsoft also notes that while verification is incomplete, important capabilities remain blocked, including publishing Marketplace offers and even some tax/payout profile operations. citeturn17view0turn17view1

The second lead-time item is **first-pass Marketplace certification**, where Microsoft’s docs give useful timing only for the **automated** parts. Their current review/publish guidance says some automated checks run in minutes, but they do **not** publish a fixed SLA for manual certification. The certification phase includes business, content, and technical validation, and Azure application docs warn that the manual validation step includes deep review of the technical assets, especially the ARM template. That means you should not commit a customer-facing submission/live date based on a guessed certification queue. citeturn15view0turn16view0

The practical sequencing I would use is this.

### Engineering workstream

1. Create a **marketplace packaging branch** of your Bicep design.
2. Add a new marketplace entrypoint Bicep file and compile path to `mainTemplate.json`.
3. Author `createUiDefinition.json`.
4. Build the Partner Center package zip with required files in the root.
5. Add CI checks for:
   - Bicep compile to JSON,
   - enforcement of ARM `languageVersion` 1.0 compatibility,
   - ARM Template Test Toolkit,
   - zero-simulation / protected-runtime parameter verification,
   - package artifact generation.
6. Decide your Managed Application permission posture:
   - publisher management access enabled,
   - JIT enabled,
   - customer access restricted with deny assignments unless a customer requires otherwise,
   - support principal/group and role assignments finalized.
7. Decide whether you will use **managed identity only** for runtime service access in the customer subscription or whether any service-specific exceptions exist.
8. Prepare a **Marketplace reviewer deployment profile** that aligns with your existing `ManagedApplicationReviewer` runtime assumptions. This is an IIP-specific recommendation rather than a Microsoft doc requirement, but it will make certification and private-plan review much safer. citeturn9view0turn13view0turn14view0turn16view0

### Business and operations workstream

1. Create or enroll the company in **Microsoft Marketplace in Partner Center**.
2. Complete legal/business verification.
3. Ensure the correct people have owner/manager/developer roles in Partner Center and verify notification email settings.
4. Confirm who can sign the Microsoft Publisher Agreement.
5. Decide the commercial model:
   - standard private plan only,
   - or private plan plus later private offers for negotiated terms.
6. Collect the **customer Azure subscription IDs** for each insurer private-plan target.
7. Decide whether any target customer is on a CSP-resold subscription, because private plans are not supported there.
8. If you intend to use Marketplace metering, prepare the Entra application/tenant information required for metering identity configuration.
9. Prepare internal testing instructions for the certification team and preview sign-off path. citeturn17view0turn17view1turn16view0turn11view0turn13view0turn10view0turn21view0

My realistic schedule estimate, based strictly on Microsoft’s documented prerequisites and what they do or do not promise, is:

- **Start Partner Center enrollment and verification immediately**.
- **In parallel**, start Marketplace package engineering.
- Expect **3–5 business days** for account verification in the normal case.
- Expect automated offer validation steps to be short.
- Treat manual certification turnaround as **variable and not SLA-backed in public docs**.
- Do not make a hard insurer-facing launch commitment until:
  - Partner Center account verification is complete,
  - your first preview package is accepted,
  - and your private plan audience targeting is confirmed with real insurer subscription IDs. citeturn17view1turn16view0turn15view0

## Ordered checklist you can hand to engineering and business operations

### Engineering checklist

1. Add a Marketplace packaging pipeline that produces:
   - `mainTemplate.json`
   - `createUiDefinition.json`
   - `iip-managedapp.zip`
2. Ensure the zip has the required files at the **root**.
3. Keep Bicep as source-of-truth, but compile for submission; do not try to submit raw Bicep.
4. Validate that compiled ARM stays on **languageVersion 1.0** compatibility for Managed Applications.
5. Add ARM Template Test Toolkit to CI.
6. Create a Marketplace-specific deployment parameterization strategy:
   - customer-facing deployment params surfaced in `createUiDefinition.json`
   - hidden/internal params minimized
   - insurer-specific overrides handled through plan/package versioning, not ad hoc template edits
7. Implement Managed Application access posture:
   - publisher management access enabled
   - JIT enabled
   - customer access mode chosen deliberately
   - support principal/group IDs finalized
8. Explicitly design **data-plane** access:
   - SQL
   - Blob
   - Key Vault
   - AI Search
   - Azure OpenAI
   - Document Intelligence
   Do not assume managed-resource-group RBAC solves these.
9. Live-verify service prerequisites in the target region/subscription:
   - Azure OpenAI availability/quota
   - AI Search SKU availability
   - Document Intelligence regional availability
   - Container Apps constraints
10. Upload the package on the Partner Center Technical configuration page and version it semantically. citeturn11view0turn13view0turn9view0turn5view1turn5view2

### Business operations checklist

1. Create/enroll the Microsoft Marketplace account in Partner Center.
2. Complete company/legal/contact verification.
3. Confirm who has authority to accept the Publisher Agreement.
4. Confirm publisher roles and notification audience in Partner Center.
5. Choose **Managed Application plan** as the Azure Application offer type.
6. Decide whether you need:
   - only private plans, or
   - later private offers for negotiated commercial terms.
7. Collect each insurer’s **Azure subscription ID(s)** for private-plan targeting.
8. Screen for CSP-resold subscriptions, because private plans are not supported there.
9. Prepare preview audience accounts/subscriptions for internal and pre-customer validation.
10. Plan for certification notes and test instructions in the submission.
11. If monetization uses Marketplace metering, prepare the metering identity details and pricing structure.
12. Do not set a fixed submission date until Partner Center verification is complete and the first certification pass reaches preview. citeturn17view0turn17view1turn11view0turn13view0turn16view0turn21view0

## Live-verification items before you commit to a submission date

The following points are stable enough to design around, but I would still verify them live in Microsoft Learn / Partner Center before the submission calendar is locked:

- whether Managed Application submission still requires ARM `languageVersion` 1.0 only in the exact current toolchain you use,
- whether Partner Center still treats private-plan Azure Application submissions under the same certification path as public plans,
- whether any of your chosen Azure services have acquired new Managed Application caveats since the current docs,
- whether your target customer subscriptions are eligible for the private-plan targeting model you want,
- whether Azure OpenAI / AI Search / Document Intelligence prerequisites in the target customer region or subscription create deployment blockers,
- whether JIT’s current limitation around explicit principal inclusion has changed,
- whether any new Partner Center packaging or technical configuration deprecations have been introduced since the current docs were updated. citeturn9view0turn13view0turn14view0turn16view0

## Recent changes and current traps worth flagging

Three current documentation items are especially important because they are easy to miss and they do affect implementation planning.

First, **reuse technical configuration** in Partner Center is being deprecated. If you were thinking of reusing the same technical configuration across multiple Azure application plans, Microsoft’s current guidance is to stop depending on that and manage plan technical configuration independently going forward. citeturn13view0

Second, the **historical package retention view** in Partner Center is being deprecated. Microsoft says only the latest package version per plan will be preserved going forward, so if you need historical artifacts for compliance or rollback analysis, keep them in your own artifact storage and release records. citeturn13view0

Third, **Bicep is supported for authoring but not as the direct publish artifact** for Managed Applications. The output that matters for publication is still ARM JSON plus `createUiDefinition.json`. If your engineering team assumes “we already have Bicep, so we are ready for Marketplace,” that assumption is incomplete. citeturn9view0turn5view1