import type { Metadata } from "next";
import LegalDocument from "@/components/marketing/LegalDocument";
import { LEGAL_ENTITY, PRIVACY_LAST_UPDATED, PRIVACY_PAGE, PRIVACY_SECTIONS } from "@/lib/legal/iip-legal-content";

export const metadata: Metadata = {
  title: { absolute: `${PRIVACY_PAGE.title} | Insurance Intelligence Platform` },
  description: PRIVACY_PAGE.description,
  alternates: { canonical: `https://app.insuranceintelligenceplatform.com${PRIVACY_PAGE.canonical}` },
};

export default function PrivacyPage() {
  return (
    <LegalDocument
      title={PRIVACY_PAGE.title}
      lastUpdated={PRIVACY_LAST_UPDATED}
      entity={LEGAL_ENTITY}
      sections={PRIVACY_SECTIONS}
      alsoSee={{ href: "/terms", label: "Terms and Conditions" }}
    />
  );
}
