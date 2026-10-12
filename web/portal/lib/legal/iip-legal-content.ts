/**
 * Legal copy for the Insurance Intelligence Platform (IIP) hosted service.
 * Operator entity: Otic Technologies Limited.
 *
 * Adapted from CIP's terms and privacy policy (apps/web/lib/legal/cip-terms-content.ts). What changed: credit
 * and lending language became insurance, claims and bancassurance language; WhatsApp and the AI commitments
 * describe how IIP works (AI prepares, a person decides); staff-only internal rules (disciplinary action, notice
 * boards) were left out of the public privacy notice; the Google and Microsoft section lists the permissions IIP
 * actually requests today.
 *
 * Every commitment in this file must stay true of the product. Change the product and this text together.
 * This text has not been reviewed by counsel; it must be before the pages are relied on with customers.
 */

export const LEGAL_ENTITY = "Otic Technologies Limited";
export const PRODUCT_NAME = "Insurance Intelligence Platform";
export const PRODUCT_SHORT = "IIP";
/** General and terms contact */
export const LEGAL_CONTACT_EMAIL = "info@oticgroup.net";
/** Privacy and data protection contact */
export const PRIVACY_CONTACT_EMAIL = "legal@oticgroup.net";
export const LEGAL_LAST_UPDATED = "12 October 2026";
export const PRIVACY_LAST_UPDATED = "12 October 2026";

export type LegalSection = {
  id: string;
  title: string;
  paragraphs: string[];
};

export const TERMS_PAGE = {
  title: "Terms and Conditions",
  description: `Terms and Conditions for the ${PRODUCT_NAME} (${PRODUCT_SHORT}).`,
  canonical: "/terms",
} as const;

export const PRIVACY_PAGE = {
  title: "Data Privacy & Protection Policy",
  description: `How ${LEGAL_ENTITY} collects, processes, stores and protects personal and institutional data in the ${PRODUCT_NAME}, in line with the data protection laws of Uganda.`,
  canonical: "/privacy",
} as const;

