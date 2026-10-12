import Link from "next/link";
import type { ReactNode } from "react";
import MarketingFooter from "./MarketingFooter";
import MarketingNav from "./MarketingNav";
import { MarketingContainer } from "./motion";
import type { LegalSection } from "@/lib/legal/iip-legal-content";

// A public legal page (terms, privacy): the marketing navigation and footer around a readable document. Text
// comes from lib/legal/iip-legal-content.ts. Lines that start with "- " are list items, and email addresses and
// web addresses become links, so the content file stays plain text.

const LINKABLE = /(https?:\/\/[^\s)]+|[A-Za-z0-9._-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}|myaccount\.google\.com\/connections|myapps\.microsoft\.com)/g;

function linkify(text: string): ReactNode[] {
  return text.split(LINKABLE).map((part, index) => {
    if (index % 2 === 0) return part;
    const href = part.includes("@") ? `mailto:${part}` : part.startsWith("http") ? part : `https://${part}`;
    return (
      <a key={index} href={href} className="font-medium text-[var(--mkt-brand)] underline" {...(part.includes("@") ? {} : { target: "_blank", rel: "noopener noreferrer" })}>
        {part}
      </a>
    );
  });
}

function Paragraphs({ paragraphs }: { paragraphs: string[] }) {
  const blocks: ReactNode[] = [];
  let bullets: string[] = [];
  const flush = (key: string) => {
    if (bullets.length === 0) return;
    blocks.push(
      <ul key={key} className="m-0 mt-2 list-disc space-y-1 pl-6">
        {bullets.map((item, i) => <li key={i}>{linkify(item)}</li>)}
      </ul>,
    );
    bullets = [];
  };
  paragraphs.forEach((paragraph, index) => {
    if (paragraph.startsWith("- ")) {
      bullets.push(paragraph.slice(2));
      return;
    }
    flush(`ul-${index}`);
    const indented = /^[a-z]\. /.test(paragraph);
    blocks.push(
      <p key={index} className={`m-0 mt-2 ${indented ? "pl-5" : ""}`}>{linkify(paragraph)}</p>,
    );
  });
  flush("ul-end");
  return <>{blocks}</>;
}

export default function LegalDocument({
  title,
  lastUpdated,
  entity,
  sections,
  alsoSee,
}: {
  title: string;
  lastUpdated: string;
  entity: string;
  sections: LegalSection[];
  alsoSee: { href: string; label: string };
}) {
  return (
    <div className="iip-marketing min-h-dvh bg-[var(--mkt-muted-bg)]">
      <MarketingNav />
      <main className="py-12 lg:py-16">
        <MarketingContainer>
          <div className="mx-auto max-w-3xl">
            <h1 className="m-0 text-balance text-3xl font-bold tracking-tight text-[var(--mkt-ink)]">{title}</h1>
            <p className="m-0 mt-2 text-sm text-[var(--mkt-muted)]">
              Last updated: {lastUpdated} &middot; {entity}
            </p>

            <nav aria-label="On this page" className="mt-6 rounded-[10px] border border-[var(--mkt-border)] bg-white p-5">
              <h2 className="m-0 text-xs font-bold uppercase tracking-wider text-[var(--mkt-fg)]">On this page</h2>
              <ol className="m-0 mt-3 columns-1 list-none gap-x-8 space-y-1.5 p-0 text-sm sm:columns-2">
                {sections.map((section) => (
                  <li key={section.id} className="break-inside-avoid">
                    <a href={`#${section.id}`} className="text-[var(--mkt-muted)] hover:text-[var(--mkt-brand)]">{section.title}</a>
                  </li>
                ))}
              </ol>
            </nav>

            <article className="mt-6 space-y-7 rounded-[10px] border border-[var(--mkt-border)] bg-white p-6 text-sm leading-7 text-[var(--mkt-fg)] sm:p-8">
              {sections.map((section) => (
                <section key={section.id} id={section.id} className="scroll-mt-24">
                  <h2 className="m-0 text-base font-semibold text-[var(--mkt-ink)]">{section.title}</h2>
                  <Paragraphs paragraphs={section.paragraphs} />
                </section>
              ))}
            </article>

            <p className="m-0 mt-6 text-sm text-[var(--mkt-muted)]">
              See also{" "}
              <Link href={alsoSee.href} className="font-medium text-[var(--mkt-brand)] underline">{alsoSee.label}</Link>.
            </p>
          </div>
        </MarketingContainer>
      </main>
      <MarketingFooter />
    </div>
  );
}
