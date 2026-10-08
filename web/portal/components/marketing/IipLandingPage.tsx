"use client";

// The public landing page. Same structure as CIP's (navigation, hero, sections built from SectionHeading and
// Reveal, final call to action, footer), written for IIP. Every statement here describes something IIP does
// today. No partner logos and no outcome percentages until IIP has its own evidence for them.

import Link from "next/link";
import type { ReactNode } from "react";
import { ArrowRight, Building2, Check, ShieldCheck } from "lucide-react";
import MarketingFooter from "./MarketingFooter";
import MarketingNav from "./MarketingNav";
import { MarketingContainer, Reveal, SectionHeading } from "./motion";

const products = [
  {
    id: "insurance",
    name: "IIP for Insurance",
    icon: ShieldCheck,
    who: "For insurers, brokers and claims teams",
    summary: "Take claims in on WhatsApp, read the documents, track every claim against its service standard, and keep a full record of who decided what.",
    points: [
      "Claims reported on WhatsApp, in English, Luganda or Kiswahili",
      "Documents read and checked for what is missing",
      "Every claim tracked against its service standard",
      "Advisory fraud signals for a person to review",
      "An audit trail of every action",
    ],
  },
  {
    id: "bancassurance",
    name: "IIP for Bancassurance",
    icon: Building2,
    who: "For banks that sell insurance to their customers",
    summary: "Turn the bank's customers and the people who write in into a worked pipeline: scored, matched to the right cover, and contacted with a person's approval.",
    points: [
      "Each person scored, with two recommended covers and the reasons",
      "An assistant that gets to know each person in conversation",
      "Personal messages drafted for staff to approve and send",
      "Follow-ups for quiet prospects and policies about to end",
      "Covers from several partner insurers, each one named",
    ],
  },
] as const;

const challenges = [
  "Claims arrive by phone, email and WhatsApp, and nobody has one view of them",
  "Files go back and forth because a document is missing",
  "Service-standard deadlines are missed without anyone noticing",
  "Bank customers who need cover are never offered it",
  "Most customers write in their own language, not English",
  "Regulators and partners ask who decided, and why",
];

const capabilities = [
  { title: "WhatsApp assistant", description: "Kato answers customers day and night in English, Luganda or Kiswahili, by text or voice note, and hands over to a person when asked." },
  { title: "Document intelligence", description: "Claim documents are read, checked against what each claim type needs, and the gaps are listed for a person to confirm." },
  { title: "Service-standard tracking", description: "Every claim shows which deadline is running and how long is left, so the ones close to breaching are seen first." },
  { title: "Prospect scoring", description: "Each person is scored from what is recorded about them, with the two covers that fit best and the reasons quoted from the record." },
  { title: "Approved outreach", description: "Messages are drafted for each person. A named person reads, edits and approves before anything is sent." },
  { title: "Training and certificates", description: "IIP Academy teaches staff to use IIP well, in short courses that end with a certificate of completion." },
];

const steps = [
  { title: "A customer writes in", text: "On WhatsApp, in their own language, or from a list the bank imports." },
  { title: "IIP prepares the work", text: "It reads documents, records what the customer said, scores and drafts." },
  { title: "A person decides", text: "Approvals, claim decisions and every outgoing campaign stay with a named person." },
  { title: "The customer is kept informed", text: "Replies go out on WhatsApp, in the customer's language." },
  { title: "Everything is recorded", text: "Who did what, when and why, for the insurer, the bank and the regulator." },
];

const trust = [
  "Runs on Microsoft Azure",
  "Each organisation's data kept apart",
  "People make every decision",
  "Answers cite their sources",
  "Full audit trail",
  "Consent recorded with its wording",
  "Sign in with Microsoft or Google",
];

function PrimaryCta({ href, children, light = false }: { href: string; children: ReactNode; light?: boolean }) {
  return (
    <Link
      href={href}
      className={`inline-flex min-h-11 w-full items-center justify-center gap-2 rounded-[6px] px-7 text-sm font-semibold transition-colors sm:w-auto ${
        light ? "bg-white text-[var(--mkt-ink)] hover:bg-white/90" : "bg-[var(--mkt-brand)] text-white hover:bg-[#1258d6]"
      }`}
    >
      {children}
    </Link>
  );
}

function SecondaryCta({ href, children, light = false }: { href: string; children: ReactNode; light?: boolean }) {
  return (
    <Link
      href={href}
      className={`inline-flex min-h-11 w-full items-center justify-center rounded-[6px] border px-7 text-sm font-semibold transition-colors sm:w-auto ${
        light ? "border-white/30 text-white hover:border-white/60" : "border-[var(--mkt-border)] text-[var(--mkt-fg)] hover:border-[var(--mkt-brand)] hover:text-[var(--mkt-brand)]"
      }`}
    >
      {children}
    </Link>
  );
}

