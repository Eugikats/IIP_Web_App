"use client";

import { useEffect, useRef, useState } from "react";
import { LogOut, Settings } from "lucide-react";
import { Link } from "@/components/iip/NextRouterCompat";
import { signOut } from "@/lib/entraAuth";
import { canUseLocalTestDoubles } from "@/lib/config";

export default function ProfileMenu({
  displayName,
  roleLabel,
  workspaceName,
  initials,
}: {
  displayName: string;
  roleLabel: string;
  workspaceName: string;
  initials: string;
}) {
  const [open, setOpen] = useState(false);
  const [signingOut, setSigningOut] = useState(false);
  const rootRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    function onPointerDown(event: PointerEvent) {
      if (rootRef.current && !rootRef.current.contains(event.target as Node)) {
        setOpen(false);
      }
    }
    function onKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") setOpen(false);
    }
    document.addEventListener("pointerdown", onPointerDown);
    document.addEventListener("keydown", onKeyDown);
    return () => {
      document.removeEventListener("pointerdown", onPointerDown);
      document.removeEventListener("keydown", onKeyDown);
    };
  }, [open]);

  async function handleSignOut() {
    setSigningOut(true);
    try {
      await signOut();
    } finally {
      setSigningOut(false);
    }
  }

  return (
    <div ref={rootRef} className="relative">
      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        aria-haspopup="menu"
        aria-expanded={open}
        aria-label={`${displayName}. Signed in as ${roleLabel}. Open profile menu`}
        className="flex items-center gap-2.5 rounded-xl sm:ml-2 sm:pl-3 sm:border-l sm:border-line hover:bg-white/70 focus-ring px-1 py-1"
      >
        <div className="text-right leading-tight hidden sm:block">
          <div className="text-[12.5px] font-medium text-ink">{displayName}</div>
          <div className="text-[10.5px] text-muted">Signed in as {roleLabel}</div>
        </div>
        <div className="h-11 w-11 shrink-0 rounded-full bg-gradient-secondary grid place-items-center text-white text-[11px] font-semibold">
          {initials}
        </div>
      </button>

      {open && (
        <div
          role="menu"
          aria-label="Profile menu"
          className="absolute right-0 top-[calc(100%+8px)] z-50 w-64 rounded-2xl border border-line bg-white shadow-l3 p-2"
        >
          <div className="px-3 py-2.5 border-b border-line/70">
            <div className="text-[13px] font-semibold text-ink truncate">{displayName}</div>
            <div className="text-[11px] text-muted truncate">{roleLabel}</div>
            <div className="text-[10.5px] text-muted mt-0.5 truncate">Workspace · {workspaceName}</div>
            {canUseLocalTestDoubles && (
              <div className="text-[10px] text-muted mt-0.5">Local development identity — no session to sign out of.</div>
            )}
          </div>
          <div className="py-1">
            <Link
              to="/app/settings"
              onClick={() => setOpen(false)}
              className="flex items-center gap-2.5 rounded-xl px-3 py-2 text-[13px] text-ink hover:bg-line/60 focus-ring"
              role="menuitem"
            >
              <Settings className="h-4 w-4 text-muted" /> Settings
            </Link>
            <button
              type="button"
              role="menuitem"
              onClick={handleSignOut}
              disabled={signingOut}
              className="flex w-full items-center gap-2.5 rounded-xl px-3 py-2 text-[13px] text-ink hover:bg-line/60 focus-ring disabled:opacity-50"
            >
              <LogOut className="h-4 w-4 text-muted" /> {signingOut ? "Signing out…" : "Sign out"}
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
