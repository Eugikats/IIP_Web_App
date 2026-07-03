import { DesktopOnlyGate } from "@/components/DesktopOnlyGate";
import { AdminSettingsClient } from "@/app/admin-settings/AdminSettingsClient";

export default function AdminSettingsPage() {
  return (
    <DesktopOnlyGate
      title="Admin console requires a desktop workspace"
      description="Tenant settings and role changes can affect user access and runtime behavior. Use a desktop or tablet workspace to make administrative changes."
    >
      <AdminSettingsClient />
    </DesktopOnlyGate>
  );
}