// Shown at once, without the scroll-in animation: the first screen must never wait for JavaScript.
function Hero() {
  return (
    <section className="border-b border-[var(--mkt-border)] bg-[var(--mkt-muted-bg)] pb-16 pt-14 lg:pb-24 lg:pt-20">
      <MarketingContainer>
        <div className="grid items-center gap-12 lg:grid-cols-12">
          <div className="lg:col-span-5">
            <div>
              <p className="m-0 text-xs font-semibold uppercase tracking-[0.14em] text-[var(--mkt-brand)]">Insurance Intelligence Platform</p>
              <h1 className="m-0 mt-4 text-balance text-4xl font-bold leading-[1.1] tracking-tight text-[var(--mkt-ink)] sm:text-5xl">
                AI that prepares insurance work. People make the decisions.
              </h1>
              <p className="m-0 mt-5 max-w-xl text-base leading-7 text-[var(--mkt-muted)] sm:text-lg">
                For insurers handling claims and for banks selling insurance: IIP answers customers on WhatsApp in their own language, prepares every file, and leaves every decision to your team.
              </p>
              <div className="mt-8 flex flex-col gap-3 sm:flex-row">
                <PrimaryCta href="/signup">
                  Sign up
                  <ArrowRight className="h-4 w-4" aria-hidden />
                </PrimaryCta>
                <SecondaryCta href="/login">Sign in</SecondaryCta>
              </div>
            </div>
          </div>
          <div className="lg:col-span-7">
            <div>
              <figure className="m-0 overflow-hidden rounded-2xl border border-[var(--mkt-border)] bg-white">
                {/* eslint-disable-next-line @next/next/no-img-element */}
                <img src="/academy/scores.png" alt="IIP prospect scores: each person with a score, a band and the two covers that fit them" className="block w-full" />
                <figcaption className="border-t border-[var(--mkt-border)] px-4 py-2.5 text-xs text-[var(--mkt-muted)]">The prospect scores screen, shown with demonstration data.</figcaption>
              </figure>
            </div>
          </div>
        </div>
      </MarketingContainer>
    </section>
  );
}

function Products() {
  return (
    <section id="products" className="scroll-mt-24 py-20 lg:py-28">
      <MarketingContainer>
        <SectionHeading eyebrow="Two products, one platform" title="Choose how you use IIP" description="Both run on the same platform, with the same assistant, the same controls and the same record of every decision." />
        <div className="mt-14 grid gap-5 lg:grid-cols-2">
          {products.map((product, index) => (
            <Reveal key={product.id} delay={index * 0.08}>
              <div className="mkt-card flex h-full flex-col p-8">
                <span className="inline-flex h-11 w-11 items-center justify-center rounded-xl bg-[var(--mkt-brand-soft)] text-[var(--mkt-brand)]">
                  <product.icon className="h-5 w-5" aria-hidden />
                </span>
                <h3 className="m-0 mt-5 text-2xl font-bold tracking-tight text-[var(--mkt-fg)]">{product.name}</h3>
                <p className="m-0 mt-1 text-sm font-medium text-[var(--mkt-brand)]">{product.who}</p>
                <p className="m-0 mt-4 text-[15px] leading-7 text-[var(--mkt-muted)]">{product.summary}</p>
                <ul className="m-0 mt-6 list-none space-y-2.5 p-0">
                  {product.points.map((point) => (
                    <li key={point} className="flex gap-2.5 text-sm text-[var(--mkt-fg)]">
                      <Check className="mt-0.5 h-4 w-4 shrink-0 text-[var(--mkt-brand)]" aria-hidden />
                      {point}
                    </li>
                  ))}
                </ul>
                <div className="mt-auto pt-8">
                  <PrimaryCta href={`/signup?product=${product.id}`}>
                    Sign up for {product.name}
                    <ArrowRight className="h-4 w-4" aria-hidden />
                  </PrimaryCta>
                </div>
              </div>
            </Reveal>
          ))}
        </div>
      </MarketingContainer>
    </section>
  );
}

function Challenges() {
  return (
    <section className="border-y border-[var(--mkt-border)] bg-[var(--mkt-muted-bg)] py-20 lg:py-28">
      <MarketingContainer>
        <SectionHeading title="What insurance teams are up against" description="The work is growing faster than the teams doing it, and customers expect answers on the channels they already use." />
        <div className="mt-14 grid gap-5 md:grid-cols-2 lg:grid-cols-3">
          {challenges.map((challenge, index) => (
            <Reveal key={challenge} delay={index * 0.05}>
              <div className="mkt-card h-full p-7">
                <p className="m-0 text-base font-semibold leading-snug text-[var(--mkt-fg)]">{challenge}</p>
              </div>
            </Reveal>
          ))}
        </div>
      </MarketingContainer>
    </section>
  );
}

