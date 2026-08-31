/**
 * Creates one English, product-neutral Google Form and a linked response Sheet.
 * Run createInsuranceDiscoveryForm() from a standalone Apps Script project.
 */

const STR = {
  en: {
    form: {
      title: 'Insurance Operations and Business Practice Study',
      responseSheetTitle: 'Insurance Operations Study Responses',
      description:
        'This study explores how insurance work is performed, governed, measured, and improved. It is intended for people with direct knowledge of insurance operations, management, control, distribution, technology, or transformation.\n\nStudy owner/contact: [ADD BEFORE ENABLING RESPONSES]\nWho can access responses: [ADD BEFORE ENABLING RESPONSES]\nRaw-response retention and deletion date: [ADD BEFORE ENABLING RESPONSES]\n\nPlease answer from recent experience. Do not include customer names, policy or claim numbers, medical or payment details, credentials, confidential pricing, privileged advice, or other information that could identify a person or expose your organization. Generalized and redacted examples are welcome. Most questions are optional; answer the sections closest to your experience and skip anything you cannot or should not answer. Estimated completion time: 60–150 minutes, depending on route and detail.',
      confirmation:
        'Thank you. Your response has been recorded. If you offered a follow-up, the study team may contact you using only the details you chose to provide.',
      closedMessage:
        'This study is not accepting responses yet. The study owner is completing privacy and pilot checks.',
    },
    logs: {
      edit: 'Form edit URL: ',
      live: 'Respondent URL: ',
      sheet: 'Response Sheet URL: ',
    },
    common: {
      mixedRoute: 'If you selected “Other / mixed / not sure” on the previous page, describe your exposure to insurance work.',
    },
    consent: {
      title: 'Consent and safe participation',
      help:
        'Participation is voluntary. The form does not collect email addresses automatically. You may skip optional questions or stop at any time. Responses will be used for product, service, workflow, and business research. They will not be used to evaluate individual employee performance or make employment, claim, underwriting, pricing, or other consequential decisions about a person.',
      question:
        'Having read the study description, do you voluntarily agree to participate and submit responses under these conditions?',
      agree: 'Yes, I agree to participate',
      decline: 'No, I do not agree to participate',
      declineTitle: 'No response will be requested',
      declineHelp: 'Select Submit to leave the study without answering research questions.',
      declineNote: 'You did not consent. Please do not enter any additional information.',
    },
    profile: {
      title: 'Your operating context',
      help: 'Please describe your context without naming clients, employees, claimants, or other individuals.',
      questions: [
        { type: 'paragraph', title: 'What is your current role, and what insurance decisions, workflows, teams, or controls do you directly know?' },
        { type: 'check', title: 'Which functions are part of your current or recent responsibilities?', options: ['Product or strategy', 'Distribution or sales', 'Underwriting', 'Policy administration', 'Premium collection or credit control', 'Claims', 'Customer service or complaints', 'Finance or actuarial', 'Reinsurance', 'Risk, compliance, legal, audit, or privacy', 'Technology, data, security, or integration', 'Executive or general management', 'External advisory or service delivery'] },
        { type: 'mc', title: 'How many years of direct insurance-sector experience do you have?', options: ['Less than 1 year', '1–2 years', '3–5 years', '6–10 years', '11–20 years', 'More than 20 years', 'Prefer not to say'], required: false },
        { type: 'check', title: 'Which organization types have you worked with directly?', options: ['Insurer', 'Reinsurer', 'Broker', 'Agent or agency', 'Bancassurance provider', 'Health insurer or HMO', 'Third-party administrator', 'Loss assessor or adjuster', 'Repairer or service provider', 'Regulator or industry body', 'Technology or outsourcing provider'] },
        { type: 'check', title: 'Which lines of business do you know directly?', options: ['Motor', 'Property', 'Marine or aviation', 'Engineering', 'Agriculture or index insurance', 'Health or medical', 'Life', 'Pensions or long-term savings', 'Microinsurance', 'Takaful', 'Liability', 'Specialty or commercial risks'] },
        { type: 'mc', title: 'Approximately how many people work in the organization whose operations you are describing?', options: ['1–19', '20–49', '50–199', '200–499', '500–999', '1,000 or more', 'Not sure or prefer not to say'], required: false },
        { type: 'paragraph', title: 'Which countries, branches, customer segments, and distribution channels are relevant to your answers?' },
        { type: 'paragraph', title: 'Which parts of the insurance lifecycle do you personally perform, supervise, audit, support, buy for, or receive reports about?' },
        { type: 'paragraph', title: 'How large and complex is the operating environment you know: approximate teams, locations, products, volumes, or partner network?' },
        { type: 'paragraph', title: 'What changed materially in this operating environment during the last two years?' },
      ],
      routeQuestion: 'During the last 12 months, have you directly performed, supervised, audited, or supported day-to-day insurance operations?',
      routeOps: 'Yes — direct or close operational experience',
      routeBusiness: 'No — primarily strategic, commercial, governance, advisory, or technology experience',
      routeMixed: 'Other / mixed / not sure',
    },
    operations: {
      title: 'A recent real work item',
      help: 'Use one recent ordinary, difficult, delayed, or failed case. Generalize or redact all sensitive details.',
      questions: [
        { type: 'paragraph', title: 'Choose one recent work item you knew well. What triggered it, and what outcome was needed?' },
        { type: 'paragraph', title: 'Walk through what actually happened from trigger to the latest outcome, in sequence.' },
        { type: 'paragraph', title: 'Who participated at each step, and what did each person or organization contribute?' },
        { type: 'paragraph', title: 'Which systems, documents, messages, spreadsheets, calls, or physical artifacts were used?' },
        { type: 'paragraph', title: 'Where did the item wait, bounce back, lose context, duplicate work, or require escalation?' },
        { type: 'paragraph', title: 'Which decision rights, authority limits, checks, approvals, or exceptions shaped the case?' },
        { type: 'paragraph', title: 'What differed from the documented process, and why?' },
        { type: 'paragraph', title: 'How did staff know the information was current, complete, trustworthy, and sufficient for the next action?' },
        { type: 'paragraph', title: 'What did the customer or external partner experience during this case?' },
        { type: 'paragraph', title: 'What was the avoidable cost, delay, risk, rework, or harm?' },
        { type: 'paragraph', title: 'What would have made this case easier without weakening control or human accountability?' },
      ],
    },
    lifecycle: {
      title: 'Lifecycle, handoffs, and exceptions',
      help: 'Answer for the areas you know. It is acceptable to say that a step does not apply.',
      questions: [
        { type: 'paragraph', title: 'From market need to product withdrawal, what are the main lifecycle stages in your organization?' },
        { type: 'paragraph', title: 'Where do product, actuarial, sales, underwriting, policy administration, claims, finance, service, compliance, and technology hand work to one another?' },
        { type: 'paragraph', title: 'Which handoff most often creates delay, ambiguity, missing information, or repeated work?' },
        { type: 'paragraph', title: 'Which workflow variants differ materially by product, customer, channel, branch, value, or risk?' },
        { type: 'paragraph', title: 'Which events reopen completed work, and how are downstream users told that earlier information changed?' },
        { type: 'paragraph', title: 'Which unofficial workarounds are necessary for operations to continue?' },
        { type: 'paragraph', title: 'What happens during peaks, staff absence, partner delay, or system outage?' },
        { type: 'grid', title: 'How difficult are these operating conditions in the environment you know?', rows: ['Finding the current owner and status', 'Finding authoritative evidence', 'Handling exceptions', 'Coordinating external partners', 'Recovering interrupted work', 'Explaining a decision later', 'Seeing workload and risk'], columns: ['Not difficult', 'Slightly difficult', 'Moderately difficult', 'Very difficult', 'Cannot assess'], required: false },
        { type: 'paragraph', title: 'If none of the grid choices fit, or difficulty varies, explain the context and give an example.' },
      ],
    },
    market: {
      title: 'Customers, markets, products, and distribution',
      help: 'Focus on observed behaviour and recent decisions, including alternatives outside traditional insurance.',
      questions: [
        { type: 'paragraph', title: 'Which customer groups are best served today, and which are underserved or excluded?' },
        { type: 'paragraph', title: 'What jobs are customers trying to accomplish when they buy, renew, use, or claim on insurance?' },
        { type: 'paragraph', title: 'What causes prospects to choose no cover, informal risk-sharing, savings, self-insurance, another insurer, or another financial product?' },
        { type: 'paragraph', title: 'Which customer concerns, misunderstandings, trust barriers, or access constraints most affect uptake or retention?' },
        { type: 'paragraph', title: 'How do agents, brokers, banks, digital channels, employers, associations, or partners change the customer journey?' },
        { type: 'paragraph', title: 'Where do incentives or commissions help the customer, and where can they distort advice or behaviour?' },
        { type: 'paragraph', title: 'How are market needs, product performance, complaints, losses, and channel evidence used to change or withdraw products?' },
        { type: 'paragraph', title: 'Which competitor, substitute, or internal alternative is most credible when improving operations, and why?' },
        { type: 'paragraph', title: 'What underserved demand or noncustomer group might become reachable if a major barrier were removed?' },
      ],
    },
    underwriting: {
      title: 'Underwriting, policy administration, and premium operations',
      help: 'Answer only for areas you know directly.',
      questions: [
        { type: 'paragraph', title: 'Walk through a recent quote, referral, bind, issuance, endorsement, renewal, cancellation, or reinstatement.' },
        { type: 'paragraph', title: 'Which data, documents, inspections, models, and human judgments determine whether and on what terms a risk proceeds?' },
        { type: 'paragraph', title: 'How do straight-through cases differ from referrals, exceptions, and overrides?' },
        { type: 'paragraph', title: 'Who can change price, terms, limits, exclusions, or requirements, and how is that authority evidenced?' },
        { type: 'paragraph', title: 'Where do data quality, duplicate entry, missing evidence, channel communication, or approval queues delay issuance?' },
        { type: 'paragraph', title: 'How are premiums billed, collected, allocated, reconciled, followed up, refunded, or written off?' },
        { type: 'paragraph', title: 'How are intermediary remittances, commissions, unallocated cash, and failed payments resolved?' },
        { type: 'paragraph', title: 'Which underwriting or policy decisions should always remain accountable to a named human?' },
      ],
    },
    claims: {
      title: 'Claims, evidence, settlement, and recovery',
      help: 'Do not disclose a real claimant or confidential case. Use generalized recent examples.',
      questions: [
        { type: 'paragraph', title: 'How can a claim enter, and how is it acknowledged, validated, classified, assigned, and prioritized?' },
        { type: 'paragraph', title: 'What evidence is normally required, who decides completeness, and how are conflicting or corrected documents handled?' },
        { type: 'paragraph', title: 'Where do assessors, repairers, medical providers, police, investigators, lawyers, reinsurers, or payment providers enter the workflow?' },
        { type: 'paragraph', title: 'How are coverage review, liability, reserves, fraud signals, authority limits, settlement, repudiation, and payment kept distinct?' },
        { type: 'paragraph', title: 'What causes the longest delays, repeat contacts, disputes, reopenings, or complaints?' },
        { type: 'paragraph', title: 'How are customers told what is missing, what will happen next, who owns the claim, and when to expect an update?' },
        { type: 'paragraph', title: 'How are salvage, subrogation, recoveries, reinsurance notification, and recoverables connected to the claim?' },
        { type: 'paragraph', title: 'What evidence is required to explain or defend a claim decision later?' },
        { type: 'paragraph', title: 'Which claim tasks may be prepared or recommended by automation, and which must never be finally decided or executed by it?' },
      ],
    },
    business: {
      title: 'Business model, economics, and investment decisions',
      help: 'Directional or qualitative answers are welcome. Do not disclose confidential figures.',
      questions: [
        { type: 'paragraph', title: 'Which products, channels, customer groups, or services matter most strategically, and why?' },
        { type: 'paragraph', title: 'Which operating problems create the greatest cost, leakage, lost growth, working-capital pressure, complaint effort, or regulatory burden?' },
        { type: 'paragraph', title: 'Which costs are measured directly, and which important costs remain estimated or invisible?' },
        { type: 'paragraph', title: 'Describe the most recent operations or technology improvement the organization funded. What triggered it?' },
        { type: 'paragraph', title: 'Who recognized the problem, influenced the choice, controlled budget, approved risk, implemented the change, and used the result?' },
        { type: 'paragraph', title: 'Which alternatives were considered: process change, core-system configuration, spreadsheets, outsourcing, RPA, analytics, AI, specialist vendors, or doing nothing?' },
        { type: 'paragraph', title: 'What proof, business case, security review, integration plan, procurement process, and support commitment were required?' },
        { type: 'paragraph', title: 'What implementation, data, migration, training, change, compliance, switching, and ongoing support costs mattered beyond price?' },
        { type: 'paragraph', title: 'How was value measured after implementation, and what would have caused funding to stop?' },
        { type: 'paragraph', title: 'What would a low-risk pilot need to prove before the organization would expand or pay for continued use?' },
        { type: 'paragraph', title: 'What would make the organization reject an operational platform even if the demonstration looked strong?' },
      ],
    },
    governance: {
      title: 'People, governance, compliance, and customer protection',
      help: 'Describe practice, not an assessment of named individuals.',
      questions: [
        { type: 'paragraph', title: 'How are work ownership, delegated authority, segregation of duties, escalation, override, and quality review defined in practice?' },
        { type: 'paragraph', title: 'Which responsibilities depend on tacit knowledge or a small number of people?' },
        { type: 'paragraph', title: 'Which targets or incentives improve work, and which can encourage shortcuts, concealment, unfairness, or poor customer outcomes?' },
        { type: 'paragraph', title: 'How are errors raised, corrected, learned from, and kept visible without creating a blame culture?' },
        { type: 'paragraph', title: 'Which regulatory, audit, financial-crime, privacy, legal, actuarial, or reporting evidence is hardest to produce?' },
        { type: 'paragraph', title: 'How are consent, purpose, access, retention, correction, deletion, disclosure, breach response, and cross-border processing handled operationally?' },
        { type: 'paragraph', title: 'What information must never appear in analytics, logs, prompts, messages, or external tools?' },
        { type: 'paragraph', title: 'How are risk or fraud indicators kept separate from accusations and final decisions?' },
        { type: 'paragraph', title: 'What correction, appeal, complaint, or contestability path exists when a record, recommendation, or decision is wrong?' },
        { type: 'paragraph', title: 'Which efficiency idea would be unacceptable even if it saved substantial time or money?' },
      ],
    },
    technology: {
      title: 'Systems, data, integration, resilience, and automation',
      help: 'Do not include credentials, infrastructure secrets, exploitable weaknesses, or personal data.',
      questions: [
        { type: 'paragraph', title: 'Which systems, spreadsheets, email, messaging, paper, and shared drives are essential to operations?' },
        { type: 'paragraph', title: 'What is the authoritative source for each major customer, policy, claim, document, payment, decision, and audit record?' },
        { type: 'paragraph', title: 'Where is information duplicated, stale, inconsistent, inaccessible, or difficult to reconcile?' },
        { type: 'paragraph', title: 'How do systems exchange data, and which integrations are delayed, fragile, one-way, batch-based, or manual?' },
        { type: 'paragraph', title: 'How are failed jobs, mapping errors, duplicate records, and reconciliation differences detected and owned?' },
        { type: 'paragraph', title: 'What happens when a network, mailbox, core system, provider, integration, or cloud service is unavailable?' },
        { type: 'paragraph', title: 'Which work must continue offline or under low bandwidth, and how is unfinished work safely recovered?' },
        { type: 'paragraph', title: 'Which identity, hosting, residency, security, backup, recovery, support, or vendor requirements are non-negotiable?' },
        { type: 'paragraph', title: 'Which rules, extraction tools, analytics, workflow engines, or AI capabilities are already used?' },
        { type: 'paragraph', title: 'Think about the most recently introduced operational tool. How did staff first gain access and learn to use it?' },
        { type: 'paragraph', title: 'What were you doing the last time an operational tool failed, behaved unexpectedly, or produced an incorrect result?' },
        { type: 'paragraph', title: 'What happened next after that failure?' },
        { type: 'paragraph', title: 'How was support requested, and what evidence was needed before the issue was resolved?' },
        { type: 'paragraph', title: 'Which missing capability in a current tool causes the most extra work today?' },
        { type: 'paragraph', title: 'Which current operational tool would you recommend to a peer, and what observed experience supports that recommendation?' },
        { type: 'paragraph', title: 'What would you warn a peer about before they adopted that tool?' },
        { type: 'paragraph', title: 'Describe one recent automated output that was accepted and one that was corrected, rejected, or escalated.' },
        { type: 'paragraph', title: 'What evidence should a reviewer see before relying on an automated output?' },
        { type: 'paragraph', title: 'How should uncertainty, missing or conflicting evidence, correction, and downstream invalidation be shown?' },
        { type: 'paragraph', title: 'What monitoring would reveal unreliable, unfair, insecure, or over-relied-on automation?' },
      ],
    },
    outcomes: {
      title: 'Outcomes, priorities, and boundaries',
      help: 'Use current baselines where known; estimates should be labelled as estimates.',
      questions: [
        { type: 'paragraph', title: 'Which operational questions can leaders answer confidently today, and which can they not answer?' },
        { type: 'paragraph', title: 'How are volume, backlog, age, handling time, waiting time, SLA, rework, quality, complaints, cost, conversion, retention, leakage, recovery, and customer outcomes defined?' },
        { type: 'paragraph', title: 'Which metric definitions disagree across teams, systems, products, or reports?' },
        { type: 'paragraph', title: 'What baseline evidence would be required before claiming an improvement?' },
        { type: 'paragraph', title: 'What would count as meaningful improvement for frontline users, customers, managers, control functions, and the business?' },
        { type: 'paragraph', title: 'Which fairness, privacy, security, compliance, accessibility, decision-quality, or human-accountability guardrail must not worsen?' },
        { type: 'paragraph', title: 'How should outcomes be segmented so smaller groups, branches, products, channels, or accessibility needs are not hidden?' },
        { type: 'paragraph', title: 'If one operational problem could be fixed during the next twelve months, what should it be, and what recent evidence supports that choice?' },
        { type: 'paragraph', title: 'Who experiences that problem, who owns the outcome, who controls budget, and what is the smallest useful improvement?' },
        { type: 'paragraph', title: 'What must remain unchanged or protected while improving it?' },
        { type: 'paragraph', title: 'Which attractive-looking problem should not be prioritized, and why?' },
        { type: 'paragraph', title: 'What assumption about insurance operations do technology or transformation teams most often get wrong?' },
      ],
    },
    closing: {
      title: 'Closing and optional follow-up',
      help: 'These final questions are optional.',
      questions: [
        { type: 'paragraph', title: 'What question should this study have asked but did not?' },
        { type: 'paragraph', title: 'What else is essential to understand about insurance work, customers, economics, controls, or change?' },
        { type: 'mc', title: 'May the study team invite you to a follow-up interview or redacted artifact walkthrough?', options: ['Yes', 'No', 'Possibly — ask me first'], required: false },
        { type: 'text', title: 'Optional phone or WhatsApp number for follow-up. Leave blank if you do not want contact.', required: false },
        { type: 'paragraph', title: 'If you are open to follow-up, which workflow or artifact could you discuss safely?' },
        { type: 'paragraph', title: 'Any final qualification, confidentiality note, or context for interpreting your answers?' },
      ],
    },
  },
};

