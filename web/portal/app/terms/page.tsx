import type { Metadata } from "next";
import LegalDocument from "@/components/marketing/LegalDocument";
import { LEGAL_ENTITY, LEGAL_LAST_UPDATED, TERMS_PAGE, TERMS_SECTIONS } from "@/lib/legal/iip-legal-content";

export const metadata: Metadata = {
  title: { absolute: `${TERMS_PAGE.title} | Insurance Intelligence Platform` },
  description: TERMS_PAGE.description,
  alternates: { canonical: `https://app.insuranceintelligenceplatform.com${TERMS_PAGE.canonical}` },
};

export default function TermsPage() {
  return (
    <LegalDocument
      title={TERMS_PAGE.title}
      lastUpdated={LEGAL_LAST_UPDATED}
      entity={LEGAL_ENTITY}
      sections={TERMS_SECTIONS}
      alsoSee={{ href: "/privacy", label: "Data Privacy & Protection Policy" }}
    />
  );
}
