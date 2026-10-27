import type { Metadata } from "next";
import LegalDocument from "@/components/marketing/LegalDocument";
import { LEGAL_ENTITY } from "@/lib/legal/iip-legal-content";
import { DATA_PROCESSING_LAST_UPDATED, DATA_PROCESSING_PAGE, DATA_PROCESSING_SECTIONS } from "@/lib/legal/iip-data-processing-content";

export const metadata: Metadata = {
  title: { absolute: `${DATA_PROCESSING_PAGE.title} | Insurance Intelligence Platform` },
  description: DATA_PROCESSING_PAGE.description,
  alternates: { canonical: `https://app.insuranceintelligenceplatform.com${DATA_PROCESSING_PAGE.canonical}` },
};

export default function DataProcessingPage() {
  return (
    <LegalDocument
      title={DATA_PROCESSING_PAGE.title}
      lastUpdated={DATA_PROCESSING_LAST_UPDATED}
      entity={LEGAL_ENTITY}
      sections={DATA_PROCESSING_SECTIONS}
      alsoSee={{ href: "/privacy", label: "Data Privacy & Protection Policy" }}
    />
  );
}
