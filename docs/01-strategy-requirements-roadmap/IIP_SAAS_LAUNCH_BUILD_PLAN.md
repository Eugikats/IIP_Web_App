# IIP SaaS: launch build plan

Written 2026-10-08. The aim: `app.insuranceintelligenceplatform.com` live, with a public landing page, sign-up and sign-in with Microsoft, Google or an email code, and everything the Managed App edition has, so that the last step is adding DNS records. Work happens on branch `saas/merge-v2-2026-10-08` in `Projects/agenticIip-saas-merge`.

The landing page comes first: without it there is nothing to sign up from.

## Phase 1: landing page and the two front doors

Status 2026-10-08: done.

1. **Landing page at `/`**, built from CIP's marketing components (navigation, footer, section headings, reveal animation, container) with IIP wording. Sections: what IIP does, the two products, how it works, why it is safe, and a final sign-up prompt.
2. **Two products on the page and at sign-up:** IIP for Insurance (claims, documents, service standards, fraud signals, audit) and IIP for Bancassurance (prospect scoring, recommended covers, approved outreach, follow-ups). For now both sign up into the same system.
3. **Sign-up page at `/signup`:** the person picks Insurance or Bancassurance, and the choice is carried into sign-in and, in Phase 2, onto the organisation.
4. **The current sign-in page moves to `/login`.** Signed-in users still land in `/app`.
5. **What the page must not say:** no partner logos, no percentage outcomes, no compliance or certification claims. CIP's page has a partner logo strip and percentage figures that are true for CIP, not for IIP. They return only when IIP has its own evidence.
6. **Design rules:** IIP's design rules apply (no emoji, no em dashes, reduced motion respected). CIP's glow, glass and gradient effects are left out, because IIP's design rules do not allow them on screens a person sees.
7. **Checked:** type check, lint, production build, and the page opened in a browser at desktop and phone width.

## Phase 2: sign-in and sign-up that work end to end

Status 2026-10-09: built in code and tested; needs the sign-in directory set up before it works live. See `docs/05-deployment-release-enablement/IIP_SAAS_ACTIVATION_CHECKLIST.md`.

1. **Sign-in options:** Microsoft (work accounts), Google and email code, by porting CIP's sign-in code (`lib/auth/*` and its login screen). CIP uses Microsoft Entra for work accounts and an Entra External ID directory for Google and email.
2. **One sign-in directory for both products:** an IIP app registration in the same External ID directory CIP uses. This needs someone with admin rights in that directory.
3. **Create an organisation after first sign-in:** a screen that calls the existing `POST /api/v1/organisations`, with the product chosen at sign-up stored on the organisation (a new field, added through a migration).
4. **Invitations:** joining an existing organisation from an invitation link, using the existing API.
5. **API:** accept tokens from the External ID directory as well as the work-account directory.

## Phase 3: wiring to the existing resources

Detailed in `docs/05-deployment-release-enablement/IIP_SAAS_CUTOVER_ON_EXISTING_RESOURCES.md`.

1. The organisation backfill script that turns today's tenant into the first organisation.
2. SaaS settings on the existing apps (multi-tenant on, External ID, addresses, key ring).
3. Key Vault key and key-ring container (needs a Key Vault permission for the account doing it).
4. Separate Service Bus queues if the Managed App and SaaS apps ever run side by side.
5. WhatsApp connection re-saved per organisation.
6. Rehearsal on a laptop copy of the database, then Step A and Step B on dev.

## Phase 4: legal pages and go-live

1. Privacy, terms, support and data-deletion pages, reviewed by counsel. Meta, Microsoft and Partner Center all ask for these addresses.
2. DNS records at Namecheap (I supply them): `app` and `api` as CNAME plus a TXT verification record each, and the root domain as an A record plus TXT.
3. Hostnames and managed certificates bound to the apps; sign-in redirect and Meta callback updated.
4. A last check that `app.insuranceintelligenceplatform.com` signs up, signs in and opens the workspace.

## Decisions taken

- Region: `westus2` (owner, 2026-10-08).
- Today's field-test tenant becomes the first organisation (recommended, 2026-10-08).
- One code base with a switch, the SaaS repository as the main line (recommended; awaiting the owner's yes).

## Open

- Replace the Managed App code on the existing two web apps, or run the SaaS app beside it (recommendation: replace).
- Who creates the IIP registration in the External ID directory, and the Key Vault permission.