function createInsuranceDiscoveryForm() {
  return buildForm_('en');
}

function buildForm_(language) {
  const t = STR[language];
  if (!t) throw new Error('Unsupported language key: ' + language);

  const form = FormApp.create(t.form.title);
  form
    .setDescription(t.form.description)
    .setCollectEmail(false)
    .setLimitOneResponsePerUser(false)
    .setAllowResponseEdits(true)
    .setShowLinkToRespondAgain(false)
    .setShuffleQuestions(false)
    .setProgressBar(true)
    .setPublishingSummary(false)
    .setConfirmationMessage(t.form.confirmation)
    .setCustomClosedFormMessage(t.form.closedMessage);

  const responseBook = SpreadsheetApp.create(t.form.responseSheetTitle);
  form.setDestination(FormApp.DestinationType.SPREADSHEET, responseBook.getId());

  addSectionHeader_(form, t.consent.title, t.consent.help);
  const consentItem = form.addMultipleChoiceItem().setTitle(t.consent.question).setRequired(true);

  const profilePage = form.addPageBreakItem().setTitle(t.profile.title).setHelpText(t.profile.help);
  addQuestionSet_(form, t.profile.questions);
  const routeItem = form.addMultipleChoiceItem().setTitle(t.profile.routeQuestion).setRequired(true);

  const operationsPage = form.addPageBreakItem().setTitle(t.operations.title).setHelpText(t.operations.help);
  addParagraph(form, t.common.mixedRoute, false);
  addQuestionSet_(form, t.operations.questions);

  addSection_(form, t.lifecycle);
  addSection_(form, t.market);
  addSection_(form, t.underwriting);
  addSection_(form, t.claims);

  const businessPage = form.addPageBreakItem().setTitle(t.business.title).setHelpText(t.business.help);
  addQuestionSet_(form, t.business.questions);

  addSection_(form, t.governance);
  addSection_(form, t.technology);
  addSection_(form, t.outcomes);
  addSection_(form, t.closing);

  const declinePage = form.addPageBreakItem().setTitle(t.consent.declineTitle).setHelpText(t.consent.declineHelp);
  declinePage.setGoToPage(FormApp.PageNavigationType.SUBMIT);
  addParagraph(form, t.consent.declineNote, false);

  // Google Forms does not permit choice navigation and a built-in Other option
  // on the same item. Consent therefore has only explicit yes/no choices; the
  // experience branch includes an explicit mixed/other route plus long text.
  consentItem.setChoices([
    consentItem.createChoice(t.consent.agree, profilePage),
    consentItem.createChoice(t.consent.decline, declinePage),
  ]);
  routeItem.setChoices([
    routeItem.createChoice(t.profile.routeOps, operationsPage),
    routeItem.createChoice(t.profile.routeBusiness, businessPage),
    routeItem.createChoice(t.profile.routeMixed, operationsPage),
  ]);

  form.setPublished(true);
  // Keep collection closed until the owner replaces the privacy placeholders,
  // tests both consent paths and all experience branches, and completes a pilot.
  form.setAcceptingResponses(false);
  console.log(t.logs.edit + form.getEditUrl());
  console.log(t.logs.live + form.getPublishedUrl());
  console.log(t.logs.sheet + responseBook.getUrl());

  return {
    editUrl: form.getEditUrl(),
    publishedUrl: form.getPublishedUrl(),
    responseSheetUrl: responseBook.getUrl(),
  };
}