function Capabilities() {
  return (
    <section id="capabilities" className="scroll-mt-24 py-20 lg:py-28">
      <MarketingContainer>
        <SectionHeading eyebrow="Capabilities" title="What IIP does" description="One platform for answering customers, preparing files and keeping a record, with a person in charge of every decision." />
        <div className="mt-14 grid gap-5 md:grid-cols-2 lg:grid-cols-3">
          {capabilities.map((capability, index) => (
            <Reveal key={capability.title} delay={index * 0.05}>
              <div className="mkt-card h-full p-7">
                <h3 className="m-0 text-lg font-semibold text-[var(--mkt-fg)]">{capability.title}</h3>
                <p className="m-0 mt-3 text-sm leading-6 text-[var(--mkt-muted)]">{capability.description}</p>
              </div>
            </Reveal>
          ))}
        </div>
      </MarketingContainer>
    </section>
  );
}

function HowItWorks() {
  return (
    <section id="how-it-works" className="scroll-mt-24 border-t border-[var(--mkt-border)] py-20 lg:py-28">
      <MarketingContainer>
        <SectionHeading eyebrow="How it works" title="AI prepares. People decide." description="The same five steps run for a claim and for a prospect." />
        <Reveal className="mt-14">
          <ol className="m-0 grid list-none gap-5 p-0 lg:grid-cols-5">
            {steps.map((step, index) => (
              <li key={step.title} className="mkt-card p-7">
                <span className="text-3xl font-bold tabular-nums text-[var(--mkt-brand)]">{String(index + 1).padStart(2, "0")}</span>
                <h3 className="m-0 mt-4 text-base font-semibold text-[var(--mkt-fg)]">{step.title}</h3>
                <p className="m-0 mt-2 text-sm leading-6 text-[var(--mkt-muted)]">{step.text}</p>
              </li>
            ))}
          </ol>
        </Reveal>
      </MarketingContainer>
    </section>
  );
}

function Trust() {
  return (
    <section id="trust" className="scroll-mt-24 border-t border-[var(--mkt-border)] py-20 lg:py-28">
      <MarketingContainer>
        <div className="grid gap-12 lg:grid-cols-12 lg:items-start">
          <div className="lg:col-span-5">
            <SectionHeading
              eyebrow="Security and control"
              title="Built for regulated work"
              description="Insurers and banks need to know where data is, who can see it and who made each decision. IIP is built around those questions."
              align="left"
            />
          </div>
          <div className="flex flex-wrap content-start gap-3 lg:col-span-7">
            {trust.map((item, index) => (
              <Reveal key={item} delay={index * 0.04}>
                <span className="inline-flex rounded-full border border-[var(--mkt-border)] bg-[var(--mkt-muted-bg)] px-5 py-2.5 text-sm font-semibold text-[var(--mkt-fg)]">{item}</span>
              </Reveal>
            ))}
          </div>
        </div>
      </MarketingContainer>
    </section>
  );
}

function FinalCta() {
  return (
    <section className="bg-[var(--mkt-ink)] py-20 lg:py-28">
      <MarketingContainer>
        <Reveal>
          <div className="mx-auto max-w-3xl text-center">
            <h2 className="m-0 text-balance text-3xl font-bold tracking-tight text-white sm:text-4xl">Start with the product that fits you</h2>
            <p className="m-0 mx-auto mt-5 max-w-2xl text-base leading-7 text-white/70 sm:text-lg">
              Sign up for IIP for Insurance or IIP for Bancassurance, and invite your team when you are ready.
            </p>
            <div className="mt-9 flex flex-col items-stretch justify-center gap-3 sm:flex-row sm:items-center">
              <PrimaryCta href="/signup?product=insurance" light>IIP for Insurance</PrimaryCta>
              <SecondaryCta href="/signup?product=bancassurance" light>IIP for Bancassurance</SecondaryCta>
            </div>
            <p className="m-0 mt-6 text-sm text-white/60">
              Already have an account?{" "}
              <Link href="/login" className="font-semibold text-white underline-offset-4 hover:underline">Sign in</Link>
            </p>
          </div>
        </Reveal>
      </MarketingContainer>
    </section>
  );
}

export default function IipLandingPage() {
  return (
    <div className="iip-marketing min-h-dvh bg-white">
      <MarketingNav />
      <main>
        <Hero />
        <Products />
        <Challenges />
        <Capabilities />
        <HowItWorks />
        <Trust />
        <FinalCta />
      </main>
      <MarketingFooter />
    </div>
  );
}
