import { useUI } from "@/context/UIStateContext";
import { canUseLocalTestDoubles } from "@/lib/config";
import { LOVABLE_VISUAL_PARITY_MODE } from "@/lib/lovable-parity/mode";
import { previewLandingRoute, ROLES, STAFF_ROLES, type Role } from "@/lib/roles";
import { UserCog } from "lucide-react";
import { useNavigate } from "@/components/iip/NextRouterCompat";
import { useEffect, useState } from "react";
import { useRuntimeContext } from "@/lib/runtimeContext";

export default function RoleSwitcher() {
  const { role, setRole, hydrated } = useUI();
  const { runtimeContext } = useRuntimeContext();
  const announcementKey = "iip.preview-role-announcement";
  const [announcement, setAnnouncement] = useState("");
  const [pendingNavigation, setPendingNavigation] = useState<{ role: Role; route: string } | null>(null);
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

  useEffect(() => {
    if (!pendingNavigation || role !== pendingNavigation.role) return;
    nav(pendingNavigation.route);
    setPendingNavigation(null);
  }, [nav, pendingNavigation, role]);

  if (!canUseLocalTestDoubles && !LOVABLE_VISUAL_PARITY_MODE) {
    return null;
  }

  return (
    // WO: the row used to also carry a trailing "experience · Preview only" caption. With the
    // sidebar's fixed 240px expanded width, that caption plus the longest role name
    // ("Workspace Administrator") never fit on one nowrap flex row, the caption span rendered
    // with a squeezed effective width and, because the label allows visible overflow, painted
    // outside its own parent's box instead of wrapping or clipping, reading as a stray fragment of
    // text floating below the row. "Previewing" already states the same fact the caption
    // repeated, so the fix is to drop the redundant caption rather than fight the width, and
    // min-w-0 + truncate on the select is a genuine width guard, not just a fix for today's
    // longest label, in case a future role name is longer still.
    <label className="hidden md:flex items-center gap-2 text-[12px] text-ink-soft rounded-xl border border-line bg-white/70 px-2.5 py-1.5 hover-lift cursor-pointer">
      <UserCog className="h-3.5 w-3.5 shrink-0 text-violet" />
      <span className="shrink-0 text-muted">Previewing</span>
      <select
        aria-label="Preview staff experience"
        value={role}
        disabled={!hydrated}
        onChange={(e) => {
          const next = e.target.value as Role;
          const nextAnnouncement = `Previewing ${ROLES[next].label} experience. Signed-in authority remains ${runtimeContext?.roleLabel ?? "unchanged"}.`;
          setRole(next);
          setAnnouncement(nextAnnouncement);
          try { sessionStorage.setItem(announcementKey, nextAnnouncement); } catch { /* best effort */ }
          setPendingNavigation({ role: next, route: previewLandingRoute(next) });
        }}
        className="min-w-0 truncate bg-transparent outline-none text-ink font-medium pr-1 disabled:cursor-wait disabled:opacity-60"
      >
        {STAFF_ROLES.map((key) => ROLES[key]).map((r) => (
          <option key={r.key} value={r.key}>{r.label}</option>
        ))}
      </select>
      <span className="sr-only" aria-live="polite">{announcement}</span>
    </label>
  );
}