function addSection_(form, section) {
  form.addPageBreakItem().setTitle(section.title).setHelpText(section.help);
  addQuestionSet_(form, section.questions);
}

function addSectionHeader_(form, title, helpText) {
  return form.addSectionHeaderItem().setTitle(title).setHelpText(helpText);
}

function addQuestionSet_(form, questions) {
  questions.forEach(function (question) {
    if (question.type === 'paragraph') {
      addParagraph(form, question.title, question.required === true);
    } else if (question.type === 'text') {
      addText_(form, question.title, question.required === true);
    } else if (question.type === 'mc') {
      addMC_(form, question.title, question.options, question.required === true);
    } else if (question.type === 'check') {
      addCheck_(form, question.title, question.options, question.required === true);
    } else if (question.type === 'grid') {
      addGrid_(form, question.title, question.rows, question.columns, question.required === true);
    } else {
      throw new Error('Unsupported question type: ' + question.type);
    }
  });
}

function addMC_(form, title, options, required) {
  const item = form.addMultipleChoiceItem().setTitle(title).setChoiceValues(options).setRequired(required);
  item.showOtherOption(true);
  return item;
}

function addCheck_(form, title, options, required) {
  const item = form.addCheckboxItem().setTitle(title).setChoiceValues(options).setRequired(required);
  item.showOtherOption(true);
  return item;
}

function addGrid_(form, title, rows, columns, required) {
  return form.addGridItem().setTitle(title).setRows(rows).setColumns(columns).setRequired(required);
}

function addParagraph(form, title, required) {
  return form.addParagraphTextItem().setTitle(title).setRequired(required);
}

function addText_(form, title, required) {
  return form.addTextItem().setTitle(title).setRequired(required);
}
