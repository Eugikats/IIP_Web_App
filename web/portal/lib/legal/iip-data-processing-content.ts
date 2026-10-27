import { LEGAL_ENTITY, PRIVACY_CONTACT_EMAIL, PRODUCT_NAME, type LegalSection } from "@/lib/legal/iip-legal-content";

/**
 * The data processing agreement an organisation accepts when it sets up IIP. Every statement here must stay true of the product;
 * change the product and this text together. It has not been reviewed by counsel and must be before it is relied on with a bank
 * or an insurer. The version below is what an acceptance is recorded against: raise it when the text changes, and every
 * organisation is asked to accept again (see CompanyProfileService.CurrentDataProcessingVersion).
 */
export const DATA_PROCESSING_VERSION = "2026-10-01";
export const TERMS_VERSION = "2026-10-01";
export const DATA_PROCESSING_LAST_UPDATED = "27 October 2026";

export const DATA_PROCESSING_PAGE = {
  title: "Data Processing Agreement",
  description: `How ${LEGAL_ENTITY} processes personal data for an organisation that uses the ${PRODUCT_NAME}.`,
  canonical: "/data-processing",
} as const;

export const DATA_PROCESSING_SECTIONS: LegalSection[] = [
  {
    id: "roles",
    title: "1. Who is responsible for what",
    paragraphs: [
      `The organisation that signs up (\"the Organisation\") decides why and how its customers' personal data is used. ${LEGAL_ENTITY} (\"we\") processes that data on its behalf, only to provide ${PRODUCT_NAME} to it. In data protection terms the Organisation is the controller and we are its processor.`,
      "The Organisation is responsible for having a lawful basis to hold and use its customers' data, and for the agreement it records before it messages anyone about insurance. IIP will not send a marketing message to a person whose agreement is not recorded, but it cannot make that agreement lawful for the Organisation.",
    ],
  },
  {
    id: "what",
    title: "2. What we process and why",
    paragraphs: [
      "We process what the Organisation puts into IIP or receives through it: customer names and phone numbers, policy and claim records, documents sent for a claim, WhatsApp conversations, and the events a bank tells us about, which are limited to a phone number, what happened, when, and a few safe details. IIP does not accept balances, account numbers or transaction lists.",
      "We process it to run the services the Organisation switches on: answering customers, handling claims, sending reminders and offers within the Organisation's approved rules, and reporting. We do not use it for our own purposes or to train models.",
    ],
  },
  {
    id: "humans",
    title: "3. AI prepares, people decide",
    paragraphs: [
      "The AI in IIP prepares summaries, drafts and recommendations. It does not approve or reject a claim, bind a policy, set a premium or make any other decision about a person. Those remain with the Organisation's people, and IIP records who decided.",
    ],
  },
  {
    id: "subprocessors",
    title: "4. Other companies we use",
    paragraphs: [
      "To provide the service we use Microsoft Azure for hosting, databases, storage, search, document reading and AI models; Meta for WhatsApp; and SMS and email providers for messages the Organisation chooses to send. Payments for IIP Academy use a separate payment provider and never touch an organisation's customer data.",
      "We will tell the Organisation before we add another company that will process its customers' data, and it may object.",
    ],
  },
  {
    id: "location",
    title: "5. Where data is kept",
    paragraphs: [
      "Data is currently kept in Microsoft Azure regions outside Uganda. An Organisation that needs its data in a particular country must agree that with us before it sends personal data to IIP.",
    ],
  },
  {
    id: "security",
    title: "6. Security",
    paragraphs: [
      "Data is encrypted in transit and is encrypted at rest by the Azure services that hold it. People see only what their role allows, checked on the server, and one organisation's data is kept apart from another's. Important actions are written to an audit trail that does not copy message text or documents.",
      "WhatsApp messages travel through Meta's business platform, which receives and processes them. We do not describe them as end-to-end encrypted to the Organisation.",
      "We hold no security certification such as ISO 27001 or SOC 2 at present, and we say so if asked.",
    ],
  },
  {
    id: "incidents",
    title: "7. If something goes wrong",
    paragraphs: [
      "We will tell the Organisation without undue delay after we become aware of a breach of the security of its data, and tell it what we know, what we have done and what it may need to do.",
    ],
  },
  {
    id: "people",
    title: "8. Requests from the people the data is about",
    paragraphs: [
      "If a person asks the Organisation for their data, or to correct or delete it, we will help the Organisation to answer, using the export and deletion tools in IIP.",
    ],
  },
  {
    id: "leaving",
    title: "9. When the Organisation leaves",
    paragraphs: [
      "An administrator of the Organisation can export everything it owns in one file and can close the organisation, which deletes everything it owns. Closing cannot be undone. Copies in backups age out as the backups are replaced.",
    ],
  },
  {
    id: "contact",
    title: "10. Questions",
    paragraphs: [`Write to ${PRIVACY_CONTACT_EMAIL}. A signed agreement between the Organisation and ${LEGAL_ENTITY} may replace this one.`],
  },
];
