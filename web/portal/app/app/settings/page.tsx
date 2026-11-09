"use client";

import { useNavigate } from "@/components/iip/NextRouterCompat";
import { SettingsModal } from "@/components/settings/SettingsModal";

export default function Page() {
  const navigate = useNavigate();
  return <SettingsModal open section="profile" onSectionChange={() => {}} onClose={() => navigate("/app/workspace", { replace: true })} />;
}
