import { DesktopOnlyGate } from "@/components/DesktopOnlyGate";
import { PortalIntegrationsClient } from "@/app/integrations/PortalIntegrationsClient";

export default function IntegrationsPage() {
  return (
    <DesktopOnlyGate
      title="Integration setup requires a desktop workspace"
      description="Mailbox mappings, provider checks, and ingestion controls can affect live operational routing. Use a desktop or tablet workspace to change integration configuration."
    >
      <PortalIntegrationsClient />
    </DesktopOnlyGate>
  );
}
