import { DesktopOnlyGate } from "@/components/DesktopOnlyGate";
import { TenantOnboardingClient } from "@/app/tenant-onboarding/TenantOnboardingClient";

export default function TenantOnboardingPage() {
  return (
    <DesktopOnlyGate
      title="Tenant onboarding requires a desktop workspace"
      description="Insurer profile, operating scope, and provider readiness can affect tenant-wide behavior. Use a desktop or tablet workspace to complete setup."
    >
      <TenantOnboardingClient />
    </DesktopOnlyGate>
  );
}
