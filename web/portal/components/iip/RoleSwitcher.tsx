import { useUI } from "@/context/UIStateContext";
import { canUseLocalTestDoubles } from "@/lib/config";
import { LOVABLE_VISUAL_PARITY_MODE } from "@/lib/lovable-parity/mode";
import { previewLandingRoute, ROLES, STAFF_ROLES, type Role } from "@/lib/roles";
import { UserCog } from "lucide-react";
import { useNavigate } from "@/components/iip/NextRouterCompat";
import { useState } from "react";
import { useRuntimeContext } from "@/lib/runtimeContext";

export default function RoleSwitcher() {
  const { role, setRole } = useUI();
  const { runtimeContext } = useRuntimeContext();
  const [announcement, setAnnouncement] = useState("");
  const nav = useNavigate();
  if (!canUseLocalTestDoubles && !LOVABLE_VISUAL_PARITY_MODE) {
    return null;
  }

  return (
    <label className="hidden md:flex items-center gap-2 text-[12px] text-slate2 rounded-xl border border-cloud bg-white/70 px-2.5 py-1.5 hover-lift cursor-pointer">
      <UserCog className="h-3.5 w-3.5 text-violet2" />
      <span className="text-stone">Previewing</span>
      <select
        aria-label="Preview staff experience"
        value={role}
        onChange={(e) => {
          const next = e.target.value as Role;
          setRole(next);
          setAnnouncement(`Previewing ${ROLES[next].label} experience. Signed-in authority remains ${runtimeContext?.roleLabel ?? "unchanged"}.`);
          nav(previewLandingRoute(next));
        }}
        className="bg-transparent outline-none text-navy font-medium pr-1"
      >
        {STAFF_ROLES.map((key) => ROLES[key]).map((r) => (
          <option key={r.key} value={r.key}>{r.label}</option>
        ))}
      </select>
      <span className="text-[10.5px] text-stone">experience · Preview only</span>
      <span className="sr-only" aria-live="polite">{announcement}</span>
    </label>
  );
}