export const TERMS_SECTIONS: LegalSection[] = [
  {
    id: "intro",
    title: "Introduction",
    paragraphs: [
      "These Terms and Conditions (\"Terms\") govern your access to and use of the Insurance Intelligence Platform (\"IIP\", \"Platform\", \"Service\" or \"Services\") provided by Otic Technologies Limited (\"we\", \"us\" or \"our\", also \"the Provider\").",
      "By accessing or using the IIP website, Platform, applications, APIs, messaging channels, tools, products or Services, you agree to these Terms.",
      "These Terms apply to all users of IIP, including insurers, banks and other organisations that sell insurance, brokers and agents, employees and representatives of those organisations, policyholders, claimants, prospective customers and other authorised users.",
    ],
  },
  {
    id: "1-acceptance-of-these-terms",
    title: "1. ACCEPTANCE OF THESE TERMS",
    paragraphs: [
      "By using IIP, you confirm that:",
      "a. you have read and understood these Terms;",
      "b. you agree to comply with them;",
      "c. you have the legal capacity to enter into this agreement; and",
      "d. where you are using IIP on behalf of an organisation, you have authority to bind that organisation.",
      "If you are under the legal age required to independently enter into a contract in your jurisdiction, you may only use IIP where permitted by applicable law and with any required consent or supervision.",
    ],
  },
  {
    id: "2-eligibility-and-account-registration",
    title: "2. ELIGIBILITY AND ACCOUNT REGISTRATION",
    paragraphs: [
      "You agree to provide accurate, complete and current information when creating and maintaining your account.",
      "You are responsible for:",
      "- maintaining the confidentiality of your login credentials;",
      "- preventing unauthorised access to your account;",
      "- ensuring that information associated with your account remains accurate;",
      "- notifying us promptly of suspected unauthorised access; and",
      "- ensuring that your use of IIP complies with these Terms and applicable law.",
      "We may require identity, business or institutional verification before granting access to certain Services.",
      "We reserve the right to refuse, suspend or restrict an account where reasonably necessary to protect IIP, its users, our partners or the integrity and security of the Services.",
    ],
  },
  {
    id: "3-institutional-users",
    title: "3. INSTITUTIONAL USERS",
    paragraphs: [
      "Institutional Users may include insurers, reinsurers, banks and other financial institutions that sell insurance (bancassurance), brokers, agents, claims and loss-adjusting firms and other organisations authorised to use IIP.",
      "Institutional Users may provide access to IIP to their employees, officers, contractors or other authorised representatives (\"Authorised Users\").",
      "An Institutional User is responsible for:",
      "a. ensuring that its Authorised Users comply with these Terms;",
      "b. determining and managing user access rights and roles;",
      "c. protecting credentials and access information;",
      "d. ensuring that information submitted to IIP is lawfully obtained and processed;",
      "e. complying with applicable insurance, financial-sector, data-protection, consumer-protection and other regulatory requirements, including requirements of the Insurance Regulatory Authority; and",
      "f. maintaining appropriate human and institutional oversight over every decision made using IIP.",
    ],
  },
  {
    id: "4-individuals-and-customers",
    title: "4. POLICYHOLDERS, CLAIMANTS AND OTHER INDIVIDUALS",
    paragraphs: [
      "Individuals may interact with IIP through an insurer or a bank, for example by sending a message on WhatsApp, uploading a document through a secure link or answering questions about a claim or a product.",
      "Depending on the Service being used, you may be asked for information about your identity, your policy, an incident or loss, your documents, your business or your contact details.",
      "You are responsible for ensuring that information you provide is accurate and not misleading.",
      "You must not knowingly provide fraudulent, forged, manipulated or misleading information.",
      "Where IIP is used by an insurer or a bank, that organisation has its own terms and obligations governing its relationship with you. Decisions about your cover or your claim are made by that organisation, not by IIP.",
    ],
  },
  {
    id: "5-iip-services-and-modules",
    title: "5. IIP SERVICES AND MODULES",
    paragraphs: [
      "IIP operates as a modular platform. Services may include claims management, document intelligence, a WhatsApp assistant, customer and prospect management, outreach, reporting and the IIP Academy.",
      "We may introduce new Modules and Services and may modify, improve, replace or discontinue existing features from time to time.",
      "Access to a particular Module does not automatically provide access to all other IIP Services.",
    ],
  },
  {
    id: "6-subscriptions-and-fees",
    title: "6. SUBSCRIPTIONS, FEES AND AI CREDITS",
    paragraphs: [
      "IIP Services may be provided on a paid Subscription basis.",
      "Subscription fees, implementation fees, Module fees, usage fees, support fees and other charges will be communicated through the applicable commercial arrangement.",
      "Use of artificial-intelligence features may be measured in credits, with an allowance included in a plan. Where an allowance is used up, AI features may be paused until it is renewed or increased, and we will tell you when that happens.",
      "Unless expressly stated otherwise, fees are exclusive of applicable taxes.",
      "Institutional Users shall pay fees in accordance with the applicable commercial agreement.",
      "IIP may be offered on an initial free trial basis for the purpose of evaluating the Platform and its suitability for the intended use. Any such free trial is subject to these Terms and any applicable terms governing the use of the Platform.",
    ],
  },
  {
    id: "7-artificial-intelligence-and-human-decisions",
    title: "7. ARTIFICIAL INTELLIGENCE AND HUMAN DECISIONS",
    paragraphs: [
      "IIP uses artificial intelligence, including language models, document-reading services, search over approved sources, translation and automation.",
      "IIP is designed so that AI prepares work and people decide. AI may summarise, classify, extract, draft, recommend and flag. AI does not approve or reject a claim, release or deny a payment, bind a policy, set a final premium, make a final underwriting decision, or accuse any person of fraud. Those decisions, and any final regulated communication to a customer, are made by a named person who is responsible for them.",
      "AI-generated outputs may not always be accurate, complete, current or appropriate for a particular situation. Answers about cover or procedure are intended to rely on sources approved by the Institutional User and to say when no approved source is available.",
      "Users should exercise appropriate judgment when relying on such outputs and must keep appropriate human oversight, review or intervention in place.",
    ],
  },
  {
    id: "8-no-guarantee-of-accuracy",
    title: "8. NO GUARANTEE OF ACCURACY",
    paragraphs: [
      "IIP relies on information supplied by users, Institutional Users, third-party sources, connected systems, documents, models and other inputs.",
      "Although we seek to maintain reliable Services, we do not guarantee that every piece of information or every IIP output will always be accurate, complete, current, error-free, uninterrupted or suitable for every purpose.",
      "The reliability of an output may depend on the quality and completeness of the underlying information.",
    ],
  },
  {
    id: "9-insurance-disclaimer",
    title: "9. INSURANCE DISCLAIMER",
    paragraphs: [
      "IIP is a technology and decision-support platform.",
      "Unless expressly stated otherwise, the Provider is not an insurer, reinsurer, broker, agent, loss adjuster or other regulated insurance intermediary merely by providing technology, analytics, intelligence or assistant functionality, and does not provide banking, lending, investment, legal, accounting or other regulated services.",
      "Information provided through IIP, including answers from the assistant, should not be read as a guarantee that cover exists, that a claim will be accepted or paid, or that a particular product or price will be offered.",
      "Where a person applies for cover or makes a claim through IIP, the relevant insurer remains responsible for its own product, eligibility requirements, underwriting, claims process and final decision.",
    ],
  },
  {
    id: "10-user-data",
    title: "10. USER DATA",
    paragraphs: [
      "You retain your rights in information and content that you lawfully submit to IIP, subject to applicable law and any agreement governing your relationship with an Institutional User.",
      "By submitting information to IIP, you grant us the permissions reasonably necessary to receive, store, process, transmit and use that information for the purposes of providing, securing and maintaining the Services, subject to applicable law and our Privacy Policy.",
      "Institutional Users are responsible for ensuring that they have the necessary rights, permissions and lawful basis to provide data to IIP, including consent to contact people through messaging channels.",
    ],
  },
  {
    id: "11-personal-data-and-privacy",
    title: "11. PERSONAL DATA AND PRIVACY",
    paragraphs: [
      "Your privacy is important to us.",
      "Personal information processed through IIP will be handled in accordance with applicable data-protection and privacy laws and our Privacy Policy.",
      "Depending on the particular Service and data flow, the Provider, an Institutional User or another party may act as a data controller, data processor or in another legally recognised capacity. For information that an Institutional User collects from its own customers, that Institutional User is ordinarily the controller and the Provider acts as its processor.",
      "Where required, additional data-processing terms may apply.",
      "You may have rights in relation to your personal information under applicable law, including rights relating to access, correction, objection, deletion or other forms of control over your information.",
      "Details of how we process personal information are provided in our Privacy Policy.",
    ],
  },
  {
    id: "12-claims-policy-and-customer-information",
    title: "12. CLAIMS, POLICY AND CUSTOMER INFORMATION",
    paragraphs: [
      "Where IIP processes or receives claims, policy or customer information, that information may originate from the user, an Institutional User, documents, messages, public sources or other lawful sources.",
      "We do not guarantee the accuracy or completeness of information obtained from independent third parties.",
      "Where you believe that information associated with you is inaccurate, you may contact the relevant Institutional User or the Provider, as applicable.",
      "We will handle requests relating to personal information in accordance with applicable law.",
    ],
  },
  {
    id: "13-messaging-channels",
    title: "13. MESSAGING CHANNELS",
    paragraphs: [
      "IIP can send and receive messages for an Institutional User through channels such as WhatsApp. Those channels are operated by third parties and are subject to their own terms and policies, which an Institutional User must follow.",
      "An Institutional User is responsible for having a lawful basis and any required consent before contacting a person, for honouring a request to stop, and for the content of messages sent in its name.",
      "Messages sent to customers about claims, offers, payments or other consequential matters are approved by a named person before they are sent.",
    ],
  },
  {
    id: "14-data-security",
    title: "14. DATA SECURITY",
    paragraphs: [
      "We implement reasonable technical and organisational measures designed to protect information processed through IIP.",
      "Security measures may include:",
      "- authentication and role-based access controls;",
      "- separation of each organisation's data;",
      "- encryption;",
      "- monitoring, logging and audit trails;",
      "- vulnerability management;",
      "- backups;",
      "- incident response;",
      "- security testing; and",
      "- other appropriate security measures.",
      "However, no electronic system can be guaranteed to be completely secure.",
      "Users must also take reasonable steps to protect their accounts, devices, credentials and information.",
    ],
  },
  {
    id: "15-acceptable-use",
    title: "15. ACCEPTABLE USE",
    paragraphs: [
      "You may use IIP only for lawful and authorised purposes.",
      "You must not:",
      "a. access or use IIP without authorisation;",
      "b. impersonate another person or entity;",
      "c. submit fraudulent, forged or misleading information or documents;",
      "d. interfere with the operation or security of IIP;",
      "e. introduce viruses, malware or other harmful code;",
      "f. attempt to bypass authentication or security controls;",
      "g. access information belonging to another user or organisation without authorisation;",
      "h. scrape, harvest or systematically extract information from IIP without permission;",
      "i. reverse engineer or attempt to discover source code, algorithms or protected technology except where expressly permitted by law;",
      "j. copy, reproduce, distribute, sell, lease or sublicense IIP without authorisation;",
      "k. use IIP to facilitate fraud, money laundering or other unlawful conduct;",
      "l. use IIP to unlawfully discriminate against any person;",
      "m. misuse AI functionality, including attempting to make it disclose its instructions or act beyond its authority;",
      "n. interfere with another user's access to IIP; or",
      "o. use IIP in any manner that could reasonably harm the Platform, its users or the Provider.",
    ],
  },
  {
    id: "16-intellectual-property",
    title: "16. INTELLECTUAL PROPERTY",
    paragraphs: [
      "IIP and its underlying technology are owned by or licensed to the Provider and are protected by applicable intellectual-property laws.",
      "Nothing in these Terms transfers ownership of IIP or its underlying technology to a User.",
      "Users receive only the limited right to access and use IIP in accordance with these Terms and any applicable commercial agreement.",
    ],
  },
  {
    id: "17-user-feedback",
    title: "17. USER FEEDBACK",
    paragraphs: [
      "If you provide suggestions, ideas, recommendations, comments or other feedback regarding IIP, you grant us the right to use that feedback without restriction or obligation, provided that such use does not unlawfully disclose your confidential information or personal data.",
    ],
  },
  {
    id: "18-third-party-services",
    title: "18. THIRD-PARTY SERVICES",
    paragraphs: [
      "IIP may connect to or depend upon third-party systems, cloud infrastructure, AI services, messaging platforms, identity services, file-storage accounts such as Google Drive and Microsoft OneDrive, communication services or other third-party services.",
      "Third-party services may be subject to separate terms.",
      "We are not responsible for failures attributable solely to third-party systems outside our reasonable control.",
    ],
  },
  {
    id: "19-platform-availability",
    title: "19. PLATFORM AVAILABILITY",
    paragraphs: [
      "We will use reasonable efforts to maintain the availability of IIP.",
      "However, IIP may occasionally be unavailable because of:",
      "a. scheduled maintenance;",
      "b. emergency maintenance;",
      "c. security incidents;",
      "d. infrastructure failures;",
      "e. telecommunications or internet failures;",
      "f. third-party service interruptions, including messaging platforms;",
      "g. regulatory or governmental requirements; or",
      "h. circumstances beyond our reasonable control.",
      "Where applicable, Institutional Users may receive additional availability and support commitments under a separate Service Level Agreement.",
    ],
  },
  {
    id: "20-support-and-maintenance",
    title: "20. SUPPORT AND MAINTENANCE",
    paragraphs: [
      "The level of technical support available to a User may depend on the applicable service or subscription.",
      "Additional support, implementation, maintenance or professional services may be provided under separate arrangements.",
    ],
  },
  {
    id: "21-suspension-of-access",
    title: "21. SUSPENSION OF ACCESS",
    paragraphs: [
      "We may suspend or restrict access to IIP where reasonably necessary to:",
      "a. protect the security of the Platform;",
      "b. prevent fraud or abuse;",
      "c. investigate suspected unlawful activity;",
      "d. address a serious breach of these Terms;",
      "e. comply with applicable law;",
      "f. protect other users;",
      "g. address security vulnerabilities; or",
      "h. address unpaid fees.",
      "Where reasonably practicable, we will provide notice before suspension, except where immediate action is necessary.",
    ],
  },
  {
    id: "22-termination",
    title: "22. TERMINATION",
    paragraphs: [
      "A User may stop using IIP at any time, subject to any continuing obligations under an applicable commercial agreement.",
      "We may terminate or restrict access where:",
      "a. the User materially breaches these Terms;",
      "b. continued access would violate applicable law;",
      "c. the User engages in fraudulent, abusive or harmful conduct;",
      "d. the relevant Subscription or institutional agreement expires or is terminated; or",
      "e. continued operation of the relevant Service becomes impracticable.",
      "Termination does not affect rights or obligations that accrued before termination.",
    ],
  },
  {
    id: "23-effect-of-termination",
    title: "23. EFFECT OF TERMINATION",
    paragraphs: [
      "Following termination:",
      "- access to applicable Services shall cease;",
      "- account credentials may be disabled;",
      "- outstanding amounts will remain payable;",
      "- information may be retained where required or permitted by law, and will otherwise be handled as set out in our Privacy Policy and the applicable agreement; and",
      "- provisions intended to survive termination shall continue to apply.",
    ],
  },
  {
    id: "24-confidentiality",
    title: "24. CONFIDENTIALITY",
    paragraphs: [
      "Users may receive confidential or proprietary information through their use of IIP.",
      "Users must not disclose or misuse confidential information belonging to the Provider, another User or an Institutional User except where authorised or required by law.",
      "Separate confidentiality obligations may apply under an NDA or institutional agreement.",
    ],
  },
  {
    id: "25-complaints-and-disputes",
    title: "25. COMPLAINTS AND DISPUTES",
    paragraphs: [
      "We encourage users to contact us first with any complaint concerning IIP.",
      "Where a complaint concerns a policy, a claim, a premium, a claims decision or other service provided by an insurer, a bank or another Institutional User, the complaint may need to be directed to that organisation in accordance with its complaints procedure, and may then be taken to the Insurance Regulatory Authority.",
      "Nothing in these Terms limits any mandatory rights or remedies available to a consumer or other person under applicable law.",
    ],
  },
  {
    id: "26-limitation-of-liability",
    title: "26. LIMITATION OF LIABILITY",
    paragraphs: [
      "To the maximum extent permitted by applicable law, the Provider shall not be liable for indirect, incidental, special, consequential or punitive losses arising from the use of the Platform.",
      "This may include loss of profits, revenue, business opportunities, goodwill, anticipated savings or data, except where such liability cannot lawfully be excluded.",
      "Nothing in these Terms excludes or limits liability that cannot legally be excluded or limited.",
      "Institutional Users may have separate liability provisions under their applicable commercial agreements.",
    ],
  },
  {
    id: "27-user-indemnity",
    title: "27. USER INDEMNITY",
    paragraphs: [
      "To the extent permitted by law, you agree to indemnify the Provider against claims, losses, liabilities and reasonable costs arising from:",
      "a. your material breach of these Terms;",
      "b. your unlawful use of IIP;",
      "c. fraudulent or misleading information submitted by you;",
      "d. your infringement of third-party rights; or",
      "e. your unauthorised or improper use of the Platform.",
    ],
  },
  {
    id: "28-changes-to-iip",
    title: "28. CHANGES TO IIP",
    paragraphs: [
      "We may modify, improve, update or change IIP from time to time.",
      "Where a material change substantially affects a paid Service, we will provide reasonable notice where practicable.",
    ],
  },
  {
    id: "29-changes-to-these-terms",
    title: "29. CHANGES TO THESE TERMS",
    paragraphs: [
      "We may update these Terms from time to time.",
      "The updated Terms will be published on this website with a revised \"Last updated\" date.",
      "Where required by law, or where changes are material, we may provide additional notice.",
      "Your continued use of IIP after the effective date of updated Terms constitutes acceptance of the updated Terms, to the extent permitted by law.",
    ],
  },
  {
    id: "30-electronic-communications",
    title: "30. ELECTRONIC COMMUNICATIONS",
    paragraphs: [
      "You agree that we may communicate with you electronically through email, the Platform, notifications or other electronic channels.",
      "Where legally valid, electronic records, acknowledgements and electronic acceptance may constitute binding records.",
    ],
  },
  {
    id: "31-international-use",
    title: "31. INTERNATIONAL USE",
    paragraphs: [
      "IIP may be made available to users in multiple jurisdictions.",
      "If you access IIP from outside Uganda, you are responsible for complying with laws applicable to your use of the Services in your jurisdiction.",
      "Additional jurisdiction-specific terms may apply where required by law.",
    ],
  },
  {
    id: "32-governing-law",
    title: "32. GOVERNING LAW",
    paragraphs: [
      "Unless otherwise provided in a separate written agreement, these Terms shall be governed by the laws of the Republic of Uganda.",
      "Subject to applicable mandatory laws, disputes arising from these Terms shall be subject to the jurisdiction of the courts of Uganda.",
    ],
  },
  {
    id: "33-severability",
    title: "33. SEVERABILITY",
    paragraphs: [
      "If any provision of these Terms is determined to be invalid or unenforceable, the remaining provisions shall continue in full force and effect.",
    ],
  },
  {
    id: "34-no-waiver",
    title: "34. NO WAIVER",
    paragraphs: [
      "Failure by the Provider to enforce any provision of these Terms does not constitute a waiver of its right to enforce that provision in the future.",
    ],
  },
  {
    id: "35-assignment",
    title: "35. ASSIGNMENT",
    paragraphs: [
      "You may not transfer your rights or obligations under these Terms without our prior written consent, except where permitted by law.",
      "We may assign or transfer these Terms as part of a merger, restructuring, sale, reorganisation or transfer of the relevant business or assets, subject to applicable law.",
    ],
  },
  {
    id: "36-contact-us",
    title: "36. CONTACT US",
    paragraphs: [
      "For questions, complaints, legal notices or other enquiries concerning IIP, please contact:",
      "Email: info@oticgroup.net",
    ],
  },
  {
    id: "37-acceptance",
    title: "37. ACCEPTANCE",
    paragraphs: [
      "By accessing or using IIP, you acknowledge that you have read, understood and agreed to these Terms and any applicable additional terms.",
    ],
  },
];

