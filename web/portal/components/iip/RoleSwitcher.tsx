import { useUI } from "@/context/UIStateContext";
import { canUseLocalTestDoubles } from "@/lib/config";
import { LOVABLE_VISUAL_PARITY_MODE } from "@/lib/lovable-parity/mode";
import { previewLandingRoute, ROLES, STAFF_ROLES, type Role } from "@/lib/roles";
import { UserCog } from "lucide-react";
import { useNavigate } from "@/components/iip/NextRouterCompat";
import { useEffect, useState } from "react";
import { useRuntimeContext } from "@/lib/runtimeContext";

export default function RoleSwitcher() {
  const { role, setRole } = useUI();
  const { runtimeContext } = useRuntimeContext();
  const announcementKey = "iip.preview-role-announcement";
  const [announcement, setAnnouncement] = useState("");
  const nav = useNavigate();

  useEffect(() => {
    try { setAnnouncement(sessionStorage.getItem(announcementKey) ?? ""); } catch { /* best effort */ }
  }, []);

  useEffect(() => {
    if (!announcement) return;
    const timer = window.setTimeout(() => {
      setAnnouncement("");
      try { sessionStorage.removeItem(announcementKey); } catch { /* best effort */ }
    }, 5_000);
    return () => window.clearTimeout(timer);
  }, [announcement]);
  if (!canUseLocalTestDoubles && !LOVABLE_VISUAL_PARITY_MODE) {
    return null;
  }

  return (
    <label className="hidden md:flex items-center gap-2 text-[12px] text-ink-soft rounded-xl border border-line bg-white/70 px-2.5 py-1.5 hover-lift cursor-pointer">
      <UserCog className="h-3.5 w-3.5 text-violet" />
      <span className="text-muted">Previewing</span>
      <select
        aria-label="Preview staff experience"
        value={role}
        onChange={(e) => {
          const next = e.target.value as Role;
          const nextAnnouncement = `Previewing ${ROLES[next].label} experience. Signed-in authority remains ${runtimeContext?.roleLabel ?? "unchanged"}.`;
          setRole(next);
          setAnnouncement(nextAnnouncement);
          try { sessionStorage.setItem(announcementKey, nextAnnouncement); } catch { /* best effort */ }
          nav(previewLandingRoute(next));
        }}
        className="bg-transparent outline-none text-ink font-medium pr-1"
      >
        {STAFF_ROLES.map((key) => ROLES[key]).map((r) => (
          <option key={r.key} value={r.key}>{r.label}</option>
        ))}
      </select>
      <span className="text-[10.5px] text-muted">experience · Preview only</span>
      <span className="sr-only" aria-live="polite">{announcement}</span>
    </label>
  );
}
