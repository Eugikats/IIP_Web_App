import type { Metadata } from "next";
import IipLandingPage from "@/components/marketing/IipLandingPage";

export const metadata: Metadata = {
  title: { absolute: "Insurance Intelligence Platform (IIP)" },
  description:
    "AI that prepares insurance work for insurers and for banks that sell insurance. Customers are answered on WhatsApp in their own language; people make every decision.",
  openGraph: {
    title: "Insurance Intelligence Platform (IIP)",
    description: "AI that prepares insurance work. People make the decisions.",
    url: "https://app.insuranceintelligenceplatform.com/",
  },
};

export default function HomePage() {
  return <IipLandingPage />;
}