export const PRIVACY_SECTIONS: LegalSection[] = [
  {
    id: "intro",
    title: "1.0 Introduction",
    paragraphs: [
      "At Otic Technologies Limited, we are committed to the lawful, secure and ethical collection, use and management of personal and institutional data, and to protecting the right to privacy of the people whose data passes through the Insurance Intelligence Platform (\"IIP\"): our customers' staff, policyholders, claimants, prospective customers and other stakeholders. This policy sets out the principles and procedures that govern how Otic Technologies Limited (\"the Company\", \"we\" or \"us\") collects, processes, stores, shares and disposes of data in IIP, in order to protect the privacy of personal data. It is designed to comply with the data protection laws of Uganda, including the Data Protection and Privacy Act, 2019, and the regulations made under it.",
    ],
  },
  {
    id: "definitions",
    title: "2.0 Definitions",
    paragraphs: [
      "2.1 Data means information which is processed by means of equipment operating automatically in response to instructions given for that purpose, is recorded with the intention that it should be processed by such equipment, or is recorded as part of a relevant filing system or accessible record.",
      "2.2 Personal Data means information about a person from which the person can be identified, recorded in any form, including nationality, age or marital status, educational level or occupation, an identification number, symbol or other particulars assigned to a person, identity data, and other information in the possession of, or likely to come into the possession of, the data controller, including an expression of opinion about the individual.",
      "2.3 Data subject means an individual from whom or in respect of whom personal information has been requested, collected, collated, processed or stored.",
      "2.4 Data Controller means a person who alone, jointly with others or in common with others, or as a statutory duty, determines the purposes for and the manner in which personal data is processed.",
      "2.5 Data Processor means a person, other than an employee of the data controller, who processes data on behalf of the data controller.",
      "2.6 Policy means this Data Privacy & Protection Policy of Otic Technologies Limited.",
    ],
  },
  {
    id: "roles",
    title: "3.0 Our role",
    paragraphs: [
      "3.1 IIP is used by insurers, banks that sell insurance and similar organisations (\"Institutional Users\"). For personal data that an Institutional User collects from its own customers, staff and prospects through IIP, the Institutional User is ordinarily the data controller and Otic Technologies Limited acts as its data processor. In that case we process the data on the Institutional User's instructions, and requests about that data are best directed to the Institutional User.",
      "3.2 For data about the people who use IIP's own website and accounts (for example, sign-up and sign-in details), and for data we process for our own security and billing, Otic Technologies Limited is the data controller.",
    ],
  },
  {
    id: "collect",
    title: "4.0 What we collect",
    paragraphs: [
      "4.1 Account data: name, work email address, role, organisation and sign-in details of the people who use IIP.",
      "4.2 Conversation data: messages that people send and receive through channels connected to IIP, such as WhatsApp, including text, voice notes and attachments, together with the language used and the time of each message.",
      "4.3 Documents: documents uploaded for a claim, a policy or a customer, such as identity documents, forms, invoices, photographs and reports, and the information read from them.",
      "4.4 Customer and prospect records: names, phone numbers, the cover someone is interested in, what they have said about their work or situation, scores and notes recorded by staff or by the assistant, and the history of contact with them.",
      "4.5 Usage and security data: records of actions taken in IIP (an audit trail), technical logs, and usage of AI features measured in credits.",
      "4.6 Connected account data: where an organisation connects a Google or Microsoft account, the information described in section 12.0.",
    ],
  },
  {
    id: "use",
    title: "5.0 How we use it",
    paragraphs: [
      "5.1 To provide IIP: to take in and manage claims, read and check documents, answer customers, manage leads and customers, send messages that staff approve, and show staff what has happened.",
      "5.2 To keep IIP secure and working: authentication, access control, monitoring, preventing fraud and abuse, backups and support.",
      "5.3 To measure and bill use of the Service, including AI credits.",
      "5.4 To meet legal obligations and respond to lawful requests.",
      "5.5 We use content that an organisation puts into IIP only for that organisation's workspace, to provide the Service to it. We do not use it to train generalised artificial intelligence or machine learning models. Corrections that people record against document classifications are used to measure how accurate classification is within that workspace.",
    ],
  },
  {
    id: "ai",
    title: "6.0 Artificial intelligence and human decisions",
    paragraphs: [
      "6.1 IIP uses AI services to read documents, summarise, translate, search approved sources and draft replies. To produce an answer or a draft, relevant passages may be sent to the AI services that power IIP; this processing serves only that request.",
      "6.2 AI prepares work and people decide. IIP does not use AI to make final decisions about a claim, a payment, a policy, a premium or an underwriting outcome, and does not use it to accuse any person of fraud. Those decisions are made by a named person, and the action and the reason are recorded.",
    ],
  },
  {
    id: "sharing",
    title: "7.0 Who we share it with",
    paragraphs: [
      "7.1 We do not sell personal data, and we do not use it for advertising or to build profiles for advertising.",
      "7.2 We share data only with the service providers that help us run IIP, under confidentiality obligations: cloud hosting and storage providers, AI and language-processing providers, and messaging platforms such as WhatsApp through which an Institutional User chooses to communicate. We share data with the Institutional User whose workspace it belongs to.",
      "7.3 We may disclose data where required by law or to protect rights, safety and security.",
    ],
  },
  {
    id: "security",
    title: "8.0 Security",
    paragraphs: [
      "8.1 We apply technical and organisational measures to protect data, including authentication and role-based access, separation of each organisation's data, encryption in transit and at rest, audit logging, backups and incident response. Saved credentials and sign-in tokens for connected services are encrypted.",
      "8.2 No system is completely secure. People who use IIP must also protect their own accounts, devices and credentials.",
    ],
  },
  {
    id: "retention",
    title: "9.0 Retention and deletion",
    paragraphs: [
      "9.1 We keep data for as long as an organisation uses IIP, as set out in its agreement with us, and as the law requires or allows.",
      "9.2 An organisation can ask us to delete its data when it stops using IIP. A person whose data an organisation holds in IIP can ask that organisation to correct or delete it, or contact us at the address in section 14.0.",
    ],
  },
  {
    id: "principles-rights",
    title: "10.0 Data protection principles and data subject rights",
    paragraphs: [
      "10.1 Principles. We are committed to the following principles of data protection:",
      "we are accountable to the data subject for data collected, processed, held or used;",
      "we collect and process data fairly and lawfully;",
      "we collect, process, use or hold adequate, relevant and not excessive or unnecessary personal data;",
      "we retain personal data for the period authorised by law or for which the data is required;",
      "we ensure the quality of information collected, processed, used or held;",
      "we ensure transparency and participation of the data subject in the collection, processing, use and holding of personal data; and",
      "we at all times observe security safeguards in respect of the data.",
      "10.2 Rights. A data subject has the right to:",
      "request the correction of his or her personal data;",
      "know the purpose for which personal data is being collected;",
      "request the erasure of personal data we hold on him or her, where the law provides for it;",
      "withdraw consent at any time in relation to the collection, control and processing of personal data; and",
      "lodge a complaint with the Personal Data Protection Office.",
    ],
  },
  {
    id: "breaches",
    title: "11.0 Reporting data security breaches",
    paragraphs: [
      "11.1 Our staff and contractors must immediately report any security breach that involves personal data to our Data Protection Officer, who takes all reasonable steps to remedy it.",
      "11.2 In the event of a breach we act to contain it and stop further unauthorised access, isolate affected systems and preserve logs and evidence, notify our management and security team, notify affected Institutional Users and data subjects with clear information about what happened, the possible risks and what they can do to protect themselves, and notify the Personal Data Protection Office and, where appropriate, the Police. We cooperate with investigations to find the cause and prevent a repeat.",
    ],
  },
  {
    id: "google-microsoft-data",
    title: "12.0 Google and Microsoft account data",
    paragraphs: [
      "12.1 A person in an organisation that uses IIP may connect their own Google account (Google Drive) or Microsoft account (OneDrive) so that IIP can read a file they choose, such as a list of customers to import. This section explains what IIP accesses through those connections and how that data is handled.",
      "12.2 Data accessed. When a person connects a Google or Microsoft account, IIP receives:",
      "the person's email address and account identifier, so IIP knows which person connected the account;",
      "Google Drive: access only to the individual files the person picks in Google's own file picker (the Google \"drive.file\" permission). IIP reads the name and contents of the file picked. IIP cannot see any other file in the person's Google Drive, and does not save files to Google Drive today;",
      "OneDrive: the Microsoft permissions \"Files.ReadWrite\", \"User.Read\" and \"offline_access\". IIP reads only the file the person picks, and does not change or delete files in the person's OneDrive;",
      "SharePoint is not connected to IIP today. If we add it, we will update this policy before we do.",
      "IIP does not read the person's email (Gmail or Outlook), contacts or calendar, and does not read any file the person did not pick.",
      "12.3 How the data is used. The contents of a picked file are read so that the organisation can import the list or document it chose into its own IIP workspace. The data is used only within that organisation's IIP workspace and only for that purpose.",
      "12.4 What we do not do. We do not sell Google or Microsoft user data. We do not use it for advertising or to build user profiles. We do not use it to develop, improve or train generalised artificial intelligence or machine learning models. We do not share it with third parties except the infrastructure and AI service providers that operate IIP on our behalf under confidentiality obligations, or where required by law. Our staff do not read this data unless the organisation asks us to for support, it is necessary for security purposes such as investigating abuse, or it is required by law.",
      "12.5 Storage and protection. Sign-in tokens issued by Google or Microsoft are encrypted and are never sent to a browser. File contents are stored on secured cloud infrastructure, separated per organisation, and protected in transit and at rest.",
      "12.6 Retention and deletion. Disconnecting the connection in IIP deletes the stored sign-in tokens. A person can also revoke IIP's access at any time from their Google account (myaccount.google.com/connections) or Microsoft account (myapps.microsoft.com). To have other related data deleted, contact us at the address in section 14.0.",
      "12.7 Google API Services. IIP's use and transfer to any other app of information received from Google APIs will adhere to the Google API Services User Data Policy (https://developers.google.com/terms/api-services-user-data-policy), including the Limited Use requirements.",
    ],
  },
  {
    id: "changes",
    title: "13.0 Changes to this policy",
    paragraphs: [
      "13.1 We may update this policy from time to time. The updated policy will be published on this website with a revised \"Last updated\" date. Where a change is material we will give additional notice where required by law.",
    ],
  },
  {
    id: "contact",
    title: "14.0 Contact information",
    paragraphs: [
      "14.1 For any question about data protection and privacy in IIP, to exercise a right, or to report a concern, contact:",
      "legal@oticgroup.net (Head Legal Department, Otic Technologies Limited)",
    ],
  },
];
