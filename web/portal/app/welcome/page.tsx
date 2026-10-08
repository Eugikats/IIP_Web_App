import type { Metadata } from "next";
import CreateOrganisation from "@/components/marketing/CreateOrganisation";

export const metadata: Metadata = {
  title: { absolute: "Set up your organisation | Insurance Intelligence Platform" },
};

export default function WelcomePage() {
  return <CreateOrganisation />;
}
